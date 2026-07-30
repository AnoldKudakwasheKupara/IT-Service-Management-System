using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Helpers;
using IT_Service_Management_System.Models;
using IT_Service_Management_System.Services.Realtime;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.EntityFrameworkCore;
using static IT_Service_Management_System.Models.Ticket;

namespace IT_Service_Management_System.Services.Itsm
{
    /// <summary>The outcome of a ticket mutation, mapped by the controller to a redirect + flash message.</summary>
    public enum TicketOpStatus { Ok, NotFound, Concurrency, Invalid }

    public record TicketOp(TicketOpStatus Status, string? Message = null, Ticket? Ticket = null)
    {
        public static TicketOp Success(Ticket t, string? message = null) => new(TicketOpStatus.Ok, message, t);
        public static readonly TicketOp NotFound = new(TicketOpStatus.NotFound);
        public static readonly TicketOp Concurrency = new(TicketOpStatus.Concurrency,
            "This ticket was changed by someone else while you were working on it. Please review the latest version and try again.");
        public static TicketOp Invalid(string message) => new(TicketOpStatus.Invalid, message);
    }

    /// <summary>The outcome of posting a reply (shaped for the AJAX endpoint's JSON response).</summary>
    public record ReplyResult(bool Success, int StatusCode, string? Error = null,
        int MessageId = 0, string SenderName = "", string Time = "", bool IsStaffReply = false, bool IsInternal = false);

    /// <summary>
    /// Owns the helpdesk ticket workflow: creation, replies, and the staff lifecycle actions
    /// (edit, assign, status change, hold/resume with SLA pause, escalate, reopen, close). Also owns
    /// the notification fan-out (email + realtime) and the SLA status-timestamp math, so the controller
    /// stays a thin request-coordinator. Every mutation translates an EF optimistic-concurrency conflict
    /// into <see cref="TicketOp.Concurrency"/> instead of surfacing an unhandled 500.
    /// </summary>
    public class TicketService
    {
        private readonly ApplicationDbContext _db;
        private readonly AuditService _audit;
        private readonly EmailDispatcher _email;
        private readonly ISlaService _sla;
        private readonly IRealtimeNotifier _rt;
        private readonly IHttpContextAccessor _http;
        private readonly LinkGenerator _links;
        private readonly TimeProvider _clock;
        private readonly ILogger<TicketService> _logger;

        public TicketService(ApplicationDbContext db, AuditService audit, EmailDispatcher email,
            ISlaService sla, IRealtimeNotifier rt, IHttpContextAccessor http, LinkGenerator links,
            TimeProvider clock, ILogger<TicketService> logger)
        {
            _db = db;
            _audit = audit;
            _email = email;
            _sla = sla;
            _rt = rt;
            _http = http;
            _links = links;
            _clock = clock;
            _logger = logger;
        }

        /// <summary>
        /// Current time from the injected clock. Local rather than UTC, because every persisted ticket
        /// timestamp is already local — switching would silently reinterpret existing rows. Going through
        /// <see cref="TimeProvider"/> is what makes the SLA math testable without waiting on a wall clock.
        /// </summary>
        private DateTime Now => _clock.GetLocalNow().DateTime;

        // ── role / recipient helpers ──────────────────────────────────────────────────
        public static bool IsStaff(string? role) => Roles.IsHelpdeskStaff(role);

        public Task<List<User>> StaffRecipientsAsync() =>
            _db.Users.AsNoTracking()
                .Where(u => u.IsActive && (u.Role == UserRole.Admin || u.Role == UserRole.SystemsAdmin || u.Role == UserRole.SupportAgent))
                .ToListAsync();

        public Task<List<User>> AgentsAsync() =>
            _db.Users.AsNoTracking()
                .Where(u => u.Role == UserRole.Admin || u.Role == UserRole.SystemsAdmin || u.Role == UserRole.SupportAgent)
                .OrderBy(u => u.FirstName).ThenBy(u => u.LastName)
                .ToListAsync();

