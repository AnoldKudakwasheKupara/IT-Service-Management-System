using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Helpers;
using IT_Service_Management_System.Models;
using IT_Service_Management_System.Models.Pm;
using Microsoft.EntityFrameworkCore;

namespace IT_Service_Management_System.Services.Pm
{
    /// <summary>
    /// Routes multi-level approvals for projects, budgets, expenses, changes, documents, purchases,
    /// milestones and timesheets. A chain is a set of <see cref="ProjectApproval"/> rows sharing a
    /// subject; level N only becomes actionable once level N-1 has been approved.
    /// </summary>
    public class ProjectApprovalService
    {
        private readonly ApplicationDbContext _db;
        private readonly ProjectActivityService _activity;

        /// <summary>Spend at or above this figure picks up an extra executive approval step.</summary>
        public const decimal ExecutiveThreshold = 10_000m;

        public ProjectApprovalService(ApplicationDbContext db, ProjectActivityService activity)
        {
            _db = db; _activity = activity;
        }

        /// <summary>
        /// Open an approval chain. Approvers are worked out from the subject and the amount at
        /// stake; duplicates and the requester are dropped so nobody approves their own request.
        /// Returns the number of steps created (0 when no approver could be found).
        /// </summary>
        public async Task<int> RequestAsync(ApprovalSubject subject, int subjectId, string subjectTitle,
                                            int? projectId, int requestedById, decimal? amount = null)
        {
            // Re-requesting supersedes any chain still pending on the same subject.
            var existing = await _db.ProjectApprovals
                .Where(a => a.Subject == subject && a.SubjectId == subjectId && a.Status == ApprovalStatus.Pending)
                .ToListAsync();
            foreach (var stale in existing) stale.Status = ApprovalStatus.Cancelled;

            var approvers = await ResolveApproversAsync(subject, projectId, amount, requestedById);
            if (approvers.Count == 0) return 0;

            var level = 1;
            foreach (var approverId in approvers)
            {
                _db.ProjectApprovals.Add(new ProjectApproval
                {
                    Subject = subject,
                    SubjectId = subjectId,
                    SubjectTitle = subjectTitle.Length > 250 ? subjectTitle[..250] : subjectTitle,
                    ProjectId = projectId,
                    Level = level,
                    ApproverId = approverId,
                    RequestedById = requestedById,
                    Amount = amount,
                    Status = ApprovalStatus.Pending,
                    RequestedAt = DateTime.Now
                });
                level++;
            }
            await _db.SaveChangesAsync();

            // Only the first level is actionable now, so only that approver is told.
            _activity.Notify(approvers[0], PmNotificationType.ApprovalPending,
                $"{Describe(subject)} awaiting your approval",
                subjectTitle,
                "/ProjectApprovals",
                projectId);
            await _db.SaveChangesAsync();

            return approvers.Count;
        }

        /// <summary>
        /// Work out who signs off, in order. The project manager reviews delivery matters first,
        /// finance reviews money, and anything at or above <see cref="ExecutiveThreshold"/> — plus
        /// project approval and closure — needs an executive at the end of the chain.
        /// </summary>
        private async Task<List<int>> ResolveApproversAsync(ApprovalSubject subject, int? projectId, decimal? amount, int requestedById)
        {
            var chain = new List<int>();

            var project = projectId.HasValue
                ? await _db.Projects.AsNoTracking()
                    .Where(p => p.Id == projectId)
                    .Select(p => new { p.ProjectManagerId, p.SponsorId })
                    .FirstOrDefaultAsync()
                : null;

            // Step 1 — the project manager, for anything raised inside a project.
            if (project?.ProjectManagerId is int pm) chain.Add(pm);

            // Step 2 — finance, for anything with money attached.
            var isSpend = subject is ApprovalSubject.Budget or ApprovalSubject.Expense
                or ApprovalSubject.Purchase or ApprovalSubject.ChangeRequest;
            if (isSpend)
            {
                var finance = await FirstActiveUserInRoleAsync(Ticket.UserRole.Finance);
                if (finance.HasValue) chain.Add(finance.Value);
            }

            // Step 3 — an executive, for high-value spend and for project-level decisions.
            var needsExecutive =
                subject is ApprovalSubject.Project or ApprovalSubject.Closure
                || (amount.HasValue && amount.Value >= ExecutiveThreshold);
            if (needsExecutive)
            {
                if (project?.SponsorId is int sponsor) chain.Add(sponsor);
                else
                {
                    var exec = await FirstActiveUserInRoleAsync(Ticket.UserRole.GeneralManager)
                               ?? await FirstActiveUserInRoleAsync(Ticket.UserRole.Admin);
                    if (exec.HasValue) chain.Add(exec.Value);
                }
            }

            // Nobody approves their own request, and one person never appears twice in a chain.
            chain = chain.Distinct().Where(id => id != requestedById).ToList();

            // Last resort so a request is never silently unroutable.
            if (chain.Count == 0)
            {
                var admin = await FirstActiveUserInRoleAsync(Ticket.UserRole.Admin);
                if (admin.HasValue && admin.Value != requestedById) chain.Add(admin.Value);
            }

            return chain;
        }

        private async Task<int?> FirstActiveUserInRoleAsync(Ticket.UserRole role) =>
            await _db.Users.AsNoTracking()
                .Where(u => u.IsActive && u.Role == role)
                .OrderBy(u => u.Id)
                .Select(u => (int?)u.Id)
                .FirstOrDefaultAsync();

