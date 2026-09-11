using IT_Service_Management_System.Models.Pm;

namespace IT_Service_Management_System.ViewModels.Pm
{
    /// <summary>
    /// The tasks tab. One query feeds both presentations — the list and the board are the same
    /// tasks arranged differently, so switching view costs nothing and cannot disagree.
    /// </summary>
    public class ProjectTasksVm
    {
        public Project Project { get; set; } = null!;
        public List<ProjectTask> Tasks { get; set; } = new();
        public bool CanContribute { get; set; }

        /// <summary>"list" or "board".</summary>
        public string View { get; set; } = "list";

        public int? MilestoneId { get; set; }
        public int? AssigneeId { get; set; }
        public bool OpenOnly { get; set; }

        public bool IsFiltered => MilestoneId.HasValue || AssigneeId.HasValue || OpenOnly;

        public int Completed => Tasks.Count(t => t.Status == ProjectTaskStatus.Completed);
        public int Blocked => Tasks.Count(t => t.Status == ProjectTaskStatus.Blocked);
        public int Overdue => Tasks.Count(t =>
            t.DueDate.HasValue && t.DueDate.Value.Date < DateTime.Today &&
            t.Status != ProjectTaskStatus.Completed && t.Status != ProjectTaskStatus.Cancelled);

        public IEnumerable<ProjectTask> InColumn(KanbanColumn column) =>
            Tasks.Where(t => t.Column == column);

        /// <summary>Route values that keep the current filter when switching view.</summary>
        public Dictionary<string, string?> RouteValues(string view)
        {
            var values = new Dictionary<string, string?> { ["view"] = view };
            if (MilestoneId.HasValue) values["milestoneId"] = MilestoneId.Value.ToString();
            if (AssigneeId.HasValue) values["assigneeId"] = AssigneeId.Value.ToString();
            if (OpenOnly) values["openOnly"] = "true";
            return values;
        }
    }
}