        private string TicketLink(int id) =>
            _links.GetUriByAction(_http.HttpContext!, "Details", "Tickets", new { id })
            ?? $"/Tickets/Details/{id}";

        private string ActorName() => _http.HttpContext?.Session.GetString("UserName") ?? "Support";

        private void QueueEmail(string toEmail, string toName, string subject, string body) =>
            _email.Queue(toEmail, toName, subject, body);

        // ── create ────────────────────────────────────────────────────────────────────
        public async Task<Ticket> CreateAsync(Ticket ticket, int creatorId)
        {
            var now = Now;
            ticket.CreatedAt = now;
            ticket.UpdatedAt = now;
            ticket.Status = TicketStatus.Open;
            ticket.CreatedById = creatorId;
            ticket.AssignedToId = null;

            var targets = await _sla.ComputeAsync(ticket.Priority, ticket.Category, ticket.CreatedAt);
            ticket.SlaPolicyId = targets.PolicyId;
            ticket.ResponseDueAt = targets.ResponseDueAt;
            ticket.DueAt = targets.ResolutionDueAt ?? TicketSla.DueFrom(ticket.CreatedAt, ticket.Priority);

            _db.Tickets.Add(ticket);
            await _db.SaveChangesAsync();

            await _audit.LogAsync("Created", "Ticket", ticket.Id, $"Ticket '{ticket.Title}' created");

            await _rt.NotifyStaffAsync(new RealtimeNotice(
                $"New ticket {ticket.Reference}", ticket.Title, TicketLink(ticket.Id),
                ticket.Priority == TicketPriority.Critical ? "error" : "info"));

            var creator = await _db.Users.FindAsync(creatorId);
            var creatorName = creator?.FullName ?? "A user";
            var link = TicketLink(ticket.Id);
            foreach (var staff in await StaffRecipientsAsync())
            {
                QueueEmail(staff.Email, staff.FirstName,
                    $"[New Ticket {ticket.Reference}] {ticket.Title}",
                    EmailTemplates.TicketCreatedForStaff(ticket.Reference, ticket.Title, ticket.Description,
                        ticket.Priority.ToString(), creatorName, link));
            }

            return ticket;
        }

        // ── edit (staff) ────────────────────────────────────────────────────────────────
        public async Task<TicketOp> EditAsync(Ticket updated)
        {
            var ticket = await _db.Tickets.Include(t => t.CreatedBy)
                .FirstOrDefaultAsync(t => t.Id == updated.Id);
            if (ticket == null) return TicketOp.NotFound;

            var oldStatus = ticket.Status;
            var oldAssignee = ticket.AssignedToId;

            // The edit form posts a free-choice status dropdown, so it needs the same gate as the
            // quick-change action — otherwise it is a way around the workflow.
            if (!TicketWorkflow.CanTransition(oldStatus, updated.Status))
                return TicketOp.Invalid(TicketWorkflow.Describe(oldStatus, updated.Status));

            if (await ValidateAssigneeAsync(updated.AssignedToId) is { } assigneeError)
                return assigneeError;

            ticket.Title = updated.Title;
            ticket.Description = updated.Description;
            ticket.Status = updated.Status;
            ticket.Priority = updated.Priority;
            ticket.AssignedToId = updated.AssignedToId;
            ticket.UpdatedAt = Now;
            ApplyStatusTimestamps(ticket, oldStatus, Now);

            // Detect a concurrent edit when the form round-trips the original RowVersion.
            if (updated.RowVersion != null)
                _db.Entry(ticket).Property(t => t.RowVersion).OriginalValue = updated.RowVersion;

            try { await _db.SaveChangesAsync(); }
            catch (DbUpdateConcurrencyException) { return TicketOp.Concurrency; }

            await _audit.LogAsync("Updated", "Ticket", ticket.Id, $"Ticket '{ticket.Title}' updated");
            NotifyStatusChange(ticket, oldStatus);
            await NotifyAssignmentAsync(ticket, oldAssignee);
            return TicketOp.Success(ticket);
        }

