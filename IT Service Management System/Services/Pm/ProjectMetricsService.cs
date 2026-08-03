using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Models.Pm;
using IT_Service_Management_System.ViewModels.Pm;
using Microsoft.EntityFrameworkCore;

namespace IT_Service_Management_System.Services.Pm
{
    /// <summary>
    /// Derives every number the project module reports on: progress roll-ups, spend against budget,
    /// resource utilisation, project health, and the executive dashboard aggregate.
    /// </summary>
    public class ProjectMetricsService
    {
        private readonly ApplicationDbContext _db;

        public ProjectMetricsService(ApplicationDbContext db) => _db = db;

        // ── Spend ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Total actual spend on a project: approved expenses, spend booked directly on budget
        /// lines, costed approved time, and paid procurement. Each source is counted once.
        /// </summary>
        public async Task<decimal> ActualSpendAsync(int projectId)
        {
            // Expenses already linked to a budget line are reflected in that line's ActualAmount by
            // the budget controller, so only unlinked approved expenses are added here.
            var expenses = await _db.ProjectExpenses
                .Where(e => e.ProjectId == projectId && e.BudgetLineId == null
                            && (e.Status == ExpenseStatus.Approved || e.Status == ExpenseStatus.Reimbursed))
                .SumAsync(e => (decimal?)e.Amount) ?? 0m;

            var budgetLines = await _db.BudgetLines
                .Where(l => l.ProjectId == projectId)
                .SumAsync(l => (decimal?)l.ActualAmount) ?? 0m;

            var labour = await _db.TimeEntries
                .Where(t => t.ProjectId == projectId && t.Status == TimeEntryStatus.Approved)
                .SumAsync(t => (decimal?)((t.Hours - t.BreakHours) * t.CostRate)) ?? 0m;

            var procurement = await _db.ProcurementRequests
                .Where(p => p.ProjectId == projectId)
                .SumAsync(p => (decimal?)p.PaidAmount) ?? 0m;

            return expenses + budgetLines + labour + procurement;
        }

        /// <summary>Approved but not yet invoiced purchase commitments.</summary>
        public async Task<decimal> CommittedSpendAsync(int projectId) =>
            await _db.ProcurementRequests
                .Where(p => p.ProjectId == projectId
                            && (p.Status == ProcurementStatus.Ordered || p.Status == ProcurementStatus.GoodsReceived))
                .SumAsync(p => (decimal?)(p.OrderedAmount - p.PaidAmount)) ?? 0m;

        // ── Progress ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Roll progress up from the task list, weighted by estimated hours so a 40-hour task counts
        /// for more than a 1-hour one. Falls back to a simple task count when no estimates exist.
        /// </summary>
        public async Task<int> CalculateProgressAsync(int projectId)
        {
            var tasks = await _db.ProjectTasks.AsNoTracking()
                .Where(t => t.ProjectId == projectId && t.Status != ProjectTaskStatus.Cancelled)
                .Select(t => new { t.EstimatedHours, t.PercentComplete, t.Status })
                .ToListAsync();

            if (tasks.Count == 0) return 0;

            var totalWeight = tasks.Sum(t => t.EstimatedHours);
            if (totalWeight <= 0)
                return (int)Math.Round(tasks.Average(t => t.Status == ProjectTaskStatus.Completed ? 100 : t.PercentComplete));

            var earned = tasks.Sum(t =>
                t.EstimatedHours * (t.Status == ProjectTaskStatus.Completed ? 100 : t.PercentComplete));
            return (int)Math.Clamp(Math.Round(earned / totalWeight), 0, 100);
        }

        /// <summary>
        /// Recompute a project's progress and health and persist them. Called after any change that
        /// could move either — a task completing, an expense being approved, a risk being raised.
        /// </summary>
        public async Task RefreshProjectAsync(int projectId, bool save = true)
        {
            var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null) return;

            if (project.AutoCalculateProgress)
                project.ProgressPercent = await CalculateProgressAsync(projectId);

            project.Health = await CalculateHealthAsync(project);

