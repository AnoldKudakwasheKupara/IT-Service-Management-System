using IT_Service_Management_System.Models;
using IT_Service_Management_System.Models.Pm;

namespace IT_Service_Management_System.ViewModels.Pm
{
    /// <summary>
    /// One project's record: the project itself, its team, and the roll-ups the overview shows.
    ///
    /// The counts are queried rather than walked over loaded collections — a project with hundreds
    /// of tasks should not drag them all into memory to print "84 of 95 done".
    /// </summary>
    public class ProjectDetailsVm
    {
        public Project Project { get; set; } = null!;

        public List<ProjectTeamMember> Team { get; set; } = new();

        public int MilestonesTotal { get; set; }
        public int MilestonesAchieved { get; set; }
        public int TasksTotal { get; set; }
        public int TasksCompleted { get; set; }
        public int LinkedRecords { get; set; }
        public int OpenRisks { get; set; }
        public int OpenIssues { get; set; }

        /// <summary>Users who can still be added to the team — everyone active who is not on it.</summary>
        public List<User> AssignableUsers { get; set; } = new();

        public bool CanEdit { get; set; }
        public bool CanContribute { get; set; }
        public bool CanApprove { get; set; }
        public bool CanDelete { get; set; }

        public int MilestonePercent => MilestonesTotal == 0 ? 0 : MilestonesAchieved * 100 / MilestonesTotal;
        public int TaskPercent => TasksTotal == 0 ? 0 : TasksCompleted * 100 / TasksTotal;
    }
}