        // ── assign (staff) ──────────────────────────────────────────────────────────────
        public async Task<TicketOp> AssignAsync(int id, int? assignedToId)
        {
            var ticket = await _db.Tickets.FindAsync(id);
            if (ticket == null) return TicketOp.NotFound;

            if (await ValidateAssigneeAsync(assignedToId) is { } assigneeError) return assigneeError;

            var oldAssignee = ticket.AssignedToId;
            ticket.AssignedToId = assignedToId;
            ticket.UpdatedAt = Now;
            if (ticket.Status == TicketStatus.Open && assignedToId != null)
                ticket.Status = TicketStatus.InProgress;

            try { await _db.SaveChangesAsync(); }
            catch (DbUpdateConcurrencyException) { return TicketOp.Concurrency; }

            await _audit.LogAsync("Assigned", "Ticket", ticket.Id,
                assignedToId == null ? "Ticket unassigned" : $"Ticket assigned to user #{assignedToId}");
            await NotifyAssignmentAsync(ticket, oldAssignee);
            if (assignedToId != null)
                await _rt.NotifyUserAsync(assignedToId.Value, new RealtimeNotice(
                    $"Assigned to you: {ticket.Reference}", ticket.Title, TicketLink(ticket.Id), "info"));

            return TicketOp.Success(ticket, assignedToId == null ? "Ticket unassigned." : "Ticket assigned.");
        }

        /// <summary>
        /// Rejects an assignee who isn't an active helpdesk agent. The agent dropdown only lists valid
        /// users, but the id is posted by the client, so it has to be re-checked server-side: an arbitrary
        /// user id would otherwise both take the ticket and — via the assignee branch in
        /// <see cref="AddReplyAsync"/> — gain reply access to it. Returns null when the value is fine.
        /// </summary>
        private async Task<TicketOp?> ValidateAssigneeAsync(int? assignedToId)
        {
            if (assignedToId is not int id) return null;   // unassigning is always allowed

            var candidate = await _db.Users.AsNoTracking()
                .Where(u => u.Id == id)
                .Select(u => new { u.IsActive, u.Role })
                .FirstOrDefaultAsync();

            if (candidate == null || !candidate.IsActive || !Roles.IsHelpdeskStaff(candidate.Role))
                return TicketOp.Invalid("Tickets can only be assigned to an active helpdesk agent.");

            return null;
        }

        // ── status quick-change (staff) ──────────────────────────────────────────────────
        public async Task<TicketOp> ChangeStatusAsync(int id, TicketStatus status)
        {
            var ticket = await _db.Tickets.Include(t => t.CreatedBy).FirstOrDefaultAsync(t => t.Id == id);
            if (ticket == null) return TicketOp.NotFound;

            var oldStatus = ticket.Status;
            if (!TicketWorkflow.CanTransition(oldStatus, status))
                return TicketOp.Invalid(TicketWorkflow.Describe(oldStatus, status));

            if (oldStatus != status)
            {
                ticket.Status = status;
                ticket.UpdatedAt = Now;
                ApplyStatusTimestamps(ticket, oldStatus, Now);
                try { await _db.SaveChangesAsync(); }
                catch (DbUpdateConcurrencyException) { return TicketOp.Concurrency; }
                await _audit.LogAsync("Status Changed", "Ticket", ticket.Id, $"Status {oldStatus} -> {status}");
                NotifyStatusChange(ticket, oldStatus);
            }
            return TicketOp.Success(ticket, $"Ticket marked {status}.");
        }

