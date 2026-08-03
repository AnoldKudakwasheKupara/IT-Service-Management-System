using IT_Service_Management_System.Models.Pm;

namespace IT_Service_Management_System.ViewModels.Pm
{
    /// <summary>Everything the executive project dashboard renders, assembled in one pass.</summary>
    public class PmDashboardVm
    {
        // ── Headline counts ──────────────────────────────────────────────────────
        public int TotalProjects { get; set; }
        public int ActiveProjects { get; set; }
        public int CompletedProjects { get; set; }
        public int DelayedProjects { get; set; }
        public int OnHoldProjects { get; set; }
        public int PlanningProjects { get; set; }

        // ── Money ────────────────────────────────────────────────────────────────
        public decimal TotalBudget { get; set; }
        public decimal TotalSpent { get; set; }
        public decimal TotalCommitted { get; set; }
        public decimal BudgetRemaining => TotalBudget - TotalSpent;
        public int BudgetUtilisationPercent =>
            TotalBudget <= 0 ? 0 : (int)Math.Round(TotalSpent / TotalBudget * 100);

        // ── Resources ────────────────────────────────────────────────────────────
        public int TotalResources { get; set; }
        public int AllocatedResources { get; set; }
        public int ResourceUtilisationPercent { get; set; }
        public decimal HoursLoggedThisMonth { get; set; }

        // ── Work & governance ────────────────────────────────────────────────────
        public int OpenTasks { get; set; }
        public int OverdueTasks { get; set; }
        public int TasksCompletedThisMonth { get; set; }
        public int RisksNeedingAttention { get; set; }
        public int OpenIssues { get; set; }
        public int CriticalIssues { get; set; }
        public int PendingApprovals { get; set; }
        public int OverdueMilestones { get; set; }

        // ── Health roll-up ───────────────────────────────────────────────────────
        public int HealthyProjects { get; set; }
        public int AtRiskProjects { get; set; }
        public int UnhealthyProjects { get; set; }

        // ── Panels ───────────────────────────────────────────────────────────────
        public List<Project> UpcomingDeadlines { get; set; } = new();
        public List<Milestone> UpcomingMilestones { get; set; } = new();
        public List<ProjectRisk> TopRisks { get; set; } = new();
        public List<ProjectActivityLog> RecentActivity { get; set; } = new();
        public List<ProjectHealthRow> HealthBoard { get; set; } = new();

        // ── Chart series ─────────────────────────────────────────────────────────
        /// <summary>Project count per status, for the status pie chart.</summary>
        public Dictionary<string, int> StatusBreakdown { get; set; } = new();

        /// <summary>Project count per department, for the department bar chart.</summary>
        public Dictionary<string, int> DepartmentBreakdown { get; set; } = new();

        /// <summary>Tasks completed per month over the last twelve months.</summary>
        public List<MonthlyPoint> TasksCompletedByMonth { get; set; } = new();

        /// <summary>Budget vs actual per project, capped to the largest few for readability.</summary>
        public List<BudgetComparisonRow> BudgetComparison { get; set; } = new();

        /// <summary>Per-resource workload for the workload chart.</summary>
        public List<ResourceWorkloadRow> ResourceWorkload { get; set; } = new();

        /// <summary>Active projects positioned on a timeline, for the portfolio Gantt strip.</summary>
        public List<TimelineRow> Timeline { get; set; } = new();
    }

    /// <summary>One project's health line on the dashboard board.</summary>
    public class ProjectHealthRow
    {
        public int ProjectId { get; set; }
        public string Reference { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Manager { get; set; }
        public ProjectStatus Status { get; set; }
        public ProjectHealth Health { get; set; }
        public int ProgressPercent { get; set; }
        public int SchedulePercentElapsed { get; set; }
        public int BudgetUsedPercent { get; set; }
        public int OpenRisks { get; set; }
        public int OpenIssues { get; set; }
        public DateTime? EndDate { get; set; }

        /// <summary>Progress minus elapsed schedule — negative means the project is behind plan.</summary>
        public int ScheduleVariancePercent => ProgressPercent - SchedulePercentElapsed;
    }

    public class MonthlyPoint
    {
        public string Label { get; set; } = string.Empty;
        public int Value { get; set; }
    }

    public class BudgetComparisonRow
    {
        public string Name { get; set; } = string.Empty;
        public decimal Budget { get; set; }
        public decimal Spent { get; set; }
        public decimal Remaining => Math.Max(0, Budget - Spent);
        public bool IsOverBudget => Spent > Budget;
    }

    public class ResourceWorkloadRow
    {
        public int ResourceId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Role { get; set; }
        public decimal CapacityHours { get; set; }
        public decimal AllocatedHours { get; set; }
        public int UtilisationPercent =>
            CapacityHours <= 0 ? 0 : (int)Math.Round(AllocatedHours / CapacityHours * 100);
        public bool IsOverallocated => UtilisationPercent > 100;
        public int ActiveProjects { get; set; }
    }

    public class TimelineRow
    {
        public int ProjectId { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public ProjectStatus Status { get; set; }
        public ProjectHealth Health { get; set; }
        public int ProgressPercent { get; set; }
    }
}
