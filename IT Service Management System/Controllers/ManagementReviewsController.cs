using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Filters;
using IT_Service_Management_System.Helpers.Ims;
using IT_Service_Management_System.Models.Ims;
using IT_Service_Management_System.Services;
using IT_Service_Management_System.Services.Ims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IT_Service_Management_System.Controllers
{
    /// <summary>
    /// Management Review (ISO 9001 / ISO 27001 cl. 9.3) — the periodic top-management review of the
    /// management system: meeting record, attendees, the standard 9.3 inputs (auto-gathered from live
    /// data across the IMS), decisions/conclusions and the resulting action tracking (module 25).
    /// </summary>
    [IT_Service_Management_System.Filters.RoleAuthorize("Admin", "SystemsAdmin", "QualityManager", "DocumentController", "DepartmentManager", "Auditor", "ExternalAuditor")]
    public class ManagementReviewsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly AuditService _audit;

        public ManagementReviewsController(ApplicationDbContext db, AuditService audit)
        {
            _db = db;
            _audit = audit;
        }

        private int? Uid => HttpContext.Session.GetInt32("UserId");
        private string? Role => HttpContext.Session.GetString("UserRole");
        private bool Can(ImsPermission p) => ImsAccess.Can(Role, p);

        private IActionResult Denied() => RedirectToAction("AccessDenied", "Home");

        private void LoadLookups()
        {
            ViewBag.Users = _db.Users.Where(u => u.IsActive).OrderBy(u => u.FirstName).ThenBy(u => u.LastName).ToList();
            ViewBag.Departments = _db.Departments.OrderBy(d => d.Name).ToList();
        }

        // ── LIST ─────────────────────────────────────────────────────────────────
        public async Task<IActionResult> Index(ReviewMeetingStatus? status, string? q)
        {
            var query = _db.ManagementReviews
                .Include(r => r.Chair)
                .Include(r => r.Actions)
                .AsQueryable();

            if (status.HasValue) query = query.Where(r => r.Status == status.Value);
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim();
                query = query.Where(r => r.Title.Contains(term)
                    || (r.Location != null && r.Location.Contains(term)));
            }

            var list = await query.OrderByDescending(r => r.MeetingDate).ToListAsync();

            ViewBag.Status = status;
            ViewBag.Query = q;
            ViewBag.Total = list.Count;
            ViewBag.Planned = list.Count(r => r.Status == ReviewMeetingStatus.Planned || r.Status == ReviewMeetingStatus.Scheduled);
            ViewBag.Held = list.Count(r => r.Status == ReviewMeetingStatus.Held || r.Status == ReviewMeetingStatus.Closed);
            ViewBag.OpenActions = await _db.ManagementReviewActions
                .CountAsync(a => a.Status != ReviewActionStatus.Completed && a.Status != ReviewActionStatus.Cancelled);
            ViewBag.CanManage = Can(ImsPermission.ManageManagementReview);

            return View(list);
        }

        // ── DETAILS ──────────────────────────────────────────────────────────────
        public async Task<IActionResult> Details(int id)
        {
            var review = await _db.ManagementReviews
                .Include(r => r.Chair)
                .Include(r => r.CreatedBy)
                .Include(r => r.Attendees).ThenInclude(a => a.User)
                .Include(r => r.Inputs)
                .Include(r => r.Actions).ThenInclude(a => a.AssignedTo)
                .FirstOrDefaultAsync(r => r.Id == id);
            if (review == null) return NotFound();

            ViewBag.CanManage = Can(ImsPermission.ManageManagementReview);
            LoadLookups();
            return View(review);
        }

        // ── CREATE ───────────────────────────────────────────────────────────────
        public IActionResult Create()
        {
            if (!Can(ImsPermission.ManageManagementReview)) return Denied();
            LoadLookups();
            return View(new ManagementReview());
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ManagementReview model)
        {
            if (!Can(ImsPermission.ManageManagementReview)) return Denied();

            if (!ModelState.IsValid)
            {
                LoadLookups();
                return View(model);
            }

            model.CreatedById = Uid;
            model.CreatedAt = DateTime.Now;
            _db.ManagementReviews.Add(model);
            await _db.SaveChangesAsync();

            await _audit.LogAsync("Created", "ManagementReview", model.Id, $"{model.Reference} — {model.Title}");
            TempData["Success"] = $"Management review {model.Reference} created.";
            return RedirectToAction(nameof(Details), new { id = model.Id });
        }

        // ── EDIT ─────────────────────────────────────────────────────────────────
        public async Task<IActionResult> Edit(int id)
        {
            if (!Can(ImsPermission.ManageManagementReview)) return Denied();
            var review = await _db.ManagementReviews.FindAsync(id);
            if (review == null) return NotFound();
            LoadLookups();
            return View(review);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ManagementReview model)
        {
            if (!Can(ImsPermission.ManageManagementReview)) return Denied();
            var review = await _db.ManagementReviews.FindAsync(id);
            if (review == null) return NotFound();

            if (!ModelState.IsValid) { LoadLookups(); return View(model); }

            review.Title = model.Title;
            review.Standard = model.Standard;
            review.MeetingDate = model.MeetingDate;
            review.Location = model.Location;
            review.ChairId = model.ChairId;
            review.Status = model.Status;
            review.AgendaNotes = model.AgendaNotes;
            review.Decisions = model.Decisions;
            review.Conclusions = model.Conclusions;
            await _db.SaveChangesAsync();

            await _audit.LogAsync("Updated", "ManagementReview", review.Id, $"{review.Reference} — {review.Title}");
            TempData["Success"] = "Management review updated.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // ── DELETE ───────────────────────────────────────────────────────────────
        // Confirmation page — deleting is destructive, so it gets the same explicit
        // acknowledgement step as tickets rather than a browser confirm() popup.
        public async Task<IActionResult> Delete(int id)
        {
            if (!Can(ImsPermission.ManageManagementReview)) return Denied();
            var review = await _db.ManagementReviews.Include(r => r.Chair)
                .FirstOrDefaultAsync(r => r.Id == id);
            if (review == null) return NotFound();

            var vm = new ViewModels.DeleteConfirmationVm
            {
                EntityName = "Management Review",
                Icon = "fa-people-group",
                RecordTitle = review.Title,
                Reference = review.Reference,
                Controller = "ManagementReviews",
                Id = review.Id
            };
            vm.Add("Meeting Date", review.MeetingDate.ToString("dd MMM yyyy"));
            vm.Add("Status", review.Status.ToString());
            vm.Add("Chairperson", review.Chair?.FullName);
            vm.Add("Standard", IsoStandards.Label(review.Standard));
            vm.Consequences.Add("The meeting record — its agenda, decisions and conclusions — will be removed.");
            vm.Consequences.Add("Its attendees, gathered inputs and tracked actions will be deleted with it.");
            vm.Consequences.Add("It will no longer appear in management-review reporting or ISO 9.3 evidence.");
            return View("DeleteConfirm", vm);
        }

        [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (!Can(ImsPermission.ManageManagementReview)) return Denied();
            var review = await _db.ManagementReviews.FindAsync(id);
            if (review == null) return NotFound();

            var reference = review.Reference;
            _db.ManagementReviews.Remove(review);
            await _db.SaveChangesAsync();

            await _audit.LogAsync("Deleted", "ManagementReview", id, $"{reference} deleted.");
            TempData["Success"] = "Management review deleted.";
            return RedirectToAction(nameof(Index));
        }

        // ── GATHER INPUTS (auto-populate the ISO 9.3 inputs from live data) ────────
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> GatherInputs(int id)
        {
            if (!Can(ImsPermission.ManageManagementReview)) return Denied();
            var review = await _db.ManagementReviews.FindAsync(id);
            if (review == null) return NotFound();

            var now = DateTime.Now;

            // Remove any previously gathered inputs so this is an idempotent snapshot of live data.
            var existing = await _db.ManagementReviewInputs.Where(i => i.ManagementReviewId == id).ToListAsync();
            if (existing.Count > 0) _db.ManagementReviewInputs.RemoveRange(existing);

            // 1 — Internal Audit Results
            var auditsCompleted = await _db.Audits.CountAsync(a => a.Status == AuditStatus.Completed || a.Status == AuditStatus.Closed);
            var auditsOpen = await _db.Audits.CountAsync(a => a.Status == AuditStatus.Planned || a.Status == AuditStatus.Scheduled || a.Status == AuditStatus.InProgress);
            var findingsOpen = await _db.AuditFindings.CountAsync(f => f.Status != FindingStatus.Closed && f.Status != FindingStatus.Verified);

            // 2 — CAPAs
            var capaOpen = await _db.Capas.CountAsync(c => c.Status != CapaStatus.Closed && c.Status != CapaStatus.Verified);
            var capaClosed = await _db.Capas.CountAsync(c => c.Status == CapaStatus.Closed || c.Status == CapaStatus.Verified);
            var capaOverdue = await _db.Capas.CountAsync(c => c.Status != CapaStatus.Closed && c.Status != CapaStatus.Verified && c.DueDate.HasValue && c.DueDate < now);

            // 3 — Non-conformities (open, by severity)
            var ncOpen = await _db.NonConformances.CountAsync(n => n.Status != NcStatus.Closed);
            var ncMinor = await _db.NonConformances.CountAsync(n => n.Status != NcStatus.Closed && n.Severity == NcSeverity.Minor);
            var ncMajor = await _db.NonConformances.CountAsync(n => n.Status != NcStatus.Closed && n.Severity == NcSeverity.Major);
            var ncCritical = await _db.NonConformances.CountAsync(n => n.Status != NcStatus.Closed && n.Severity == NcSeverity.Critical);

            // 4 — Risks & Opportunities
            var risksOpen = await _db.Risks.CountAsync(r => r.Status != RiskStatus.Closed);
            var risksCritical = await _db.Risks.CountAsync(r => r.Status != RiskStatus.Closed && r.Likelihood * r.Impact > 15);
            var opportunitiesOpen = await _db.Opportunities.CountAsync(o => o.Status != OpportunityStatus.Closed && o.Status != OpportunityStatus.Declined);

            // 5 — Objectives & KPIs
            var objectivesActive = await _db.Objectives.CountAsync(o => o.Status == ObjectiveStatus.Active || o.Status == ObjectiveStatus.OnTrack || o.Status == ObjectiveStatus.AtRisk);
            var objectivesAtRisk = await _db.Objectives.CountAsync(o => o.Status == ObjectiveStatus.AtRisk || o.Status == ObjectiveStatus.NotAchieved);

            // 6 — Training
            var trainingTotal = await _db.TrainingRecords.CountAsync();
            var trainingCompleted = await _db.TrainingRecords.CountAsync(t => t.Status == AttendanceStatus.Completed);
            var trainingPct = trainingTotal == 0 ? 0 : (int)Math.Round(trainingCompleted * 100.0 / trainingTotal);

            // 7 — Supplier Performance
            var supplierAvg = await _db.SupplierEvaluations.AnyAsync()
                ? (int)Math.Round(await _db.SupplierEvaluations
                    .AverageAsync(e => (e.QualityScore + e.DeliveryScore + e.PricingScore + e.SupportScore + e.ComplianceScore) / 5.0))
                : 0;
            var suppliersApproved = await _db.Suppliers.CountAsync(s => s.Status == SupplierStatus.Approved);

            // 8 — Continuous Improvement
            var improvementsOpen = await _db.Improvements.CountAsync(i => i.Status != ImprovementStatus.Implemented && i.Status != ImprovementStatus.Rejected && i.Status != ImprovementStatus.Closed);
            var improvementsImplemented = await _db.Improvements.CountAsync(i => i.Status == ImprovementStatus.Implemented);

            // 9 — Previous Actions (open actions carried over from prior reviews)
            var previousOpen = await _db.ManagementReviewActions
                .CountAsync(a => a.ManagementReviewId != id && a.Status != ReviewActionStatus.Completed && a.Status != ReviewActionStatus.Cancelled);

            var rows = new List<ManagementReviewInput>
            {
                new() { ManagementReviewId = id, Sequence = 1, Category = "Internal Audit Results",
                    Summary = $"{auditsCompleted} audit(s) completed, {auditsOpen} in progress/planned. {findingsOpen} finding(s) still open." },
                new() { ManagementReviewId = id, Sequence = 2, Category = "CAPAs",
                    Summary = $"{capaOpen} open, {capaClosed} closed/verified, {capaOverdue} overdue." },
                new() { ManagementReviewId = id, Sequence = 3, Category = "Non-conformities",
                    Summary = $"{ncOpen} open — {ncMinor} minor, {ncMajor} major, {ncCritical} critical." },
                new() { ManagementReviewId = id, Sequence = 4, Category = "Risks & Opportunities",
                    Summary = $"{risksOpen} open risk(s) ({risksCritical} critical); {opportunitiesOpen} open opportunity(ies)." },
                new() { ManagementReviewId = id, Sequence = 5, Category = "Objectives & KPIs",
                    Summary = $"{objectivesActive} active objective(s), {objectivesAtRisk} at risk / not achieved." },
                new() { ManagementReviewId = id, Sequence = 6, Category = "Training",
                    Summary = $"{trainingPct}% training completion ({trainingCompleted} of {trainingTotal} records completed)." },
                new() { ManagementReviewId = id, Sequence = 7, Category = "Supplier Performance",
                    Summary = $"Average supplier score {supplierAvg}/100 across evaluations; {suppliersApproved} approved supplier(s)." },
                new() { ManagementReviewId = id, Sequence = 8, Category = "Continuous Improvement",
                    Summary = $"{improvementsOpen} improvement(s) in progress, {improvementsImplemented} implemented." },
                new() { ManagementReviewId = id, Sequence = 9, Category = "Previous Actions",
                    Summary = $"{previousOpen} open action(s) carried over from previous management reviews." }
            };

            _db.ManagementReviewInputs.AddRange(rows);
            await _db.SaveChangesAsync();

            await _audit.LogAsync("GatherInputs", "ManagementReview", id, $"{review.Reference}: {rows.Count} inputs gathered from live data.");
            TempData["Success"] = "Review inputs regenerated from live management-system data.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // ── ATTENDEES ─────────────────────────────────────────────────────────────
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AddAttendee(int id, ManagementReviewAttendee attendee)
        {
            if (!Can(ImsPermission.ManageManagementReview)) return Denied();
            var review = await _db.ManagementReviews.FindAsync(id);
            if (review == null) return NotFound();

            if (attendee.UserId <= 0)
            {
                TempData["Error"] = "Select an attendee to add.";
                return RedirectToAction(nameof(Details), new { id });
            }

            attendee.ManagementReviewId = id;
            _db.ManagementReviewAttendees.Add(attendee);
            await _db.SaveChangesAsync();

            await _audit.LogAsync("AddAttendee", "ManagementReview", id, $"{review.Reference}: attendee added.");
            TempData["Success"] = "Attendee added.";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveAttendee(int id, int attendeeId)
        {
            if (!Can(ImsPermission.ManageManagementReview)) return Denied();
            var attendee = await _db.ManagementReviewAttendees.FirstOrDefaultAsync(a => a.Id == attendeeId && a.ManagementReviewId == id);
            if (attendee == null) return NotFound();
            _db.ManagementReviewAttendees.Remove(attendee);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Attendee removed.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // ── ACTIONS ───────────────────────────────────────────────────────────────
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AddAction(int id, ManagementReviewAction action)
        {
            if (!Can(ImsPermission.ManageManagementReview)) return Denied();
            var review = await _db.ManagementReviews.FindAsync(id);
            if (review == null) return NotFound();

            if (string.IsNullOrWhiteSpace(action.Description))
            {
                TempData["Error"] = "An action description is required.";
                return RedirectToAction(nameof(Details), new { id });
            }

            action.ManagementReviewId = id;
            action.CreatedAt = DateTime.Now;
            _db.ManagementReviewActions.Add(action);
            await _db.SaveChangesAsync();

            await _audit.LogAsync("AddAction", "ManagementReview", id, $"{review.Reference}: {action.Reference} raised.");
            TempData["Success"] = "Action added.";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> CompleteAction(int id, int actionId)
        {
            if (!Can(ImsPermission.ManageManagementReview)) return Denied();
            var action = await _db.ManagementReviewActions.FirstOrDefaultAsync(a => a.Id == actionId && a.ManagementReviewId == id);
            if (action == null) return NotFound();

            action.Status = ReviewActionStatus.Completed;
            action.CompletedDate = DateTime.Now;
            await _db.SaveChangesAsync();

            await _audit.LogAsync("CompleteAction", "ManagementReview", id, $"{action.Reference} completed.");
            TempData["Success"] = "Action marked complete.";
            return RedirectToAction(nameof(Details), new { id });
        }
    }
}