        // ── reopen (owner or staff) ──────────────────────────────────────────────────────
        public async Task<TicketOp> ReopenAsync(int id, int userId, string? role)
        {
            var ticket = await _db.Tickets.Include(t => t.CreatedBy).FirstOrDefaultAsync(t => t.Id == id);
            if (ticket == null) return TicketOp.NotFound;
            if (!IsStaff(role) && ticket.CreatedById != userId)
                return TicketOp.Invalid("You don't have access to this ticket.");

            var oldStatus = ticket.Status;
            ticket.Status = TicketStatus.Open;
            ticket.UpdatedAt = Now;
            ApplyStatusTimestamps(ticket, oldStatus, Now);
            try { await _db.SaveChangesAsync(); }
            catch (DbUpdateConcurrencyException) { return TicketOp.Concurrency; }
            await _audit.LogAsync("Reopened", "Ticket", ticket.Id, "Ticket reopened");
            NotifyStatusChange(ticket, oldStatus);
            return TicketOp.Success(ticket, "Ticket reopened.");
        }

        // ── hold (staff) — pauses the SLA clock ──────────────────────────────────────────
        public async Task<TicketOp> HoldAsync(int id, string? reason, int actorId)
        {
            var ticket = await _db.Tickets.FirstOrDefaultAsync(t => t.Id == id);
            if (ticket == null) return TicketOp.NotFound;
            if (!ticket.IsOpen || ticket.IsOnHold)
                return TicketOp.Invalid("Only an active ticket can be placed on hold.");

            var now = Now;
            var oldStatus = ticket.Status;
            ticket.Status = TicketStatus.OnHold;
            ticket.UpdatedAt = now;
            ApplyStatusTimestamps(ticket, oldStatus, now);   // stamps OnHoldSince (SLA paused)
            _db.TicketMessages.Add(new TicketMessage
            {
                TicketId = id,
                SenderId = actorId,
                Message = "Placed on hold — SLA paused." + (string.IsNullOrWhiteSpace(reason) ? "" : $" Reason: {reason.Trim()}"),
                SentAt = now,
                IsInternal = true
            });
            try { await _db.SaveChangesAsync(); }
            catch (DbUpdateConcurrencyException) { return TicketOp.Concurrency; }
            await _audit.LogAsync("On Hold", "Ticket", id,
                string.IsNullOrWhiteSpace(reason) ? "Ticket placed on hold" : $"On hold: {reason.Trim()}");
            return TicketOp.Success(ticket, "Ticket placed on hold — SLA paused.");
        }

        // ── resume from hold (staff) — resumes the SLA clock ─────────────────────────────
        public async Task<TicketOp> ResumeAsync(int id, int actorId)
        {
            var ticket = await _db.Tickets.FirstOrDefaultAsync(t => t.Id == id);
            if (ticket == null) return TicketOp.NotFound;
            if (!ticket.IsOnHold) return TicketOp.Invalid("This ticket is not on hold.");

            var now = Now;
            var oldStatus = ticket.Status;
            ticket.Status = ticket.AssignedToId != null ? TicketStatus.InProgress : TicketStatus.Open;
            ticket.UpdatedAt = now;
            ApplyStatusTimestamps(ticket, oldStatus, now);   // adds paused time + extends SLA targets
            _db.TicketMessages.Add(new TicketMessage
            {
                TicketId = id,
                SenderId = actorId,
                Message = "Resumed from hold — SLA running again.",
                SentAt = now,
                IsInternal = true
            });
            try { await _db.SaveChangesAsync(); }
            catch (DbUpdateConcurrencyException) { return TicketOp.Concurrency; }
            await _audit.LogAsync("Resumed", "Ticket", id, $"Resumed from hold ({ticket.PausedMinutes} min paused total)");
            return TicketOp.Success(ticket, "Ticket resumed — SLA running again.");
        }

