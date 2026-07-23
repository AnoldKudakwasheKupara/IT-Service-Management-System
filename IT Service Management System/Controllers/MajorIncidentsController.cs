using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Helpers;
using IT_Service_Management_System.Models.Itsm;
using IT_Service_Management_System.Services;
using IT_Service_Management_System.Services.Realtime;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IT_Service_Management_System.Controllers
{
    /// <summary>
    /// ITIL Major Incident Management — declaration, command-team assignment, affected services/CIs,
    /// a response timeline, stakeholder communications, recovery/resolution and a post-incident review
    /// with follow-up actions.
    /// </summary>
    [IT_Service_Management_System.Filters.RoleAuthorize("Admin", "SystemsAdmin")]
    public class MajorIncidentsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly AuditService _audit;
        private readonly IRealtimeNotifier _rt;

        public MajorIncidentsController(ApplicationDbContext db, AuditService audit, IRealtimeNotifier rt)
        {
            _db = db; _audit = audit; _rt = rt;
        }

        private int? Uid => HttpContext.Session.GetInt32("UserId");

        private async Task PopulateListsAsync()
        {
            ViewBag.Agents = await _db.Users.Where(u => u.IsActive).OrderBy(u => u.FirstName)
                .Select(u => new { u.Id, Name = u.FirstName + " " + u.LastName }).ToListAsync();
            ViewBag.Cis = await _db.ConfigurationItems.OrderBy(c => c.Name)
                .Select(c => new { c.Id, c.Name }).ToListAsync();
            ViewBag.Tickets = await _db.Tickets.OrderByDescending(t => t.CreatedAt).Take(200)
                .Select(t => new { t.Id, Label = t.Reference + " · " + t.Title }).ToListAsync();
        }

        // ── Register ────────────────────────────────────────────────────────────────
        public async Task<IActionResult> Index(MajorIncidentStatus? status, MajorIncidentSeverity? severity, string? q)
        {
            IQueryable<MajorIncident> query = _db.MajorIncidents
                .Include(m => m.Commander).Include(m => m.TechnicalLead);
            if (status.HasValue) query = query.Where(m => m.Status == status.Value);
            if (severity.HasValue) query = query.Where(m => m.Severity == severity.Value);
            if (!string.IsNullOrWhiteSpace(q))
            { var t = q.Trim(); query = query.Where(m => m.Title.Contains(t) || (m.Summary != null && m.Summary.Contains(t))); }

            var all = await _db.MajorIncidents.AsNoTracking()
                .Select(m => new { m.Status, m.Severity, m.DeclaredAt, m.ResolvedAt }).ToListAsync();
            ViewBag.Total = all.Count;
            ViewBag.Open = all.Count(m => m.Status != MajorIncidentStatus.Closed);
            ViewBag.Sev1 = all.Count(m => m.Severity == MajorIncidentSeverity.Sev1 && m.Status != MajorIncidentStatus.Closed);
            var mttr = all.Where(m => m.ResolvedAt.HasValue)
                .Select(m => (m.ResolvedAt!.Value - m.DeclaredAt).TotalMinutes).Where(x => x >= 0).ToList();
            ViewBag.MttrHours = mttr.Count == 0 ? (double?)null : Math.Round(mttr.Average() / 60.0, 1);
            ViewBag.Status = status; ViewBag.Severity = severity; ViewBag.Q = q;

            return View(await query.OrderByDescending(m => m.DeclaredAt).ToListAsync());
        }

        // ── Declaration ───────────────────────────────────────────────────────────────
        public async Task<IActionResult> Declare()
        {
            await PopulateListsAsync();
            return View(new MajorIncident { DetectedAt = DateTime.Now, DeclaredAt = DateTime.Now });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Declare(MajorIncident input)
        {
            if (!ModelState.IsValid) { await PopulateListsAsync(); return View(input); }

            input.DeclaredById = Uid;
            input.DeclaredAt = DateTime.Now;
            input.CreatedAt = DateTime.Now;
            input.Status = MajorIncidentStatus.Declared;
            _db.MajorIncidents.Add(input);
            await _db.SaveChangesAsync();

            _db.MajorIncidentTimelineEntries.Add(new MajorIncidentTimelineEntry
            {
                MajorIncidentId = input.Id, OccurredAt = DateTime.Now, Type = MajorIncidentEventType.StatusChange,
                Detail = $"Major incident declared ({input.Severity}).", LoggedById = Uid
            });
            await _db.SaveChangesAsync();

            await _audit.LogAsync("Declare", "MajorIncident", input.Id, $"{input.Reference} — {input.Title}");
            await _rt.NotifyStaffAsync(new RealtimeNotice(
                $"Major incident declared: {input.Reference}",
                $"{input.Severity} — {input.Title}",
                Url: $"/MajorIncidents/Details/{input.Id}", Level: "danger"));

            TempData["Success"] = $"Major incident {input.Reference} declared.";
            return RedirectToAction(nameof(Details), new { id = input.Id });
        }

        // ── Command console ─────────────────────────────────────────────────────────
        public async Task<IActionResult> Details(int id)
        {
            var m = await _db.MajorIncidents
                .Include(x => x.DeclaredBy).Include(x => x.Commander).Include(x => x.TechnicalLead)
                .Include(x => x.CommunicationsLead).Include(x => x.ReviewFacilitator).Include(x => x.SourceTicket)
                .Include(x => x.AffectedItems).ThenInclude(a => a.ConfigurationItem)
                .Include(x => x.Timeline).ThenInclude(t => t.LoggedBy)
                .Include(x => x.Updates).ThenInclude(u => u.PostedBy)
                .Include(x => x.FollowUps).ThenInclude(f => f.Owner)
                .FirstOrDefaultAsync(x => x.Id == id);
            if (m == null) return NotFound();
            await PopulateListsAsync();
            return View(m);
        }

        public async Task<IActionResult> Edit(int id)
        {
            var m = await _db.MajorIncidents.FindAsync(id);
            if (m == null) return NotFound();
            await PopulateListsAsync();
            return View(m);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(MajorIncident input)
        {
            var m = await _db.MajorIncidents.FindAsync(input.Id);
            if (m == null) return NotFound();
            if (!ModelState.IsValid) { await PopulateListsAsync(); return View(input); }

            m.Title = input.Title; m.Summary = input.Summary; m.BusinessImpact = input.BusinessImpact;
            m.Severity = input.Severity; m.DetectedAt = input.DetectedAt;
            m.SourceTicketId = input.SourceTicketId;
            m.UsersAffected = input.UsersAffected; m.DowntimeMinutes = input.DowntimeMinutes;
            await _db.SaveChangesAsync();
            TempData["Success"] = $"{m.Reference} updated.";
            return RedirectToAction(nameof(Details), new { id = m.Id });
        }

        // ── Command team assignment ───────────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Assign(int id, int? commanderId, int? technicalLeadId, int? communicationsLeadId)
        {
            var m = await _db.MajorIncidents.FindAsync(id);
            if (m == null) return NotFound();
            m.CommanderId = commanderId; m.TechnicalLeadId = technicalLeadId; m.CommunicationsLeadId = communicationsLeadId;
            _db.MajorIncidentTimelineEntries.Add(new MajorIncidentTimelineEntry
            {
                MajorIncidentId = m.Id, Type = MajorIncidentEventType.Decision,
                Detail = "Command team updated.", LoggedById = Uid
            });
            await _db.SaveChangesAsync();

            foreach (var assignee in new[] { commanderId, technicalLeadId, communicationsLeadId })
                if (assignee.HasValue)
                    await _rt.NotifyUserAsync(assignee.Value, new RealtimeNotice(
                        $"Assigned to major incident {m.Reference}", m.Title,
                        Url: $"/MajorIncidents/Details/{m.Id}", Level: "warning"));

            TempData["Success"] = "Command team assigned.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // ── Affected services & CIs ───────────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddAffectedItem(int id, int? configurationItemId, string? serviceName, string? impactNote)
        {
            var m = await _db.MajorIncidents.FindAsync(id);
            if (m == null) return NotFound();
            if (configurationItemId == null && string.IsNullOrWhiteSpace(serviceName))
            { TempData["Error"] = "Pick a configuration item or name a service."; return RedirectToAction(nameof(Details), new { id }); }

            _db.MajorIncidentAffectedItems.Add(new MajorIncidentAffectedItem
            {
                MajorIncidentId = id, ConfigurationItemId = configurationItemId,
                ServiceName = serviceName?.Trim(), ImpactNote = impactNote?.Trim()
            });
            await _db.SaveChangesAsync();
            TempData["Success"] = "Affected service added.";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleRestored(int id, int itemId)
        {
            var item = await _db.MajorIncidentAffectedItems.FirstOrDefaultAsync(a => a.Id == itemId && a.MajorIncidentId == id);
            if (item == null) return NotFound();
            item.Restored = !item.Restored;
            item.RestoredAt = item.Restored ? DateTime.Now : null;
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveAffectedItem(int id, int itemId)
        {
            var item = await _db.MajorIncidentAffectedItems.FirstOrDefaultAsync(a => a.Id == itemId && a.MajorIncidentId == id);
            if (item == null) return NotFound();
            _db.MajorIncidentAffectedItems.Remove(item);
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Details), new { id });
        }

        // ── Response timeline ─────────────────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddTimelineEntry(int id, MajorIncidentEventType type, string detail, DateTime? occurredAt)
        {
            var m = await _db.MajorIncidents.FindAsync(id);
            if (m == null) return NotFound();
            if (string.IsNullOrWhiteSpace(detail))
            { TempData["Error"] = "Timeline entry cannot be empty."; return RedirectToAction(nameof(Details), new { id }); }

            _db.MajorIncidentTimelineEntries.Add(new MajorIncidentTimelineEntry
            {
                MajorIncidentId = id, Type = type, Detail = detail.Trim(),
                OccurredAt = occurredAt ?? DateTime.Now, LoggedById = Uid
            });
            await _db.SaveChangesAsync();
            TempData["Success"] = "Timeline updated.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // ── Stakeholder updates ───────────────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PostUpdate(int id, StakeholderChannel channel, string? audience, string message)
        {
            var m = await _db.MajorIncidents.FindAsync(id);
            if (m == null) return NotFound();
            if (string.IsNullOrWhiteSpace(message))
            { TempData["Error"] = "Update message cannot be empty."; return RedirectToAction(nameof(Details), new { id }); }

            _db.MajorIncidentUpdates.Add(new MajorIncidentUpdate
            {
                MajorIncidentId = id, Channel = channel, Audience = audience?.Trim(),
                Message = message.Trim(), StatusAtUpdate = m.Status, PostedById = Uid
            });
            _db.MajorIncidentTimelineEntries.Add(new MajorIncidentTimelineEntry
            {
                MajorIncidentId = id, Type = MajorIncidentEventType.Communication,
                Detail = $"Stakeholder update issued via {channel}.", LoggedById = Uid
            });
            await _db.SaveChangesAsync();
            TempData["Success"] = "Stakeholder update posted.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // ── Recovery & resolution stages ──────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Advance(int id, MajorIncidentStatus to, string? note)
        {
            var m = await _db.MajorIncidents.FindAsync(id);
            if (m == null) return NotFound();
            if (!MajorIncidentWorkflow.CanTransition(m.Status, to))
            { TempData["Error"] = $"Cannot move from {m.Status} to {to}."; return RedirectToAction(nameof(Details), new { id }); }

            var from = m.Status;
            m.Status = to;
            if (to == MajorIncidentStatus.Recovering) m.RecoveryStartedAt ??= DateTime.Now;
            if (to == MajorIncidentStatus.Resolved) m.ResolvedAt ??= DateTime.Now;
            if (to == MajorIncidentStatus.Closed) m.ClosedAt ??= DateTime.Now;

            _db.MajorIncidentTimelineEntries.Add(new MajorIncidentTimelineEntry
            {
                MajorIncidentId = id, Type = MajorIncidentEventType.StatusChange,
                Detail = $"Status moved {from} → {to}." + (string.IsNullOrWhiteSpace(note) ? "" : $" {note.Trim()}"),
                LoggedById = Uid
            });
            await _db.SaveChangesAsync();
            await _audit.LogAsync("Advance", "MajorIncident", id, $"{from} → {to}");
            TempData["Success"] = $"Status set to {to}.";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Resolve(int id, string? resolutionSummary, string? rootCauseSummary, string? workaround)
        {
            var m = await _db.MajorIncidents.FindAsync(id);
            if (m == null) return NotFound();

            m.ResolutionSummary = resolutionSummary?.Trim();
            m.RootCauseSummary = rootCauseSummary?.Trim();
            if (!string.IsNullOrWhiteSpace(workaround)) m.Workaround = workaround.Trim();
            if (!MajorIncidentWorkflow.IsResolvedState(m.Status))
            {
                m.Status = MajorIncidentStatus.Resolved;
                m.ResolvedAt ??= DateTime.Now;
                _db.MajorIncidentTimelineEntries.Add(new MajorIncidentTimelineEntry
                {
                    MajorIncidentId = id, Type = MajorIncidentEventType.StatusChange,
                    Detail = "Incident resolved — service restored.", LoggedById = Uid
                });
            }
            await _db.SaveChangesAsync();
            await _audit.LogAsync("Resolve", "MajorIncident", id, m.Reference);
            TempData["Success"] = "Resolution recorded.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // ── Post-incident review ──────────────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SavePir(int id, MajorIncident input, bool complete = false)
        {
            var m = await _db.MajorIncidents.FindAsync(id);
            if (m == null) return NotFound();

            m.ReviewScheduledAt = input.ReviewScheduledAt;
            m.ReviewHeldAt = input.ReviewHeldAt;
            m.ReviewFacilitatorId = input.ReviewFacilitatorId;
            m.PirWhatHappened = input.PirWhatHappened;
            m.PirWhatWentWell = input.PirWhatWentWell;
            m.PirWhatWentWrong = input.PirWhatWentWrong;
            m.PirLessonsLearned = input.PirLessonsLearned;
            if (complete)
            {
                m.ReviewCompleted = true;
                m.ReviewHeldAt ??= DateTime.Now;
            }
            await _db.SaveChangesAsync();
            TempData["Success"] = complete ? "Post-incident review completed." : "Post-incident review saved.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // ── Follow-up actions ─────────────────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddFollowUp(int id, string description, int? ownerId, string? ownerName, DateTime? dueDate)
        {
            var m = await _db.MajorIncidents.FindAsync(id);
            if (m == null) return NotFound();
            if (string.IsNullOrWhiteSpace(description))
            { TempData["Error"] = "Follow-up description is required."; return RedirectToAction(nameof(Details), new { id }); }

            _db.MajorIncidentFollowUps.Add(new MajorIncidentFollowUp
            {
                MajorIncidentId = id, Description = description.Trim(), OwnerId = ownerId,
                OwnerName = ownerName?.Trim(), DueDate = dueDate, Status = FollowUpStatus.Open
            });
            await _db.SaveChangesAsync();
            if (ownerId.HasValue)
                await _rt.NotifyUserAsync(ownerId.Value, new RealtimeNotice(
                    $"Follow-up action from {m.Reference}", description.Trim(),
                    Url: $"/MajorIncidents/Details/{m.Id}", Level: "info"));
            TempData["Success"] = "Follow-up action added.";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetFollowUpStatus(int id, int followUpId, FollowUpStatus status)
        {
            var f = await _db.MajorIncidentFollowUps.FirstOrDefaultAsync(x => x.Id == followUpId && x.MajorIncidentId == id);
            if (f == null) return NotFound();
            f.Status = status;
            f.CompletedAt = status == FollowUpStatus.Done ? DateTime.Now : null;
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var m = await _db.MajorIncidents.FindAsync(id);
            if (m == null) return NotFound();
            var reference = m.Reference;
            _db.MajorIncidents.Remove(m);
            await _db.SaveChangesAsync();
            await _audit.LogAsync("Delete", "MajorIncident", id, reference);
            TempData["Success"] = $"{reference} deleted.";
            return RedirectToAction(nameof(Index));
        }
    }
}