            // A project past its end date with work outstanding is Delayed, not merely Active.
            if (project.Status == ProjectStatus.Active && project.IsOverdue)
                project.Status = ProjectStatus.Delayed;
            else if (project.Status == ProjectStatus.Delayed && !project.IsOverdue)
                project.Status = ProjectStatus.Active;

            project.UpdatedAt = DateTime.Now;
            if (save) await _db.SaveChangesAsync();
        }

        /// <summary>
        /// Traffic-light health. Red on any hard breach (overdue, over budget, critical risk or
        /// issue); amber when trending that way; green otherwise.
        /// </summary>
        public async Task<ProjectHealth> CalculateHealthAsync(Project project)
        {
            var spend = await ActualSpendAsync(project.Id);
            var budget = project.TotalBudget;
            var budgetUsed = budget <= 0 ? 0 : (int)Math.Round(spend / budget * 100);

            var openRisks = await _db.ProjectRisks
                .Where(r => r.ProjectId == project.Id && r.Status != PmRiskStatus.Closed)
                .Select(r => r.Probability * r.Impact)
                .ToListAsync();

            var criticalIssues = await _db.ProjectIssues.CountAsync(i =>
                i.ProjectId == project.Id
                && i.Severity == IssueSeverity.Critical
                && i.Status != IssueStatus.Resolved && i.Status != IssueStatus.Closed);

            var scheduleVariance = project.ProgressPercent - project.SchedulePercentElapsed;

            // ── Red conditions ──
            if (project.IsOverdue) return ProjectHealth.Red;
            if (budget > 0 && budgetUsed > 100) return ProjectHealth.Red;
            if (criticalIssues > 0) return ProjectHealth.Red;
            if (openRisks.Any(s => s >= 20)) return ProjectHealth.Red;
            if (scheduleVariance <= -25) return ProjectHealth.Red;

            // ── Amber conditions ──
            if (budget > 0 && budgetUsed > 90) return ProjectHealth.Amber;
            if (openRisks.Any(s => s >= 15)) return ProjectHealth.Amber;
            if (scheduleVariance <= -10) return ProjectHealth.Amber;
            if (project.Status == ProjectStatus.OnHold) return ProjectHealth.Amber;

            return ProjectHealth.Green;
        }

        // ── Resource utilisation ─────────────────────────────────────────────────

        /// <summary>
        /// Workload per resource over a window: hours committed through assignments against the
        /// resource's capacity for the same period, net of any recorded unavailability.
        /// </summary>
        public async Task<List<ResourceWorkloadRow>> ResourceWorkloadAsync(DateTime from, DateTime to, int take = 15)
        {
            var resources = await _db.Resources.AsNoTracking()
                .Where(r => r.IsActive)
                .Select(r => new { r.Id, r.Name, r.Type, r.WeeklyCapacityHours })
                .ToListAsync();

            var assignments = await _db.ResourceAssignments.AsNoTracking()
                .Where(a => a.FromDate <= to && a.ToDate >= from)
                .Select(a => new { a.ResourceId, a.ProjectId, a.PlannedHours, a.AllocationPercent, a.FromDate, a.ToDate })
                .ToListAsync();

            var unavailable = await _db.ResourceUnavailabilities.AsNoTracking()
                .Where(u => u.FromDate <= to && u.ToDate >= from)
                .Select(u => new { u.ResourceId, u.FromDate, u.ToDate })
                .ToListAsync();

            var weeks = Math.Max(1, (to - from).TotalDays / 7.0);

            var rows = resources.Select(r =>
            {
                var mine = assignments.Where(a => a.ResourceId == r.Id).ToList();

                // Prefer explicit planned hours; otherwise infer from the allocation percentage
                // across the overlapping portion of the window.
                var allocated = mine.Sum(a =>
                {
                    if (a.PlannedHours > 0) return a.PlannedHours;
                    var overlapStart = a.FromDate > from ? a.FromDate : from;
                    var overlapEnd = a.ToDate < to ? a.ToDate : to;
                    var overlapWeeks = Math.Max(0, (overlapEnd - overlapStart).TotalDays) / 7.0;
                    return (decimal)overlapWeeks * r.WeeklyCapacityHours * a.AllocationPercent / 100m;
                });

                var lostDays = unavailable.Where(u => u.ResourceId == r.Id).Sum(u =>
                {
                    var s = u.FromDate > from ? u.FromDate : from;
                    var e = u.ToDate < to ? u.ToDate : to;
                    return Math.Max(0, (e - s).TotalDays);
                });

                var capacity = (decimal)weeks * r.WeeklyCapacityHours
                               - (decimal)(lostDays / 7.0) * r.WeeklyCapacityHours;

                return new ResourceWorkloadRow
                {
                    ResourceId = r.Id,
                    Name = r.Name,
                    Role = r.Type.ToString(),
                    CapacityHours = Math.Max(0, Math.Round(capacity, 1)),
                    AllocatedHours = Math.Round(allocated, 1),
                    ActiveProjects = mine.Select(a => a.ProjectId).Distinct().Count()
                };
            })
            .Where(r => r.AllocatedHours > 0 || r.CapacityHours > 0)
            .OrderByDescending(r => r.UtilisationPercent)
            .Take(take)
            .ToList();

            return rows;
        }