        // ── escalate (staff) — raise priority + alert the team ───────────────────────────
        public async Task<TicketOp> EscalateAsync(int id, string? reason, int actorId)
        {
            var ticket = await _db.Tickets.FirstOrDefaultAsync(t => t.Id == id);
            if (ticket == null) return TicketOp.NotFound;
            if (!ticket.IsOpen) return TicketOp.Invalid("Only an open ticket can be escalated.");

            var now = Now;
            ticket.EscalatedAt = now;
            var oldPriority = ticket.Priority;
            ticket.Priority = ticket.Priority switch
            {
                TicketPriority.Low => TicketPriority.Medium,
                TicketPriority.Medium => TicketPriority.High,
                _ => TicketPriority.Critical
            };
            ticket.UpdatedAt = now;
            _db.TicketMessages.Add(new TicketMessage
            {
                TicketId = id,
                SenderId = actorId,
                Message = $"Escalated (priority {oldPriority} → {ticket.Priority})."
                    + (string.IsNullOrWhiteSpace(reason) ? "" : $" Reason: {reason.Trim()}"),
                SentAt = now,
                IsInternal = true
            });
            try { await _db.SaveChangesAsync(); }
            catch (DbUpdateConcurrencyException) { return TicketOp.Concurrency; }
            await _audit.LogAsync("Escalated", "Ticket", id, $"Escalated; priority {oldPriority} -> {ticket.Priority}");
            await _rt.NotifyStaffAsync(new RealtimeNotice(
                $"Escalated: {ticket.Reference}", ticket.Title, TicketLink(ticket.Id), "error"));
            return TicketOp.Success(ticket, $"Ticket escalated — priority raised to {ticket.Priority}.");
        }

        // ── close (staff) ────────────────────────────────────────────────────────────────
        public async Task<TicketOp> CloseAsync(int id, string? closingNotes, int actorId)
        {
            var ticket = await _db.Tickets.Include(t => t.CreatedBy).FirstOrDefaultAsync(t => t.Id == id);
            if (ticket == null) return TicketOp.NotFound;

            var now = Now;
            var oldStatus = ticket.Status;
            if (!TicketWorkflow.CanTransition(oldStatus, TicketStatus.Closed))
                return TicketOp.Invalid(TicketWorkflow.Describe(oldStatus, TicketStatus.Closed));

            ticket.Status = TicketStatus.Closed;
            ticket.UpdatedAt = now;
            ApplyStatusTimestamps(ticket, oldStatus, now);   // stamps ClosedAt, unpauses a held SLA

            if (!string.IsNullOrWhiteSpace(closingNotes))
                _db.TicketMessages.Add(new TicketMessage
                {
                    TicketId = id,
                    SenderId = actorId,
                    Message = $"[Closing notes] {closingNotes.Trim()}",
                    SentAt = now
                });

            try { await _db.SaveChangesAsync(); }
            catch (DbUpdateConcurrencyException) { return TicketOp.Concurrency; }
            await _audit.LogAsync("Closed", "Ticket", id, $"Ticket closed. Notes: {closingNotes ?? "None"}");
            NotifyStatusChange(ticket, oldStatus);
            return TicketOp.Success(ticket, $"Ticket {ticket.Reference} closed.");
        }

        // ── CSAT (requester) ─────────────────────────────────────────────────────────────
        public async Task<TicketOp> RateSatisfactionAsync(int id, int userId, int rating, string? comment)
        {
            var ticket = await _db.Tickets.FirstOrDefaultAsync(t => t.Id == id);
            if (ticket == null) return TicketOp.NotFound;
            if (ticket.CreatedById != userId) return TicketOp.Invalid("Only the requester can rate this ticket.");
            if (ticket.IsOpen) return TicketOp.Invalid("You can rate a ticket once it's resolved or closed.");

            ticket.SatisfactionRating = Math.Clamp(rating, 1, 5);
            ticket.SatisfactionComment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
            try { await _db.SaveChangesAsync(); }
            catch (DbUpdateConcurrencyException) { return TicketOp.Concurrency; }
            await _audit.LogAsync("Satisfaction Rated", "Ticket", id, $"Rated {ticket.SatisfactionRating}/5");
            return TicketOp.Success(ticket, "Thanks for your feedback!");
        }

