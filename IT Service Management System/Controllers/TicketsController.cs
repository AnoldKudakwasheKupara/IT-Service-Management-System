using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Helpers;
using IT_Service_Management_System.Models;
using IT_Service_Management_System.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static IT_Service_Management_System.Models.Ticket;

namespace IT_Service_Management_System.Controllers
{
    public class TicketsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly AuditService _auditService;
        private readonly EmailDispatcher _email;
        private readonly ILogger<TicketsController> _logger;

        public TicketsController(ApplicationDbContext context, AuditService auditService,
            EmailDispatcher email, ILogger<TicketsController> logger)
        {
            _context = context;
            _auditService = auditService;
            _email = email;
            _logger = logger;
        }

        // ── helpers ──────────────────────────────────────────────────────────────────
        private bool IsStaff(string? role) => role == "Admin" || role == "SystemsAdmin";

        // Session-based auth has no ASP.NET auth scheme, so Forbid() would throw. Redirect instead.
        private IActionResult Denied() => RedirectToAction("AccessDenied", "Home");

        private string TicketLink(int id) => Url.Action("Details", "Tickets", new { id }, Request.Scheme)!;

        private async Task<List<User>> StaffRecipientsAsync() =>
            await _context.Users.AsNoTracking()
                .Where(u => u.IsActive && (u.Role == UserRole.Admin || u.Role == UserRole.SystemsAdmin))
                .ToListAsync();

        private async Task<List<User>> AgentsAsync() =>
            await _context.Users.AsNoTracking()
                .Where(u => u.Role == UserRole.Admin || u.Role == UserRole.SystemsAdmin)
                .OrderBy(u => u.FirstName).ThenBy(u => u.LastName)
                .ToListAsync();

        // Queues the email on the background worker so SMTP latency never blocks the request.
        private Task TrySendEmailAsync(string toEmail, string toName, string subject, string body)
        {
            _email.Queue(toEmail, toName, subject, body);
            return Task.CompletedTask;
        }

        // ── list ─────────────────────────────────────────────────────────────────────
        public async Task<IActionResult> Index()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var role = HttpContext.Session.GetString("UserRole");
            if (userId == null) return RedirectToAction("Login", "Account");

            var query = _context.Tickets
                .Include(t => t.CreatedBy)
                .Include(t => t.AssignedTo)
                .AsQueryable();

            if (!IsStaff(role))
                query = query.Where(t => t.CreatedById == userId);

            var tickets = await query
                .OrderByDescending(t => t.UpdatedAt ?? t.CreatedAt)
                .ToListAsync();