        /// <summary>
        /// Record a decision on one step. Approving the last outstanding step returns true, which
        /// tells the caller to apply the decision to the underlying record.
        /// </summary>
        public async Task<(bool ChainComplete, bool Rejected)> DecideAsync(int approvalId, int actingUserId, bool approve, string? comment)
        {
            var step = await _db.ProjectApprovals.FirstOrDefaultAsync(a => a.Id == approvalId);
            if (step == null || step.Status != ApprovalStatus.Pending) return (false, false);

            // Only the named approver (or their delegate) may decide.
            if (step.ApproverId != actingUserId && step.DelegatedToId != actingUserId) return (false, false);

            // Earlier levels must have cleared first.
            var blockedByEarlierLevel = await _db.ProjectApprovals.AnyAsync(a =>
                a.Subject == step.Subject && a.SubjectId == step.SubjectId
                && a.Level < step.Level && a.Status == ApprovalStatus.Pending);
            if (blockedByEarlierLevel) return (false, false);

            step.Status = approve ? ApprovalStatus.Approved : ApprovalStatus.Rejected;
            step.Comment = comment;
            step.DecidedAt = DateTime.Now;

            _activity.Log(step.ProjectId, nameof(ProjectApproval), step.Id,
                approve ? "Approved" : "Rejected",
                $"{Describe(step.Subject)} · {step.SubjectTitle}");

            if (!approve)
            {
                // A rejection ends the chain — later levels never see it.
                var remaining = await _db.ProjectApprovals
                    .Where(a => a.Subject == step.Subject && a.SubjectId == step.SubjectId
                                && a.Level > step.Level && a.Status == ApprovalStatus.Pending)
                    .ToListAsync();
                foreach (var r in remaining) r.Status = ApprovalStatus.Cancelled;

                _activity.Notify(step.RequestedById, PmNotificationType.ApprovalDecided,
                    $"{Describe(step.Subject)} rejected", comment, "/ProjectApprovals", step.ProjectId);
                await _db.SaveChangesAsync();
                return (false, true);
            }

            var next = await _db.ProjectApprovals
                .Where(a => a.Subject == step.Subject && a.SubjectId == step.SubjectId
                            && a.Level > step.Level && a.Status == ApprovalStatus.Pending)
                .OrderBy(a => a.Level)
                .FirstOrDefaultAsync();

            if (next != null)
            {
                _activity.Notify(next.ApproverId, PmNotificationType.ApprovalPending,
                    $"{Describe(next.Subject)} awaiting your approval", next.SubjectTitle,
                    "/ProjectApprovals", next.ProjectId);
                await _db.SaveChangesAsync();
                return (false, false);
            }

            _activity.Notify(step.RequestedById, PmNotificationType.ApprovalDecided,
                $"{Describe(step.Subject)} approved", comment, "/ProjectApprovals", step.ProjectId);
            await _db.SaveChangesAsync();
            return (true, false);
        }

        /// <summary>Hand a pending step to a colleague without deciding it.</summary>
        public async Task<bool> DelegateAsync(int approvalId, int actingUserId, int delegateToId)
        {
            var step = await _db.ProjectApprovals.FirstOrDefaultAsync(a => a.Id == approvalId);
            if (step == null || step.Status != ApprovalStatus.Pending || step.ApproverId != actingUserId) return false;

            step.DelegatedToId = delegateToId;
            _activity.Notify(delegateToId, PmNotificationType.ApprovalPending,
                $"{Describe(step.Subject)} delegated to you", step.SubjectTitle, "/ProjectApprovals", step.ProjectId);
            await _db.SaveChangesAsync();
            return true;
        }

        /// <summary>The steps this user can act on right now — pending, theirs, and not blocked upstream.</summary>
        public async Task<List<ProjectApproval>> PendingForUserAsync(int userId)
        {
            var mine = await _db.ProjectApprovals
                .Include(a => a.Project).Include(a => a.RequestedBy)
                .Where(a => a.Status == ApprovalStatus.Pending
                            && (a.ApproverId == userId || a.DelegatedToId == userId))
                .OrderBy(a => a.RequestedAt)
                .ToListAsync();
            if (mine.Count == 0) return mine;

            // Drop any step still waiting on a lower level.
            var subjects = mine.Select(a => new { a.Subject, a.SubjectId }).ToList();
            var blocking = await _db.ProjectApprovals
                .Where(a => a.Status == ApprovalStatus.Pending)
                .Select(a => new { a.Subject, a.SubjectId, a.Level })
                .ToListAsync();

            return mine.Where(step => !blocking.Any(b =>
                b.Subject == step.Subject && b.SubjectId == step.SubjectId && b.Level < step.Level)).ToList();
        }

        /// <summary>True when every step for a subject has been approved.</summary>
        public async Task<bool> IsFullyApprovedAsync(ApprovalSubject subject, int subjectId)
        {
            var steps = await _db.ProjectApprovals
                .Where(a => a.Subject == subject && a.SubjectId == subjectId && a.Status != ApprovalStatus.Cancelled)
                .Select(a => a.Status)
                .ToListAsync();
            return steps.Count > 0 && steps.All(s => s == ApprovalStatus.Approved);
        }

        private static string Describe(ApprovalSubject subject) => subject switch
        {
            ApprovalSubject.Project => "Project",
            ApprovalSubject.Budget => "Budget",
            ApprovalSubject.Expense => "Expense claim",
            ApprovalSubject.ChangeRequest => "Change request",
            ApprovalSubject.Document => "Document",
            ApprovalSubject.Purchase => "Purchase request",
            ApprovalSubject.Milestone => "Milestone",
            ApprovalSubject.Closure => "Project closure",
            ApprovalSubject.Timesheet => "Timesheet",
            _ => subject.ToString()
        };
    }
}