        // ── soft delete (staff) ──────────────────────────────────────────────────────────
        public async Task<TicketOp> SoftDeleteAsync(int id)
        {
            var ticket = await _db.Tickets.FirstOrDefaultAsync(t => t.Id == id);
            if (ticket == null) return TicketOp.NotFound;

            ticket.IsDeleted = true;
            ticket.DeletedAt = Now;
            try { await _db.SaveChangesAsync(); }
            catch (DbUpdateConcurrencyException) { return TicketOp.Concurrency; }
            await _audit.LogAsync("Deleted", "Ticket", id, $"Ticket ID {id} deleted (soft)");
            return TicketOp.Success(ticket, $"Ticket {ticket.Reference} deleted.");
        }

        // ── reply ────────────────────────────────────────────────────────────────────────
        /// <summary>
        /// Persists a reply/internal note, applies the first-response + Open→In Progress transitions,
        /// and fans out notifications. Returns the message id so the caller can attach files to it.
        /// Enforces ticket-access rules (owner, assignee, or staff only).
        /// </summary>
        public async Task<ReplyResult> AddReplyAsync(int ticketId, int userId, string? role, string? message, bool isInternal)
        {
            var ticket = await _db.Tickets.Include(t => t.CreatedBy).Include(t => t.AssignedTo)
                .FirstOrDefaultAsync(t => t.Id == ticketId);
            if (ticket == null) return new ReplyResult(false, 404, "Ticket not found.");

            bool staff = IsStaff(role);
            bool isOwner = ticket.CreatedById == userId;
            bool isAssignee = ticket.AssignedToId == userId;
            if (!staff && !isOwner && !isAssignee)
                return new ReplyResult(false, 403, "You don't have access to this ticket.");
            if (ticket.Status == TicketStatus.Closed)
                return new ReplyResult(false, 400, "This ticket is closed.");
            if (string.IsNullOrWhiteSpace(message))
                return new ReplyResult(false, 400, "Message cannot be empty.");

            bool replierIsStaffSide = staff || isAssignee;
            bool internalNote = isInternal && replierIsStaffSide;

            var now = Now;
            var sender = await _db.Users.FindAsync(userId);
            var ticketMessage = new TicketMessage
            {
                TicketId = ticketId,
                SenderId = userId,
                Message = message.Trim(),
                SentAt = now,
                IsInternal = internalNote
            };
            _db.TicketMessages.Add(ticketMessage);

            // A public staff reply (not the owner, not internal) → first response + Open→In Progress.
            if (replierIsStaffSide && !isOwner && !internalNote)
            {
                ticket.FirstRespondedAt ??= now;
                if (ticket.Status == TicketStatus.Open)
                    ticket.Status = TicketStatus.InProgress;
            }
            ticket.UpdatedAt = now;
            await _db.SaveChangesAsync();

            await _audit.LogAsync(internalNote ? "Internal Note Added" : "Reply Added",
                "Ticket", ticketId, internalNote ? "Internal note posted" : "Reply posted");

            var senderName = sender?.FullName ?? "Someone";
            if (!internalNote)
            {
                var link = TicketLink(ticket.Id);
                await NotifyReplyAsync(ticket, isOwner, senderName, ticketMessage.Message, link);

                var notice = new RealtimeNotice(
                    $"Reply on {ticket.Reference}", $"{senderName}: {ticketMessage.Message}", link, "info");
                if (isOwner)
                {
                    await _rt.NotifyStaffAsync(notice);
                    if (ticket.AssignedToId != null) await _rt.NotifyUserAsync(ticket.AssignedToId.Value, notice);
                }
                else
                {
                    await _rt.NotifyUserAsync(ticket.CreatedById, notice);
                }
            }

            return new ReplyResult(true, 200, null, ticketMessage.Id, senderName,
                ticketMessage.SentAt.ToString("MMM dd, yyyy HH:mm"), replierIsStaffSide && !isOwner, internalNote);
        }

