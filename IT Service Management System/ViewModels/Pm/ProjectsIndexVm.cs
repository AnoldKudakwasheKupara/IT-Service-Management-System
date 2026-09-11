using IT_Service_Management_System.Models.Pm;

namespace IT_Service_Management_System.ViewModels.Pm
{
    /// <summary>
    /// The project register: the page of projects, the filter that produced them, and the counts
    /// across the whole visible portfolio.
    /// </summary>
    public class ProjectsIndexVm
    {
        public List<Project> Projects { get; set; } = new();
        public ProjectFilter Filter { get; set; } = new();

        /// <summary>
        /// Counts span everything the user may see, not the current page or filter — they are the
        /// portfolio's shape, and a reader comparing "8 overdue" against a filtered list needs the
        /// denominator to stay still while they narrow it.
        /// </summary>
        public int TotalCount { get; set; }
        public int OpenCount { get; set; }
        public int OverdueCount { get; set; }
        public int AtRiskCount { get; set; }

        public List<string> Managers { get; set; } = new();
    }

    /// <summary>Query-string filter for the project register, shared by the list and its links.</summary>
    public class ProjectFilter
    {
        public string? Q { get; set; }
        public ProjectStatus? Status { get; set; }
        public ProjectCategory? Category { get; set; }
        public ProjectHealth? Health { get; set; }
        public string? Manager { get; set; }
        public bool Overdue { get; set; }

        public bool IsActive =>
            !string.IsNullOrWhiteSpace(Q) || Status.HasValue || Category.HasValue ||
            Health.HasValue || !string.IsNullOrWhiteSpace(Manager) || Overdue;

        public Dictionary<string, string?> RouteValues()
        {
            var values = new Dictionary<string, string?>();
            if (!string.IsNullOrWhiteSpace(Q)) values["q"] = Q;
            if (Status.HasValue) values["status"] = Status.Value.ToString();
            if (Category.HasValue) values["category"] = Category.Value.ToString();
            if (Health.HasValue) values["health"] = Health.Value.ToString();
            if (!string.IsNullOrWhiteSpace(Manager)) values["manager"] = Manager;
            if (Overdue) values["overdue"] = "true";
            return values;
        }
    }
}