            return View(tickets);
        }

        // ── create ───────────────────────────────────────────────────────────────────
        public IActionResult Create()
        {
            if (HttpContext.Session.GetInt32("UserId") == null)
                return RedirectToAction("Login", "Account");
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Create(Ticket ticket, List<IFormFile> files)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login", "Account");
            if (!ModelState.IsValid) return View(ticket);

            ticket.CreatedAt = DateTime.Now;
            ticket.UpdatedAt = DateTime.Now;
            ticket.Status = TicketStatus.Open;
            ticket.CreatedById = userId.Value;
            ticket.AssignedToId = null;

            _context.Tickets.Add(ticket);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync("Created", "Ticket", ticket.Id, $"Ticket '{ticket.Title}' created");

            await SaveAttachments(files, ticketId: ticket.Id);

            // Notify all admins / systems admins of the new ticket.
            var creator = await _context.Users.FindAsync(userId.Value);
            var creatorName = creator != null ? creator.FullName : "A user";
            var link = TicketLink(ticket.Id);
            foreach (var staff in await StaffRecipientsAsync())
            {
                await TrySendEmailAsync(staff.Email, staff.FirstName,
                    $"[New Ticket {ticket.Reference}] {ticket.Title}",
                    EmailTemplates.TicketCreatedForStaff(ticket.Reference, ticket.Title, ticket.Description,
                        ticket.Priority.ToString(), creatorName, link));
            }

            TempData["Success"] = $"Ticket {ticket.Reference} created. Our team has been notified.";
            return RedirectToAction("Details", new { id = ticket.Id });
        }

        // ── details ──────────────────────────────────────────────────────────────────
        public async Task<IActionResult> Details(int id)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var role = HttpContext.Session.GetString("UserRole");
            if (userId == null) return RedirectToAction("Login", "Account");

            var ticket = await _context.Tickets
                .Include(t => t.CreatedBy)
                .Include(t => t.AssignedTo)
                .Include(t => t.Attachments)
                .Include(t => t.Messages).ThenInclude(m => m.Sender)
                .Include(t => t.Messages).ThenInclude(m => m.Attachments)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (ticket == null) return NotFound();
            if (!IsStaff(role) && ticket.CreatedById != userId) return Denied();

            ViewBag.IsStaff = IsStaff(role);
            ViewBag.Agents = IsStaff(role) ? await AgentsAsync() : new List<User>();
            return View(ticket);
        }

        // ── edit (staff) ─────────────────────────────────────────────────────────────
        public async Task<IActionResult> Edit(int id)
        {
            if (!IsStaff(HttpContext.Session.GetString("UserRole"))) return Denied();

            var ticket = await _context.Tickets
                .Include(t => t.CreatedBy).Include(t => t.AssignedTo)
                .FirstOrDefaultAsync(t => t.Id == id);
            if (ticket == null) return NotFound();

            ViewBag.Agents = await AgentsAsync();
            return View(ticket);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(Ticket updatedTicket)
        {
            if (!IsStaff(HttpContext.Session.GetString("UserRole"))) return Denied();
            if (!ModelState.IsValid)
            {
                ViewBag.Agents = await AgentsAsync();
                return View(updatedTicket);
            }

            var ticket = await _context.Tickets.Include(t => t.CreatedBy)
                .FirstOrDefaultAsync(t => t.Id == updatedTicket.Id);
            if (ticket == null) return NotFound();

            var oldStatus = ticket.Status;
            var oldAssignee = ticket.AssignedToId;

            ticket.Title = updatedTicket.Title;
            ticket.Description = updatedTicket.Description;
            ticket.Status = updatedTicket.Status;
            ticket.Priority = updatedTicket.Priority;
            ticket.AssignedToId = updatedTicket.AssignedToId;
            ticket.UpdatedAt = DateTime.Now;
            ApplyStatusTimestamps(ticket, oldStatus);

            // Optimistic concurrency: when the form round-trips the original RowVersion, use it so
            // a concurrent edit is detected (otherwise fall back to last-write-wins).
            if (updatedTicket.RowVersion != null)
                _context.Entry(ticket).Property(t => t.RowVersion).OriginalValue = updatedTicket.RowVersion;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                TempData["Error"] = "This ticket was changed by someone else while you were editing. Please review the latest version and try again.";
                return RedirectToAction("Edit", new { id = ticket.Id });
            }

            await _auditService.LogAsync("Updated", "Ticket", ticket.Id, $"Ticket '{ticket.Title}' updated");

            await NotifyStatusChangeAsync(ticket, oldStatus);
            await NotifyAssignmentAsync(ticket, oldAssignee);

            return RedirectToAction("Details", new { id = ticket.Id });
        }

        // ── assign (staff) ───────────────────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Assign(int id, int? assignedToId)
        {
            if (!IsStaff(HttpContext.Session.GetString("UserRole"))) return Denied();

            var ticket = await _context.Tickets.FindAsync(id);
            if (ticket == null) return NotFound();

            var oldAssignee = ticket.AssignedToId;
            ticket.AssignedToId = assignedToId;
            ticket.UpdatedAt = DateTime.Now;
            if (ticket.Status == TicketStatus.Open && assignedToId != null)
                ticket.Status = TicketStatus.InProgress;

            await _context.SaveChangesAsync();
            await _auditService.LogAsync("Assigned", "Ticket", ticket.Id,
                assignedToId == null ? "Ticket unassigned" : $"Ticket assigned to user #{assignedToId}");

            await NotifyAssignmentAsync(ticket, oldAssignee);

            TempData["Success"] = assignedToId == null ? "Ticket unassigned." : "Ticket assigned.";
            return RedirectToAction("Details", new { id });
        }

        // ── status quick-change (staff) ──────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeStatus(int id, TicketStatus status)
        {
            if (!IsStaff(HttpContext.Session.GetString("UserRole"))) return Denied();

            var ticket = await _context.Tickets.Include(t => t.CreatedBy).FirstOrDefaultAsync(t => t.Id == id);
            if (ticket == null) return NotFound();

            var oldStatus = ticket.Status;
            if (oldStatus != status)
            {
                ticket.Status = status;
                ticket.UpdatedAt = DateTime.Now;
                ApplyStatusTimestamps(ticket, oldStatus);
                await _context.SaveChangesAsync();
                await _auditService.LogAsync("Status Changed", "Ticket", ticket.Id,
                    $"Status {oldStatus} -> {status}");
                await NotifyStatusChangeAsync(ticket, oldStatus);
            }

            TempData["Success"] = $"Ticket marked {status}.";
            return RedirectToAction("Details", new { id });
        }

        // ── reopen (owner or staff) ──────────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reopen(int id)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var role = HttpContext.Session.GetString("UserRole");
            if (userId == null) return RedirectToAction("Login", "Account");

            var ticket = await _context.Tickets.Include(t => t.CreatedBy).FirstOrDefaultAsync(t => t.Id == id);
            if (ticket == null) return NotFound();
            if (!IsStaff(role) && ticket.CreatedById != userId) return Denied();

            var oldStatus = ticket.Status;
            ticket.Status = TicketStatus.Open;
            ticket.ResolvedAt = null;
            ticket.ClosedAt = null;
            ticket.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();
            await _auditService.LogAsync("Reopened", "Ticket", ticket.Id, "Ticket reopened");
            await NotifyStatusChangeAsync(ticket, oldStatus);

            TempData["Success"] = "Ticket reopened.";
            return RedirectToAction("Details", new { id });
        }

        // ── reply ────────────────────────────────────────────────────────────────────
        [HttpPost]
        public async Task<IActionResult> AddReply(int ticketId, string message, List<IFormFile> files)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var role = HttpContext.Session.GetString("UserRole");
            if (userId == null) return Unauthorized();

            var ticket = await _context.Tickets.Include(t => t.CreatedBy).Include(t => t.AssignedTo)
                .FirstOrDefaultAsync(t => t.Id == ticketId);
            if (ticket == null) return NotFound();

            // Ownership: only the ticket owner, the assignee, or admins/sysadmins may reply.
            bool staff = IsStaff(role);
            bool isOwner = ticket.CreatedById == userId;
            bool isAssignee = ticket.AssignedToId == userId;
            if (!staff && !isOwner && !isAssignee)
                return StatusCode(403, new { success = false, message = "You don't have access to this ticket." });

            if (ticket.Status == TicketStatus.Closed)
                return BadRequest(new { success = false, message = "This ticket is closed." });

            if (string.IsNullOrWhiteSpace(message))
                return BadRequest(new { success = false, message = "Message cannot be empty." });

            var sender = await _context.Users.FindAsync(userId.Value);
            var ticketMessage = new TicketMessage
            {
                TicketId = ticketId,
                SenderId = userId.Value,
                Message = message.Trim(),
                SentAt = DateTime.Now
            };
            _context.TicketMessages.Add(ticketMessage);

            // Replying staff (not the owner) → first response + move Open to In Progress.
            bool replierIsStaffSide = staff || isAssignee;
            if (replierIsStaffSide && !isOwner)
            {
                ticket.FirstRespondedAt ??= DateTime.Now;
                if (ticket.Status == TicketStatus.Open)
                    ticket.Status = TicketStatus.InProgress;
            }
            ticket.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();
            await _auditService.LogAsync("Reply Added", "Ticket", ticketId, "Reply posted");

            await SaveAttachments(files, ticketMessageId: ticketMessage.Id);

            // Email the OTHER party with the message.
            var senderName = sender?.FullName ?? "Someone";
            var link = TicketLink(ticket.Id);
            await NotifyReplyAsync(ticket, isOwner, senderName, ticketMessage.Message, link);

            var time = ticketMessage.SentAt.ToString("MMM dd, yyyy HH:mm");
            return Json(new { success = true, senderName, time, isStaff = replierIsStaffSide && !isOwner });
        }

        // ── close (staff) ────────────────────────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> Close(int id)
        {
            if (!IsStaff(HttpContext.Session.GetString("UserRole"))) return Denied();
            var ticket = await _context.Tickets.Include(t => t.CreatedBy).FirstOrDefaultAsync(t => t.Id == id);
            if (ticket == null) return NotFound();
            return View(ticket);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Close(int id, string closingNotes)
        {
            if (!IsStaff(HttpContext.Session.GetString("UserRole"))) return Denied();

            var ticket = await _context.Tickets.Include(t => t.CreatedBy).FirstOrDefaultAsync(t => t.Id == id);
            if (ticket == null) return NotFound();

            var oldStatus = ticket.Status;
            ticket.Status = TicketStatus.Closed;
            ticket.ClosedAt = DateTime.Now;
            ticket.UpdatedAt = DateTime.Now;

            if (!string.IsNullOrWhiteSpace(closingNotes))
            {
                var actor = await _context.Users.FindAsync(HttpContext.Session.GetInt32("UserId"));
                _context.TicketMessages.Add(new TicketMessage
                {
                    TicketId = id,
                    SenderId = actor?.Id ?? ticket.CreatedById,
                    Message = $"[Closing notes] {closingNotes.Trim()}",
                    SentAt = DateTime.Now
                });
            }

            await _context.SaveChangesAsync();
            await _auditService.LogAsync("Closed", "Ticket", id, $"Ticket closed. Notes: {closingNotes ?? "None"}");
            await NotifyStatusChangeAsync(ticket, oldStatus);

            TempData["Success"] = $"Ticket {ticket.Reference} closed.";
            return RedirectToAction("Details", new { id });
        }

        // ── delete (staff) ───────────────────────────────────────────────────────────
        public async Task<IActionResult> Delete(int id)
        {
            if (!IsStaff(HttpContext.Session.GetString("UserRole"))) return Denied();
            var ticket = await _context.Tickets.Include(t => t.CreatedBy)
                .Include(t => t.Messages).Include(t => t.Attachments)
                .FirstOrDefaultAsync(t => t.Id == id);
            if (ticket == null) return NotFound();
            return View(ticket);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (!IsStaff(HttpContext.Session.GetString("UserRole"))) return Denied();

            var ticket = await _context.Tickets.FirstOrDefaultAsync(t => t.Id == id);
            if (ticket == null) return NotFound();

            // Soft delete — the ticket (and its messages/attachments) are retained for audit,
            // but hidden everywhere by the global query filter.
            ticket.IsDeleted = true;
            ticket.DeletedAt = DateTime.Now;
            await _context.SaveChangesAsync();

            await _auditService.LogAsync("Deleted", "Ticket", id, $"Ticket ID {id} deleted (soft)");
            TempData["Success"] = $"Ticket {ticket.Reference} deleted.";
            return RedirectToAction("Index");
        }

        // ── notification helpers ─────────────────────────────────────────────────────
        private async Task NotifyReplyAsync(Ticket ticket, bool replierIsOwner, string senderName, string message, string link)
        {
            var recipients = new List<User>();
            if (replierIsOwner)
            {
                // Owner replied → notify the assignee, else all staff.
                if (ticket.AssignedTo != null) recipients.Add(ticket.AssignedTo);
                else recipients.AddRange(await StaffRecipientsAsync());
            }
            else
            {
                // Staff/assignee replied → notify the owner.
                if (ticket.CreatedBy != null) recipients.Add(ticket.CreatedBy);
            }

            foreach (var r in recipients.DistinctBy(u => u.Id))
            {
                await TrySendEmailAsync(r.Email, r.FirstName,
                    $"[{ticket.Reference}] New reply: {ticket.Title}",
                    EmailTemplates.TicketReply(r.FirstName, ticket.Reference, ticket.Title, senderName, message, link));
            }
        }

        private async Task NotifyStatusChangeAsync(Ticket ticket, TicketStatus oldStatus)
        {
            if (ticket.Status == oldStatus || ticket.CreatedBy == null) return;
            var by = HttpContext.Session.GetString("UserName") ?? "Support";
            await TrySendEmailAsync(ticket.CreatedBy.Email, ticket.CreatedBy.FirstName,
                $"[{ticket.Reference}] Status: {ticket.Status}",
                EmailTemplates.TicketStatusChanged(ticket.CreatedBy.FirstName, ticket.Reference,
                    ticket.Title, ticket.Status.ToString(), by, TicketLink(ticket.Id)));
        }

        private async Task NotifyAssignmentAsync(Ticket ticket, int? oldAssignee)
        {
            if (ticket.AssignedToId == null || ticket.AssignedToId == oldAssignee) return;
            var assignee = await _context.Users.FindAsync(ticket.AssignedToId.Value);
            if (assignee == null) return;
            var by = HttpContext.Session.GetString("UserName") ?? "Support";
            await TrySendEmailAsync(assignee.Email, assignee.FirstName,
                $"[{ticket.Reference}] Assigned to you: {ticket.Title}",
                EmailTemplates.TicketAssigned(assignee.FirstName, ticket.Reference, ticket.Title,
                    ticket.Priority.ToString(), by, TicketLink(ticket.Id)));
        }

        private static void ApplyStatusTimestamps(Ticket ticket, TicketStatus oldStatus)
        {
            if (ticket.Status == oldStatus) return;
            if (ticket.Status == TicketStatus.Resolved) ticket.ResolvedAt = DateTime.Now;
            if (ticket.Status == TicketStatus.Closed) ticket.ClosedAt = DateTime.Now;
            if (ticket.Status == TicketStatus.Open || ticket.Status == TicketStatus.InProgress)
            {
                ticket.ResolvedAt = null;
                ticket.ClosedAt = null;
            }
        }

        private async Task SaveAttachments(List<IFormFile> files, int? ticketId = null, int? ticketMessageId = null)
        {
            if (files == null || files.Count == 0) return;

            var uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads");
            if (!Directory.Exists(uploadPath)) Directory.CreateDirectory(uploadPath);

            foreach (var file in files)
            {
                if (file.Length == 0) continue;
                var fileName = Guid.NewGuid() + Path.GetExtension(file.FileName);
                var filePath = Path.Combine(uploadPath, fileName);
                using (var stream = new FileStream(filePath, FileMode.Create))
                    await file.CopyToAsync(stream);

                _context.TicketAttachments.Add(new TicketAttachment
                {
                    FileName = file.FileName,
                    FilePath = "/uploads/" + fileName,
                    TicketId = ticketId,
                    TicketMessageId = ticketMessageId
                });
            }
            await _context.SaveChangesAsync();
        }
    }
}