        // ── notification helpers ───────────────────────────────────────────────────────
        private async Task NotifyReplyAsync(Ticket ticket, bool replierIsOwner, string senderName, string message, string link)
        {
            var recipients = new List<User>();
            if (replierIsOwner)
            {
                if (ticket.AssignedTo != null) recipients.Add(ticket.AssignedTo);
                else recipients.AddRange(await StaffRecipientsAsync());
            }
            else
            {
                if (ticket.CreatedBy != null) recipients.Add(ticket.CreatedBy);
            }

            foreach (var r in recipients.DistinctBy(u => u.Id))
                QueueEmail(r.Email, r.FirstName,
                    $"[{ticket.Reference}] New reply: {ticket.Title}",
                    EmailTemplates.TicketReply(r.FirstName, ticket.Reference, ticket.Title, senderName, message, link));
        }

        // Not async: queueing is a synchronous hand-off to the background email queue, so an async
        // signature here would only add a state machine and mislead callers into awaiting a send.
        private void NotifyStatusChange(Ticket ticket, TicketStatus oldStatus)
        {
            if (ticket.Status == oldStatus || ticket.CreatedBy == null) return;
            var by = ActorName();
            QueueEmail(ticket.CreatedBy.Email, ticket.CreatedBy.FirstName,
                $"[{ticket.Reference}] Status: {ticket.Status}",
                EmailTemplates.TicketStatusChanged(ticket.CreatedBy.FirstName, ticket.Reference,
                    ticket.Title, ticket.Status.ToString(), by, TicketLink(ticket.Id)));
        }

        private async Task NotifyAssignmentAsync(Ticket ticket, int? oldAssignee)
        {
            if (ticket.AssignedToId == null || ticket.AssignedToId == oldAssignee) return;
            var assignee = await _db.Users.FindAsync(ticket.AssignedToId.Value);
            if (assignee == null) return;
            var by = ActorName();
            QueueEmail(assignee.Email, assignee.FirstName,
                $"[{ticket.Reference}] Assigned to you: {ticket.Title}",
                EmailTemplates.TicketAssigned(assignee.FirstName, ticket.Reference, ticket.Title,
                    ticket.Priority.ToString(), by, TicketLink(ticket.Id)));
        }

        // ── SLA status-timestamp math ────────────────────────────────────────────────────
        /// <summary>
        /// Applies the timestamp side-effects of a status move. Takes <paramref name="now"/> rather than
        /// reading the clock so the pause arithmetic is directly testable.
        /// </summary>
        public static void ApplyStatusTimestamps(Ticket ticket, TicketStatus oldStatus, DateTime now)
        {
            if (ticket.Status == oldStatus) return;

            // On-hold SLA pause: stamp on entering hold; on leaving, add paused time and push the SLA
            // targets out by that amount so the wait doesn't count against the agent.
            if (ticket.Status == TicketStatus.OnHold && oldStatus != TicketStatus.OnHold)
            {
                ticket.OnHoldSince = now;
            }
            else if (oldStatus == TicketStatus.OnHold && ticket.OnHoldSince.HasValue)
            {
                var paused = (int)Math.Round((now - ticket.OnHoldSince.Value).TotalMinutes);
                if (paused > 0)
                {
                    ticket.PausedMinutes += paused;
                    if (ticket.DueAt.HasValue) ticket.DueAt = ticket.DueAt.Value.AddMinutes(paused);
                    if (ticket.ResponseDueAt.HasValue && ticket.FirstRespondedAt == null)
                        ticket.ResponseDueAt = ticket.ResponseDueAt.Value.AddMinutes(paused);
                }
                ticket.OnHoldSince = null;
            }

            if (ticket.Status == TicketStatus.Resolved) ticket.ResolvedAt = now;
            if (ticket.Status == TicketStatus.Closed) ticket.ClosedAt = now;
            if (ticket.Status == TicketStatus.Open || ticket.Status == TicketStatus.InProgress)
            {
                ticket.ResolvedAt = null;
                ticket.ClosedAt = null;
            }
        }
    }
}
