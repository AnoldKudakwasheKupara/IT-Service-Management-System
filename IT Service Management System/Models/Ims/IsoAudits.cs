using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace IT_Service_Management_System.Models.Ims
{
    // ── Internal Audit enumerations (ISO 9001 cl. 9.2 / ISO 27001 cl. 9.2) ───────
    public enum AuditType { Internal, External, Surveillance, Certification, Supplier, Followup }
    public enum AuditStatus { Planned, Scheduled, InProgress, Completed, Closed, Cancelled }
    public enum AuditTeamRole { LeadAuditor, Auditor, Observer, Auditee, TechnicalExpert }
    public enum ChecklistResult { Pending, Conform, NonConform, NotApplicable }

    /// <summary>Finding classification. Major/Minor NC feed the CAPA process automatically.</summary>
    public enum FindingType { MajorNonConformance, MinorNonConformance, Observation, OpportunityForImprovement, Conformity }
    public enum FindingStatus { Open, InProgress, CapaRaised, PendingVerification, Verified, Closed }

    /// <summary>An annual (or periodic) audit programme grouping individual audits.</summary>
    public class AuditProgramme
    {
        public int Id { get; set; }

        [Required, StringLength(150)]
        public string Title { get; set; } = string.Empty;

        [Range(2000, 2100)]
        public int Year { get; set; } = DateTime.Now.Year;

        public IsoStandard Standard { get; set; } = IsoStandard.Both;

        [StringLength(2000)]
        public string? Objectives { get; set; }

        public AuditStatus Status { get; set; } = AuditStatus.Planned;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public int? CreatedById { get; set; }
        [ValidateNever] public User? CreatedBy { get; set; }

        [ValidateNever] public ICollection<Audit> Audits { get; set; } = new List<Audit>();
    }

    /// <summary>A single audit event, with team, checklist and findings.</summary>
    public class Audit
    {
        public int Id { get; set; }

        [NotMapped] public string Reference => $"AUD-{Id:D5}";

        [Required, StringLength(200)]
        public string Title { get; set; } = string.Empty;

        public AuditType Type { get; set; } = AuditType.Internal;
        public IsoStandard Standard { get; set; } = IsoStandard.Both;

        [StringLength(2000)]
        public string? Scope { get; set; }

        [StringLength(2000)]
        public string? Objectives { get; set; }

        [StringLength(2000), Display(Name = "Criteria")]
        public string? Criteria { get; set; }

        public int? AuditProgrammeId { get; set; }
        [ValidateNever] public AuditProgramme? AuditProgramme { get; set; }

        public int? DepartmentId { get; set; }
        [ValidateNever] public Department? Department { get; set; }

        [Display(Name = "Lead Auditor")]
        public int? LeadAuditorId { get; set; }
        [ValidateNever] public User? LeadAuditor { get; set; }

        [Display(Name = "Planned Start"), DataType(DataType.Date)]
        public DateTime? PlannedStartDate { get; set; }
        [Display(Name = "Planned End"), DataType(DataType.Date)]
        public DateTime? PlannedEndDate { get; set; }
        public DateTime? ActualStartDate { get; set; }
        public DateTime? ActualEndDate { get; set; }

        public AuditStatus Status { get; set; } = AuditStatus.Planned;

        [StringLength(3000)]
        public string? Summary { get; set; }

        [StringLength(3000), Display(Name = "Conclusion")]
        public string? Conclusion { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public int? CreatedById { get; set; }
        [ValidateNever] public User? CreatedBy { get; set; }

        [ValidateNever] public ICollection<AuditTeamMember> TeamMembers { get; set; } = new List<AuditTeamMember>();
        [ValidateNever] public ICollection<AuditChecklistItem> ChecklistItems { get; set; } = new List<AuditChecklistItem>();
        [ValidateNever] public ICollection<AuditFinding> Findings { get; set; } = new List<AuditFinding>();
    }

    public class AuditTeamMember
    {
        public int Id { get; set; }
        public int AuditId { get; set; }
        [ValidateNever] public Audit? Audit { get; set; }
        public int UserId { get; set; }
        [ValidateNever] public User? User { get; set; }
        public AuditTeamRole RoleOnTeam { get; set; } = AuditTeamRole.Auditor;
    }

    public class AuditChecklistItem
    {
        public int Id { get; set; }
        public int AuditId { get; set; }
        [ValidateNever] public Audit? Audit { get; set; }

        [StringLength(30), Display(Name = "Clause")]
        public string? ClauseReference { get; set; }

        [Required, StringLength(1000)]
        public string Question { get; set; } = string.Empty;

        public ChecklistResult Result { get; set; } = ChecklistResult.Pending;

        [StringLength(2000)]
        public string? Evidence { get; set; }

        [StringLength(1000)]
        public string? Notes { get; set; }

        public int Sequence { get; set; }
    }

    /// <summary>An audit finding (or observation). Major/Minor NCs can auto-generate a CAPA.</summary>
    public class AuditFinding
    {
        public int Id { get; set; }

        [NotMapped] public string Reference => $"FND-{Id:D5}";

        public int? AuditId { get; set; }
        [ValidateNever] public Audit? Audit { get; set; }

        public FindingType Type { get; set; } = FindingType.Observation;

        [StringLength(30), Display(Name = "ISO Clause")]
        public string? ClauseReference { get; set; }

        [Required, StringLength(2000)]
        public string Description { get; set; } = string.Empty;

        [StringLength(2000)]
        public string? Evidence { get; set; }

        public int? DepartmentId { get; set; }
        [ValidateNever] public Department? Department { get; set; }

        public int? RaisedById { get; set; }
        [ValidateNever] public User? RaisedBy { get; set; }

        [Display(Name = "Assigned To")]
        public int? AssignedToId { get; set; }
        [ValidateNever] public User? AssignedTo { get; set; }

        [DataType(DataType.Date)]
        public DateTime? DueDate { get; set; }

        public FindingStatus Status { get; set; } = FindingStatus.Open;

        /// <summary>Set when a CAPA has been generated from this finding.</summary>
        public int? CapaId { get; set; }
        [ValidateNever] public Capa? Capa { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? ClosedAt { get; set; }

        [NotMapped] public bool IsNonConformance =>
            Type is FindingType.MajorNonConformance or FindingType.MinorNonConformance;
        [NotMapped] public bool IsOverdue => Status != FindingStatus.Closed && DueDate.HasValue && DueDate.Value.Date < DateTime.Now.Date;
    }
}
