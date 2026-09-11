using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using IT_Service_Management_System.Models.Itsm;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace IT_Service_Management_System.Models.Pm
{
    /// <summary>
    /// How a service-desk record relates to a project. Direction is the point: a change that
    /// <em>triggered</em> a project and a change the project <em>caused</em> are opposite facts,
    /// and a register that flattens them into "related" cannot answer either question later.
    /// </summary>
    public enum ProjectLinkRelation
    {
        /// <summary>Associated work — the plain case, and the one the service desk uses most.</summary>
        Related = 0,
        /// <summary>The record that triggered this project's existence.</summary>
        InitiatedProject = 1,
        /// <summary>A record raised as a consequence of this project's work.</summary>
        CausedByProject = 2
    }

    /// <summary>
    /// A typed link between a project (optionally one of its milestones) and a service-desk record.
    ///
    /// Targets are separate nullable foreign keys rather than a loose type/id pair, so the database
    /// keeps referential integrity and a deleted ticket cannot leave a link pointing at nothing.
    /// Exactly one target is set; <see cref="HasSingleTarget"/> states that rule and the controller
    /// enforces it. Tickets are the only target with a live UI today — changes and problems are
    /// modelled but headless — so their columns sit ready rather than being added later under data.
    /// </summary>
    public class ProjectItsmLink
    {
        public int Id { get; set; }

        public int ProjectId { get; set; }
        [ValidateNever] public Project? Project { get; set; }

        /// <summary>
        /// Optional finer anchor. A release or change is often tied to one milestone rather than the
        /// whole project, and losing that detail makes the link much less useful at handover.
        /// </summary>
        public int? MilestoneId { get; set; }
        [ValidateNever] public Milestone? Milestone { get; set; }

        public ProjectLinkRelation Relation { get; set; } = ProjectLinkRelation.Related;

        // ── Targets: exactly one is set ──────────────────────────────────────────
        public int? TicketId { get; set; }
        [ValidateNever] public Ticket? Ticket { get; set; }

        public int? ChangeRequestId { get; set; }
        [ValidateNever] public ChangeRequest? ChangeRequest { get; set; }

        public int? ProblemId { get; set; }
        [ValidateNever] public Problem? Problem { get; set; }

        [StringLength(500)]
        public string? Note { get; set; }

        public int CreatedById { get; set; }
        [ValidateNever] public User? CreatedBy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        /// <summary>True when precisely one target column is populated.</summary>
        [NotMapped]
        public bool HasSingleTarget =>
            (TicketId.HasValue ? 1 : 0) +
            (ChangeRequestId.HasValue ? 1 : 0) +
            (ProblemId.HasValue ? 1 : 0) == 1;

        /// <summary>Which kind of record this link points at, for display and grouping.</summary>
        [NotMapped]
        public string TargetKind =>
            TicketId.HasValue ? "Ticket"
            : ChangeRequestId.HasValue ? "Change"
            : ProblemId.HasValue ? "Problem"
            : "Unknown";

        /// <summary>The target's own reference, when the navigation has been loaded.</summary>
        [NotMapped]
        public string TargetReference =>
            TicketId.HasValue ? Ticket?.Reference ?? $"TKT-{TicketId}"
            : ChangeRequestId.HasValue ? ChangeRequest?.ChangeRef ?? $"CHG-{ChangeRequestId}"
            : ProblemId.HasValue ? Problem?.ProblemRef ?? $"PRB-{ProblemId}"
            : "—";

        /// <summary>The target's title, when the navigation has been loaded.</summary>
        [NotMapped]
        public string TargetTitle =>
            TicketId.HasValue ? Ticket?.Title ?? string.Empty
            : ChangeRequestId.HasValue ? ChangeRequest?.Title ?? string.Empty
            : ProblemId.HasValue ? Problem?.Title ?? string.Empty
            : string.Empty;
    }
}
