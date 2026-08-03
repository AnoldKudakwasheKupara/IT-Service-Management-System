using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Filters;
using IT_Service_Management_System.Helpers;
using IT_Service_Management_System.Models.Pm;
using IT_Service_Management_System.Services.Pm;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IT_Service_Management_System.Controllers
{
    /// <summary>
    /// The approval inbox and the project notification centre. Deciding an approval here applies
    /// the outcome to the underlying record — an approved expense becomes payable, an approved
    /// project moves out of planning, an approved purchase can be ordered.
    /// </summary>
    [RoleAuthorize("Admin", "SystemsAdmin", "ProjectManager", "GeneralManager", "TeamLead",
                   "DepartmentManager", "Finance", "Procurement", "Auditor", "Employee", "HR")]
    public class ProjectApprovalsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly ProjectApprovalService _approvals;
        private readonly ProjectActivityService _activity;
        private readonly ProjectMetricsService _metrics;

        public ProjectApprovalsController(ApplicationDbContext db, ProjectApprovalService approvals,
            ProjectActivityService activity, ProjectMetricsService metrics)
        {
            _db = db; _approvals = approvals; _activity = activity; _metrics = metrics;
        }

        private int Uid => HttpContext.Session.GetInt32("UserId") ?? 0;
        private string? Role => HttpContext.Session.GetString("UserRole");

        // ════════════════════════════════════════════════════════════════════════
        //  Inbox
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>Everything waiting on this user's decision, plus what they have already decided.</summary>
        public async Task<IActionResult> Index(ApprovalSubject? subject)
        {
            var pending = await _approvals.PendingForUserAsync(Uid);
            if (subject.HasValue) pending = pending.Where(a => a.Subject == subject.Value).ToList();

            ViewBag.Subject = subject;
            ViewBag.PendingValue = pending.Sum(a => a.Amount ?? 0);

            ViewBag.Decided = await _db.ProjectApprovals.AsNoTracking()
                .Include(a => a.Project).Include(a => a.RequestedBy)
                .Where(a => (a.ApproverId == Uid || a.DelegatedToId == Uid) && a.Status != ApprovalStatus.Pending)
                .OrderByDescending(a => a.DecidedAt)
                .Take(25)
                .ToListAsync();

            // What this user has sent for approval, so they can chase it.
            ViewBag.Raised = await _db.ProjectApprovals.AsNoTracking()
                .Include(a => a.Project).Include(a => a.Approver)
                .Where(a => a.RequestedById == Uid && a.Status == ApprovalStatus.Pending)
                .OrderBy(a => a.RequestedAt)
                .ToListAsync();

            ViewBag.Colleagues = await _db.Users.AsNoTracking()
                .Where(u => u.IsActive && u.Id != Uid).OrderBy(u => u.FirstName)
                .Select(u => new { u.Id, Name = u.FirstName + " " + u.LastName }).ToListAsync();

            return View(pending);
        }

        /// <summary>Every approval across the portfolio — an oversight view for administrators.</summary>
        [RoleAuthorize("Admin", "SystemsAdmin", "GeneralManager", "Finance", "Auditor")]
        public async Task<IActionResult> All(ApprovalStatus? status, ApprovalSubject? subject)
        {
            IQueryable<ProjectApproval> query = _db.ProjectApprovals.AsNoTracking()
                .Include(a => a.Project).Include(a => a.Approver).Include(a => a.RequestedBy);

            if (status.HasValue) query = query.Where(a => a.Status == status.Value);
            if (subject.HasValue) query = query.Where(a => a.Subject == subject.Value);

            ViewBag.Status = status; ViewBag.Subject = subject;
            return View(await query.OrderByDescending(a => a.RequestedAt).Take(300).ToListAsync());
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Decisions
        // ════════════════════════════════════════════════════════════════════════

        [HttpPost]
        public async Task<IActionResult> Decide(int id, bool approve, string? comment)
        {
            var step = await _db.ProjectApprovals.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id);
            if (step == null) return NotFound();

            // A rejection must say why — otherwise the requester has nothing to act on.
            if (!approve && string.IsNullOrWhiteSpace(comment))
            {
                TempData["Error"] = "Give a reason when rejecting.";
                return RedirectToAction(nameof(Index));
            }

            var (chainComplete, rejected) = await _approvals.DecideAsync(id, Uid, approve, comment);

            if (!chainComplete && !rejected && step.Status == ApprovalStatus.Pending)
            {
                // DecideAsync returns false/false either because the step advanced to the next
                // level, or because this user was not entitled to decide it.
                var stillPending = await _db.ProjectApprovals.AnyAsync(a => a.Id == id && a.Status == ApprovalStatus.Pending);
                if (stillPending)
                {
                    TempData["Error"] = "You cannot decide that approval — it is not yours, or an earlier level is still outstanding.";
                    return RedirectToAction(nameof(Index));
                }
            }

            if (rejected) await ApplyRejectionAsync(step);
            else if (chainComplete) await ApplyApprovalAsync(step);

            TempData["Success"] = approve
                ? chainComplete ? "Approved — the decision has been applied." : "Approved and passed to the next level."
                : "Rejected.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> Delegate(int id, int delegateToId)
        {
            var ok = await _approvals.DelegateAsync(id, Uid, delegateToId);
            TempData[ok ? "Success" : "Error"] = ok
                ? "Approval delegated."
                : "That approval could not be delegated — it may already have been decided.";
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// Push the fully-approved decision into the record it belongs to. Each subject knows what
        /// "approved" means for it.
        /// </summary>
        private async Task ApplyApprovalAsync(ProjectApproval step)
        {
            switch (step.Subject)
            {
                case ApprovalSubject.Project:
                    var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == step.SubjectId);
                    if (project != null)
                    {
                        project.Status = ProjectStatus.Approved;
                        project.ApprovedAt = DateTime.Now;
                        project.ApprovedById = Uid;
                        project.BaselineEndDate = project.EndDate;
                    }
                    break;

                case ApprovalSubject.Expense:
                    var expense = await _db.ProjectExpenses.FirstOrDefaultAsync(e => e.Id == step.SubjectId);
                    if (expense != null)
                    {
                        expense.Status = ExpenseStatus.Approved;
                        expense.ApprovedById = Uid;
                        expense.ApprovedAt = DateTime.Now;
                        expense.DecisionNote = step.Comment;

                        // Approved spend lands on its budget line straight away.
                        if (expense.BudgetLineId is int lineId)
                        {
                            var line = await _db.BudgetLines.FirstOrDefaultAsync(l => l.Id == lineId);
                            if (line != null) line.ActualAmount += expense.Amount;
                        }
                    }
                    break;

                case ApprovalSubject.ChangeRequest:
                    var change = await _db.ProjectChangeRequests.FirstOrDefaultAsync(c => c.Id == step.SubjectId);
                    if (change != null)
                    {
                        change.Status = ChangeRequestStatus.Approved;
                        change.ApprovedById = Uid;
                        change.DecidedAt = DateTime.Now;
                        change.DecisionNote = step.Comment;
                    }
                    break;

                case ApprovalSubject.Purchase:
                    var purchase = await _db.ProcurementRequests.FirstOrDefaultAsync(p => p.Id == step.SubjectId);
                    if (purchase != null)
                    {
                        purchase.Status = ProcurementStatus.Approved;
                        purchase.ApprovedById = Uid;
                        purchase.ApprovedAt = DateTime.Now;
                    }
                    break;

                case ApprovalSubject.Document:
                    var document = await _db.ProjectDocuments.FirstOrDefaultAsync(d => d.Id == step.SubjectId);
                    if (document != null)
                    {
                        document.Status = ProjectDocumentStatus.Approved;
                        document.ApprovedById = Uid;
                        document.ApprovedAt = DateTime.Now;
                    }
                    break;

                case ApprovalSubject.Milestone:
                    var milestone = await _db.Milestones.FirstOrDefaultAsync(m => m.Id == step.SubjectId);
                    if (milestone != null)
                    {
                        milestone.ClientApproved = true;
                        milestone.ClientApprovedAt = DateTime.Now;
                        milestone.Status = MilestoneStatus.Achieved;
                        milestone.AchievedDate ??= DateTime.Today;
                    }
                    break;

                case ApprovalSubject.Closure:
                    var closure = await _db.ProjectClosures.FirstOrDefaultAsync(c => c.ProjectId == step.SubjectId);
                    if (closure != null) closure.Status = ClosureStatus.Accepted;
                    break;

                case ApprovalSubject.Budget:
                    // The budget lines are already recorded; approval simply signs them off.
                    break;
            }

            await _db.SaveChangesAsync();
            if (step.ProjectId is int projectId) await _metrics.RefreshProjectAsync(projectId);
        }

        /// <summary>Return the underlying record to a state the requester can act on again.</summary>
        private async Task ApplyRejectionAsync(ProjectApproval step)
        {
            switch (step.Subject)
            {
                case ApprovalSubject.Expense:
                    var expense = await _db.ProjectExpenses.FirstOrDefaultAsync(e => e.Id == step.SubjectId);
                    if (expense != null)
                    {
                        expense.Status = ExpenseStatus.Rejected;
                        expense.DecisionNote = step.Comment;
                    }
                    break;

                case ApprovalSubject.ChangeRequest:
                    var change = await _db.ProjectChangeRequests.FirstOrDefaultAsync(c => c.Id == step.SubjectId);
                    if (change != null)
                    {
                        change.Status = ChangeRequestStatus.Rejected;
                        change.DecidedAt = DateTime.Now;
                        change.DecisionNote = step.Comment;
                    }
                    break;

                case ApprovalSubject.Purchase:
                    var purchase = await _db.ProcurementRequests.FirstOrDefaultAsync(p => p.Id == step.SubjectId);
                    if (purchase != null) purchase.Status = ProcurementStatus.Rejected;
                    break;

                case ApprovalSubject.Document:
                    var document = await _db.ProjectDocuments.FirstOrDefaultAsync(d => d.Id == step.SubjectId);
                    if (document != null) document.Status = ProjectDocumentStatus.Rejected;
                    break;

                case ApprovalSubject.Project:
                    var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == step.SubjectId);
                    // Back to draft so the manager can revise the case and resubmit.
                    if (project != null) project.Status = ProjectStatus.Draft;
                    break;
            }

            await _db.SaveChangesAsync();
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Notification centre
        // ════════════════════════════════════════════════════════════════════════

        public async Task<IActionResult> Notifications(bool unreadOnly = false)
        {
            IQueryable<PmNotification> query = _db.PmNotifications.AsNoTracking()
                .Include(n => n.Project)
                .Where(n => n.UserId == Uid);
            if (unreadOnly) query = query.Where(n => !n.IsRead);

            ViewBag.UnreadOnly = unreadOnly;
            ViewBag.UnreadCount = await _db.PmNotifications.CountAsync(n => n.UserId == Uid && !n.IsRead);

            return View(await query.OrderByDescending(n => n.CreatedAt).Take(150).ToListAsync());
        }

        [HttpPost]
        public async Task<IActionResult> MarkRead(int id, string? url)
        {
            var notification = await _db.PmNotifications.FirstOrDefaultAsync(n => n.Id == id && n.UserId == Uid);
            if (notification != null && !notification.IsRead)
            {
                notification.IsRead = true;
                notification.ReadAt = DateTime.Now;
                await _db.SaveChangesAsync();
            }

            // Only follow the stored link when it is local — never trust it as an open redirect.
            return !string.IsNullOrWhiteSpace(url) && Url.IsLocalUrl(url)
                ? Redirect(url)
                : RedirectToAction(nameof(Notifications));
        }

        [HttpPost]
        public async Task<IActionResult> MarkAllRead()
        {
            var unread = await _db.PmNotifications.Where(n => n.UserId == Uid && !n.IsRead).ToListAsync();
            foreach (var notification in unread)
            {
                notification.IsRead = true;
                notification.ReadAt = DateTime.Now;
            }
            await _db.SaveChangesAsync();

            TempData["Success"] = $"{unread.Count} notification(s) marked read.";
            return RedirectToAction(nameof(Notifications));
        }
    }
}
