using IT_Service_Management_System.Models.Pm;

namespace IT_Service_Management_System.ViewModels.Pm
{
    /// <summary>The milestones tab: a project's plan in the order its manager arranged it.</summary>
    public class ProjectPlanVm
    {
        public Project Project { get; set; } = null!;
        public List<Milestone> Milestones { get; set; } = new();
        public bool CanContribute { get; set; }

        public int Achieved => Milestones.Count(m => m.Status == MilestoneStatus.Achieved);
        public int Overdue => Milestones.Count(m => m.IsOverdue);
        public int Percent => Milestones.Count == 0 ? 0 : Achieved * 100 / Milestones.Count;
    }
}
