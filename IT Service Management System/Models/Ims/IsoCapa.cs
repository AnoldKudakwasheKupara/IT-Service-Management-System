using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace IT_Service_Management_System.Models.Ims
{
    // ── CAPA & Non-Conformance enumerations (ISO 9001 cl. 10.2 / ISO 27001 cl. 10.1) ──
    public enum CapaType { Corrective, Preventive }

    public enum CapaSource
    {
        InternalAudit, ExternalAudit, CustomerComplaint, NonConformance,
        RiskAssessment, ManagementReview, Incident, SupplierIssue, ProcessMonitoring, Other
    }

    public enum CapaStatus
    {
        Open, InvestigatingRootCause, ActionPlanned, InProgress,
        PendingVerification, Verified, Closed, Escalated
    }

    public enum NcSeverity { Minor, Major, Critical }
    public enum NcStatus { Open, UnderInvestigation, ActionInProgress, PendingVerification, Closed }

    /// <summary>
    /// A Corrective or Preventive Action. Covers modules 15 (CAPA) and 16 (Preventive Actions);
    /// preventive actions are simply <see cref="Type"/> == <see cref="CapaType.Preventive"/>.
    /// </summary>
    public class Capa
    {
        public int Id { get; set; }

        [NotMapped] public string Reference => $"CAPA-{Id:D5}";

        [Required, StringLength(200)]
        public string Title { get; set; } = string.Empty;

        public CapaType Type { get; set; } = CapaType.Corrective;
        public CapaSource Source { get; set; } = CapaSource.NonConformance;

        [StringLength(60), Display(Name = "Source Reference")]
        public string? SourceReference { get; set; }

        public IsoStandard Standard { get; set; } = IsoStandard.Both;

        [Required, StringLength(3000)]
        public string Description { get; set; } = string.Empty;

        [StringLength(3000), Display(Name = "Containment / Immediate Action")]
        public string? Containment { get; set; }

        [StringLength(3000), Display(Name = "Correction")]
        public string? Correction { get; set; }

        [StringLength(3000), Display(Name = "Root Cause")]
        public string? RootCause { get; set; }

        [StringLength(3000), Display(Name = "Corrective Action")]
        public string? CorrectiveAction { get; set; }

        [StringLength(3000), Display(Name = "Preventive Action")]
        public string? PreventiveAction { get; set; }

        [Display(Name = "Responsible Person")]
        public int? ResponsibleId { get; set; }
        [ValidateNever] public User? Responsible { get; set; }

        public int? DepartmentId { get; set; }
        [ValidateNever] public Department? Department { get; set; }

        [DataType(DataType.Date)]
        public DateTime? DueDate { get; set; }

        public CapaStatus Status { get; set; } = CapaStatus.Open;

        [StringLength(3000), Display(Name = "Verification")]
        public string? VerificationNotes { get; set; }
        public int? VerifiedById { get; set; }
        [ValidateNever] public User? VerifiedBy { get; set; }
        public DateTime? VerifiedAt { get; set; }

        [StringLength(3000), Display(Name = "Effectiveness Review")]
        public string? EffectivenessReview { get; set; }
        [DataType(DataType.Date)]
        public DateTime? EffectivenessReviewDate { get; set; }

        public bool Escalated { get; set; }
        public DateTime? EscalatedAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public int? CreatedById { get; set; }
        [ValidateNever] public User? CreatedBy { get; set; }
        public DateTime? ClosedAt { get; set; }

        public int? NonConformanceId { get; set; }
        [ValidateNever] public NonConformance? NonConformance { get; set; }

        [ValidateNever] public ICollection<AuditFinding> Findings { get; set; } = new List<AuditFinding>();

        [NotMapped] public bool IsClosed => Status is CapaStatus.Closed or CapaStatus.Verified;
        [NotMapped] public bool IsOverdue => !IsClosed && DueDate.HasValue && DueDate.Value.Date < DateTime.Now.Date;
    }

    /// <summary>A recorded non-conformance (module 17). May be linked to a CAPA for resolution.</summary>
    public class NonConformance
    {
        public int Id { get; set; }

        [NotMapped] public string Reference => $"NC-{Id:D5}";

        [Required, StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required, StringLength(3000)]
        public string Description { get; set; } = string.Empty;

        public NcSeverity Severity { get; set; } = NcSeverity.Minor;
        public CapaSource Source { get; set; } = CapaSource.ProcessMonitoring;
        public IsoStandard Standard { get; set; } = IsoStandard.Both;

        public int? DepartmentId { get; set; }
        [ValidateNever] public Department? Department { get; set; }

        public int? RaisedById { get; set; }
        [ValidateNever] public User? RaisedBy { get; set; }

        [Display(Name = "Assigned To")]
        public int? AssignedToId { get; set; }
        [ValidateNever] public User? AssignedTo { get; set; }

        [Display(Name = "Detected Date"), DataType(DataType.Date)]
        public DateTime DetectedDate { get; set; } = DateTime.Now;

        [StringLength(3000), Display(Name = "Root Cause")]
        public string? RootCause { get; set; }

        [StringLength(2000)]
        public string? Evidence { get; set; }

        public NcStatus Status { get; set; } = NcStatus.Open;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public int? CreatedById { get; set; }
        [ValidateNever] public User? CreatedBy { get; set; }
        public DateTime? ClosedAt { get; set; }

        [ValidateNever] public ICollection<Capa> Capas { get; set; } = new List<Capa>();

        [NotMapped] public bool IsClosed => Status == NcStatus.Closed;
    }
}
