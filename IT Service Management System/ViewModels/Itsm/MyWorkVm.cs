namespace IT_Service_Management_System.ViewModels.Itsm
{
    public class WorkItemVm
    {
        public string Kind { get; set; } = string.Empty;
        public string Reference { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public DateTime? DueAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public int PriorityScore { get; set; }
        public bool RequiresDecision { get; set; }
        public bool IsSlaRisk { get; set; }

        public bool IsOverdue(DateTime now) => DueAt.HasValue && DueAt.Value < now;
        public bool IsDueToday(DateTime now) => DueAt.HasValue && DueAt.Value.Date == now.Date;
    }

    public class MyWorkVm
    {
        public List<WorkItemVm> Items { get; set; } = new();
        public string? Kind { get; set; }
        public string? Bucket { get; set; }
        public string? Query { get; set; }
        public int Total { get; set; }
        public int Overdue { get; set; }
        public int DueToday { get; set; }
        public int Approvals { get; set; }
        public int SlaRisk { get; set; }
        public List<string> Kinds { get; set; } = new();
    }

    public static class MyWorkOrdering
    {
        public static IEnumerable<WorkItemVm> Prioritize(IEnumerable<WorkItemVm> items, DateTime now) =>
            items.OrderByDescending(i => i.IsOverdue(now))
                .ThenByDescending(i => i.RequiresDecision)
                .ThenByDescending(i => i.IsSlaRisk)
                .ThenByDescending(i => i.PriorityScore)
                .ThenBy(i => i.DueAt ?? DateTime.MaxValue)
                .ThenByDescending(i => i.CreatedAt);
    }
}