        // ── Executive dashboard ──────────────────────────────────────────────────

        /// <summary>Assemble the whole executive dashboard. One call, one page.</summary>
        public async Task<PmDashboardVm> BuildDashboardAsync()
        {
            var vm = new PmDashboardVm();
            var today = DateTime.Today;
            var monthStart = new DateTime(today.Year, today.Month, 1);

            var projects = await _db.Projects.AsNoTracking()
                .Include(p => p.Department)
                .Include(p => p.ProjectManager)
                .ToListAsync();

            vm.TotalProjects = projects.Count;
            vm.ActiveProjects = projects.Count(p => p.Status == ProjectStatus.Active);
            vm.CompletedProjects = projects.Count(p => p.Status == ProjectStatus.Completed);
            vm.DelayedProjects = projects.Count(p => p.Status == ProjectStatus.Delayed || p.IsOverdue);
            vm.OnHoldProjects = projects.Count(p => p.Status == ProjectStatus.OnHold);
            vm.PlanningProjects = projects.Count(p => p.Status is ProjectStatus.Planning or ProjectStatus.Draft);

            vm.HealthyProjects = projects.Count(p => p.IsOpen && p.Health == ProjectHealth.Green);
            vm.AtRiskProjects = projects.Count(p => p.IsOpen && p.Health == ProjectHealth.Amber);
            vm.UnhealthyProjects = projects.Count(p => p.IsOpen && p.Health == ProjectHealth.Red);

            // ── Money (aggregate once, then slice per project) ──
            vm.TotalBudget = projects.Sum(p => p.TotalBudget);

            var spendByProject = await SpendByProjectAsync();
            vm.TotalSpent = spendByProject.Values.Sum();
            vm.TotalCommitted = await _db.ProcurementRequests
                .Where(p => p.Status == ProcurementStatus.Ordered || p.Status == ProcurementStatus.GoodsReceived)
                .SumAsync(p => (decimal?)(p.OrderedAmount - p.PaidAmount)) ?? 0m;

            // ── Work ──
            vm.OpenTasks = await _db.ProjectTasks.CountAsync(t =>
                t.Status != ProjectTaskStatus.Completed && t.Status != ProjectTaskStatus.Cancelled);
            vm.OverdueTasks = await _db.ProjectTasks.CountAsync(t =>
                t.Status != ProjectTaskStatus.Completed && t.Status != ProjectTaskStatus.Cancelled
                && t.DueDate != null && t.DueDate < today);
            vm.TasksCompletedThisMonth = await _db.ProjectTasks.CountAsync(t =>
                t.Status == ProjectTaskStatus.Completed && t.CompletionDate >= monthStart);

            vm.OpenIssues = await _db.ProjectIssues.CountAsync(i =>
                i.Status != IssueStatus.Resolved && i.Status != IssueStatus.Closed);
            vm.CriticalIssues = await _db.ProjectIssues.CountAsync(i =>
                i.Severity == IssueSeverity.Critical && i.Status != IssueStatus.Resolved && i.Status != IssueStatus.Closed);

            vm.RisksNeedingAttention = await _db.ProjectRisks.CountAsync(r =>
                r.Status != PmRiskStatus.Closed && r.Probability * r.Impact >= 10);

            vm.PendingApprovals = await _db.ProjectApprovals.CountAsync(a => a.Status == ApprovalStatus.Pending);

            vm.OverdueMilestones = await _db.Milestones.CountAsync(m =>
                (m.Status == MilestoneStatus.Planned || m.Status == MilestoneStatus.AtRisk) && m.DueDate < today);

            // ── Resources ──
            vm.TotalResources = await _db.Resources.CountAsync(r => r.IsActive);
            vm.AllocatedResources = await _db.ResourceAssignments
                .Where(a => a.FromDate <= today && a.ToDate >= today)
                .Select(a => a.ResourceId).Distinct().CountAsync();
            vm.HoursLoggedThisMonth = await _db.TimeEntries
                .Where(t => t.WorkDate >= monthStart)
                .SumAsync(t => (decimal?)(t.Hours - t.BreakHours)) ?? 0m;

            vm.ResourceWorkload = await ResourceWorkloadAsync(today, today.AddDays(28));
            vm.ResourceUtilisationPercent = vm.ResourceWorkload.Count == 0
                ? 0
                : (int)Math.Round(vm.ResourceWorkload.Average(r => (double)r.UtilisationPercent));

            // ── Panels ──
            vm.UpcomingDeadlines = projects
                .Where(p => p.IsOpen && p.EndDate != null && p.EndDate >= today && p.EndDate <= today.AddDays(45))
                .OrderBy(p => p.EndDate)
                .Take(8)
                .ToList();

            vm.UpcomingMilestones = await _db.Milestones
                .Include(m => m.Project).Include(m => m.Owner)
                .Where(m => (m.Status == MilestoneStatus.Planned || m.Status == MilestoneStatus.AtRisk)
                            && m.DueDate >= today.AddDays(-14) && m.DueDate <= today.AddDays(60))
                .OrderBy(m => m.DueDate)
                .Take(8)
                .ToListAsync();

            var risks = await _db.ProjectRisks
                .Include(r => r.Project).Include(r => r.Owner)
                .Where(r => r.Status != PmRiskStatus.Closed)
                .ToListAsync();
            vm.TopRisks = risks.OrderByDescending(r => r.Score).Take(8).ToList();

            vm.RecentActivity = await _db.ProjectActivityLogs
                .Include(l => l.User).Include(l => l.Project)
                .OrderByDescending(l => l.At)
                .Take(12)
                .ToListAsync();

            // ── Health board ──
            var openIds = projects.Where(p => p.IsOpen).Select(p => p.Id).ToList();
            var riskCounts = await _db.ProjectRisks
                .Where(r => openIds.Contains(r.ProjectId) && r.Status != PmRiskStatus.Closed)
                .GroupBy(r => r.ProjectId)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count);
            var issueCounts = await _db.ProjectIssues
                .Where(i => openIds.Contains(i.ProjectId) && i.Status != IssueStatus.Resolved && i.Status != IssueStatus.Closed)
                .GroupBy(i => i.ProjectId)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count);

            vm.HealthBoard = projects
                .Where(p => p.IsOpen)
                .OrderBy(p => p.Health == ProjectHealth.Red ? 0 : p.Health == ProjectHealth.Amber ? 1 : 2)
                .ThenBy(p => p.EndDate ?? DateTime.MaxValue)
                .Take(10)
                .Select(p => new ProjectHealthRow
                {
                    ProjectId = p.Id,
                    Reference = p.Reference,
                    Name = p.Name,
                    Manager = p.ProjectManager == null ? null : $"{p.ProjectManager.FirstName} {p.ProjectManager.LastName}",
                    Status = p.Status,
                    Health = p.Health,
                    ProgressPercent = p.ProgressPercent,
                    SchedulePercentElapsed = p.SchedulePercentElapsed,
                    BudgetUsedPercent = p.TotalBudget <= 0 ? 0
                        : (int)Math.Round(spendByProject.GetValueOrDefault(p.Id) / p.TotalBudget * 100),
                    OpenRisks = riskCounts.GetValueOrDefault(p.Id),
                    OpenIssues = issueCounts.GetValueOrDefault(p.Id),
                    EndDate = p.EndDate
                })
                .ToList();

            // ── Charts ──
            vm.StatusBreakdown = projects
                .GroupBy(p => p.Status.ToString())
                .ToDictionary(g => g.Key, g => g.Count());

            vm.DepartmentBreakdown = projects
                .GroupBy(p => p.Department?.Name ?? "Unassigned")
                .OrderByDescending(g => g.Count())
                .Take(8)
                .ToDictionary(g => g.Key, g => g.Count());

            var twelveMonthsAgo = monthStart.AddMonths(-11);
            var completions = await _db.ProjectTasks
                .Where(t => t.Status == ProjectTaskStatus.Completed && t.CompletionDate >= twelveMonthsAgo)
                .Select(t => t.CompletionDate!.Value)
                .ToListAsync();
            vm.TasksCompletedByMonth = Enumerable.Range(0, 12)
                .Select(i => twelveMonthsAgo.AddMonths(i))
                .Select(m => new MonthlyPoint
                {
                    Label = m.ToString("MMM yy"),
                    Value = completions.Count(c => c.Year == m.Year && c.Month == m.Month)
                })
                .ToList();

            vm.BudgetComparison = projects
                .Where(p => p.IsOpen && p.TotalBudget > 0)
                .OrderByDescending(p => p.TotalBudget)
                .Take(8)
                .Select(p => new BudgetComparisonRow
                {
                    Name = p.Name.Length > 24 ? p.Name[..24] + "…" : p.Name,
                    Budget = p.TotalBudget,
                    Spent = spendByProject.GetValueOrDefault(p.Id)
                })
                .ToList();

            vm.Timeline = projects
                .Where(p => p.IsOpen && p.StartDate != null && p.EndDate != null)
                .OrderBy(p => p.StartDate)
                .Take(12)
                .Select(p => new TimelineRow
                {
                    ProjectId = p.Id,
                    Name = p.Name,
                    Start = p.StartDate!.Value,
                    End = p.EndDate!.Value,
                    Status = p.Status,
                    Health = p.Health,
                    ProgressPercent = p.ProgressPercent
                })
                .ToList();

            return vm;
        }

        /// <summary>
        /// Actual spend for every project in one round trip per source, rather than N per project.
        /// </summary>
        public async Task<Dictionary<int, decimal>> SpendByProjectAsync()
        {
            var result = new Dictionary<int, decimal>();

            void Accumulate(IEnumerable<(int ProjectId, decimal Amount)> rows)
            {
                foreach (var (projectId, amount) in rows)
                    result[projectId] = result.GetValueOrDefault(projectId) + amount;
            }

            Accumulate((await _db.ProjectExpenses
                .Where(e => e.BudgetLineId == null
                            && (e.Status == ExpenseStatus.Approved || e.Status == ExpenseStatus.Reimbursed))
                .GroupBy(e => e.ProjectId)
                .Select(g => new { g.Key, Total = g.Sum(x => x.Amount) })
                .ToListAsync()).Select(x => (x.Key, x.Total)));

            Accumulate((await _db.BudgetLines
                .GroupBy(l => l.ProjectId)
                .Select(g => new { g.Key, Total = g.Sum(x => x.ActualAmount) })
                .ToListAsync()).Select(x => (x.Key, x.Total)));

            Accumulate((await _db.TimeEntries
                .Where(t => t.Status == TimeEntryStatus.Approved)
                .GroupBy(t => t.ProjectId)
                .Select(g => new { g.Key, Total = g.Sum(x => (x.Hours - x.BreakHours) * x.CostRate) })
                .ToListAsync()).Select(x => (x.Key, x.Total)));

            Accumulate((await _db.ProcurementRequests
                .GroupBy(p => p.ProjectId)
                .Select(g => new { g.Key, Total = g.Sum(x => x.PaidAmount) })
                .ToListAsync()).Select(x => (x.Key, x.Total)));

            return result;
        }
    }
}
