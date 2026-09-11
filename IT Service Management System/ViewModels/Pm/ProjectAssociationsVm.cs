using IT_Service_Management_System.Models.Pm;

namespace IT_Service_Management_System.ViewModels.Pm
{
    /// <summary>A ticket offered for linking, flattened so the picker needs no entity graph.</summary>
    public record TicketOption(int Id, string Reference, string Title, string Status);

    /// <summary>
    /// The associations tab: which service-desk records this project answers, and in which
    /// direction. Grouped by relation, because "we caused this" and "this caused us" are the two
    /// questions people actually arrive with.
    /// </summary>
    public class ProjectAssociationsVm
    {
        public Project Project { get; set; } = null!;
        public List<ProjectItsmLink> Links { get; set; } = new();
        public List<TicketOption> TicketOptions { get; set; } = new();
        public List<Milestone> Milestones { get; set; } = new();
        public bool CanContribute { get; set; }

        public IEnumerable<ProjectItsmLink> Of(ProjectLinkRelation relation) =>
            Links.Where(l => l.Relation == relation);

        public int CountOf(ProjectLinkRelation relation) => Links.Count(l => l.Relation == relation);

        /// <summary>The heading each relation gets, phrased from the project's point of view.</summary>
        public static string Heading(ProjectLinkRelation relation) => relation switch
        {
            ProjectLinkRelation.InitiatedProject => "Records that started this project",
            ProjectLinkRelation.CausedByProject => "Records this project caused",
            _ => "Related work"
        };

        public static string Blurb(ProjectLinkRelation relation) => relation switch
        {
            ProjectLinkRelation.InitiatedProject =>
                "The request or change that made the case for doing this at all.",
            ProjectLinkRelation.CausedByProject =>
                "Raised as a consequence of this project — fallout, follow-up work, or a change it forced.",
            _ => "Service-desk work this project is delivering against."
        };

        public static string Icon(ProjectLinkRelation relation) => relation switch
        {
            ProjectLinkRelation.InitiatedProject => "fa-arrow-right-to-bracket",
            ProjectLinkRelation.CausedByProject => "fa-arrow-right-from-bracket",
            _ => "fa-link"
        };
    }
}
