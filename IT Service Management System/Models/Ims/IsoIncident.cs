using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace IT_Service_Management_System.Models.Ims
{
    // ── Incident Management enumerations (ISO 9001 cl. 10.2 / ISO 27001 cl. 16 / cl. 10) ──

    /// <summary>Severity band of the incident (Section E).</summary>
    public enum IncidentSeverity { Minor, Serious, Major }

    /// <summary>Probability of recurrence (Section E).</summary>
    public enum IncidentProbability { Low, Medium, High }

    /// <summary>State of the investigation report itself (Section E).</summary>
    public enum InvestigationReportStatus { Initial, InProgress, Final }

    /// <summary>Overall lifecycle of the incident record.</summary>
    public enum IncidentStatus { Reported, UnderInvestigation, ActionsPending, Closed }

    /// <summary>Category of damage recorded in Section G.</summary>
    public enum IncidentDamageType { Equipment, People, Environment, Product, Reputation, Other }

    /// <summary>Status of a remedial action (Section K).</summary>
    public enum IncidentActionStatus { Open, InProgress, Completed }

    /// <summary>Classifies an incident attachment. Police reports are tracked separately so they can be enforced.</summary>
    public enum IncidentAttachmentKind { Supporting, PoliceReport }

    /// <summary>
    /// Canonical list of per-item file-upload slots on the incident form: each Section F supporting-document
    /// type and each Section D evidence line. The Slug is the form field name; the Label is stored as the
    /// attachment's Category. Shared by the form view and the controller so they never drift apart.
    /// </summary>
    public static class IncidentFileCategories
    {
        public static readonly (string Slug, string Label)[] All = new[]
        {
            ("catfile_pollution",   "Pollution Report"),
            ("catfile_sketch",      "Sketch Diagram"),
            ("catfile_written",     "Written Statements"),
            ("catfile_motor",       "Motor Insurance Details"),
            ("catfile_labour",      "Dept of Labour"),
            ("catfile_driver",      "Driver's Details"),
            ("catfile_audit",       "Internal Audit"),
            ("catfile_workmen",     "Workmen Compensation"),
            ("catfile_other",       "Other Supporting Document"),
            ("catfile_evpeople",    "Evidence — People"),
            ("catfile_evpaper",     "Evidence — Paper"),
            ("catfile_evparts",     "Evidence — Parts"),
            ("catfile_evpositions", "Evidence — Positions"),
        };
    }

    /// <summary>
    /// An incident investigation report (ISO 9001/27001 cl. 10 Improvement — Incidents). Digitises the
    /// Axis "Incident Investigation Report" form, Sections A–O. Narrative sections are scalar fields;
    /// the three tabular sections — investigation team (C), damage (G) and remedial actions (K) —
    /// are child collections managed from the Details page.
    /// </summary>
    public class Incident
    {
        public int Id { get; set; }

        /// <summary>Per-year sequence, e.g. 3 → "INC-2026-003". Assigned on create.</summary>
        public int IncidentNo { get; set; }
        public int Year { get; set; } = DateTime.Now.Year;

        [NotMapped] public string Reference => $"INC-{Year}-{IncidentNo:D3}";

        [Required, StringLength(200)]
        public string Title { get; set; } = string.Empty;

        public IsoStandard Standard { get; set; } = IsoStandard.Both;
        public IncidentStatus Status { get; set; } = IncidentStatus.Reported;

        // ── Section A — reported by ────────────────────────────────────────────────
        [Display(Name = "Date of Incident"), DataType(DataType.Date)]
        public DateTime? DateOfIncident { get; set; }

        [Display(Name = "Time of Incident"), StringLength(40)]
        public string? TimeOfIncident { get; set; }

        [Display(Name = "Location of Incident"), StringLength(250)]
        public string? LocationOfIncident { get; set; }

        [Display(Name = "Function / Department")]
        public int? DepartmentId { get; set; }
        [ValidateNever] public Department? Department { get; set; }

        [Display(Name = "Reported By"), StringLength(150)]
        public string? ReportedByName { get; set; }

        [Display(Name = "Date Reported"), DataType(DataType.Date)]
        public DateTime? DateReported { get; set; }

        // ── Section B — brief description ──────────────────────────────────────────
        [Display(Name = "Brief description of incident"), StringLength(6000)]
        public string? BriefDescription { get; set; }

        // ── Section C — police (reporter) ─────────────────────────────────────────
        /// <summary>Whether the incident was reported to the police. When true, a police report file is mandatory.</summary>
        [Display(Name = "Reported to Police?")]
        public bool? ReportedToPolice { get; set; }

        [Display(Name = "Reported to Police at"), StringLength(250)]
        public string? ReportedToPoliceAt { get; set; }

        [Display(Name = "Police details & Tel"), StringLength(250)]
        public string? PoliceDetailsTel { get; set; }

        [Display(Name = "Case Number"), StringLength(100)]
        public string? CaseNumber { get; set; }

        // ── Section D — detailed description / sequence of events + evidence ───────
        [Display(Name = "Detailed description / sequence of events"), StringLength(12000)]
        public string? DetailedDescription { get; set; }

        [Display(Name = "Evidence — People"), StringLength(2000)]
        public string? EvidencePeople { get; set; }
        [Display(Name = "Evidence — Paper"), StringLength(2000)]
        public string? EvidencePaper { get; set; }
        [Display(Name = "Evidence — Parts"), StringLength(2000)]
        public string? EvidenceParts { get; set; }
        [Display(Name = "Evidence — Positions"), StringLength(2000)]
        public string? EvidencePositions { get; set; }

        // ── Section E — classification ────────────────────────────────────────────
        [Display(Name = "Incident category"), StringLength(200)]
        public string? Category { get; set; }

        public IncidentSeverity Severity { get; set; } = IncidentSeverity.Minor;
        public IncidentProbability Probability { get; set; } = IncidentProbability.Low;

        [Display(Name = "Status of investigation report")]
        public InvestigationReportStatus ReportStatus { get; set; } = InvestigationReportStatus.Initial;

        // ── Section F — supporting documents checklist ────────────────────────────
        public bool DocPollutionReport { get; set; }
        public bool DocSketchDiagram { get; set; }
        public bool DocWrittenStatements { get; set; }
        public bool DocMotorInsurance { get; set; }
        public bool DocDeptOfLabour { get; set; }
        public bool DocDriversDetails { get; set; }
        public bool DocInternalAudit { get; set; }
        public bool DocWorkmenCompensation { get; set; }
        public bool DocOther { get; set; }
        [Display(Name = "Other (describe)"), StringLength(300)]
        public string? DocOtherText { get; set; }

        // ── Section H — preventability / claim ────────────────────────────────────
        [Display(Name = "Was the incident preventable?")]
        public bool? Preventable { get; set; }
        [Display(Name = "Preventability notes"), StringLength(1000)]
        public string? PreventableNotes { get; set; }

        [Display(Name = "Is the incident claimable?")]
        public bool? Claimable { get; set; }

        [Display(Name = "Was it claimed from insurance / any party?")]
        public bool? ClaimedFromInsurance { get; set; }
        [Display(Name = "Claim notes"), StringLength(1000)]
        public string? ClaimNotes { get; set; }

        // ── Section I — critical factors ──────────────────────────────────────────
        [Display(Name = "Critical factors identified"), StringLength(6000)]
        public string? CriticalFactors { get; set; }

        // ── Section J — root cause analysis ───────────────────────────────────────
        [Display(Name = "Immediate cause(s)"), StringLength(4000)]
        public string? ImmediateCause { get; set; }
        [Display(Name = "Basic cause(s)"), StringLength(4000)]
        public string? BasicCause { get; set; }
        [Display(Name = "Root cause"), StringLength(4000)]
        public string? RootCause { get; set; }

        // ── Section L — lessons learned ───────────────────────────────────────────
        [Display(Name = "Lessons learned"), StringLength(6000)]
        public string? LessonsLearned { get; set; }

        // ── Sections M / N / O — sign-off comments ────────────────────────────────
        [Display(Name = "Department Manager / Team Leader comments"), StringLength(4000)]
        public string? DeptManagerComments { get; set; }
        [DataType(DataType.Date)] public DateTime? DeptManagerCommentDate { get; set; }
        public int? DeptManagerSignedById { get; set; }
        [ValidateNever] public User? DeptManagerSignedBy { get; set; }

        [Display(Name = "Quality Assurance comments"), StringLength(4000)]
        public string? QaComments { get; set; }
        [DataType(DataType.Date)] public DateTime? QaCommentDate { get; set; }
        public int? QaSignedById { get; set; }
        [ValidateNever] public User? QaSignedBy { get; set; }

        [Display(Name = "General Manager comments (major incidents)"), StringLength(4000)]
        public string? GmComments { get; set; }
        [DataType(DataType.Date)] public DateTime? GmCommentDate { get; set; }
        public int? GmSignedById { get; set; }
        [ValidateNever] public User? GmSignedBy { get; set; }

        // ── Audit / lifecycle ─────────────────────────────────────────────────────
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public int? CreatedById { get; set; }
        [ValidateNever] public User? CreatedBy { get; set; }
        public DateTime? ClosedAt { get; set; }

        // Optional link to a raised CAPA (Section J/K often flow into corrective action).
        public int? CapaId { get; set; }
        [ValidateNever] public Capa? Capa { get; set; }

        [ValidateNever] public ICollection<IncidentInvestigator> Investigators { get; set; } = new List<IncidentInvestigator>();
        [ValidateNever] public ICollection<IncidentDamage> Damages { get; set; } = new List<IncidentDamage>();
        [ValidateNever] public ICollection<IncidentAction> Actions { get; set; } = new List<IncidentAction>();
        [ValidateNever] public ICollection<IncidentAttachment> Attachments { get; set; } = new List<IncidentAttachment>();

        [NotMapped] public bool IsClosed => Status == IncidentStatus.Closed;

        /// <summary>True when a police report is required (reported to police) but none has been attached.</summary>
        [NotMapped] public bool PoliceReportMissing =>
            ReportedToPolice == true && !Attachments.Any(a => a.Kind == IncidentAttachmentKind.PoliceReport);
    }

    /// <summary>A file attached to an incident (supporting evidence or the police report). Bytes live in shared storage.</summary>
    public class IncidentAttachment
    {
        public int Id { get; set; }
        public int IncidentId { get; set; }
        [ValidateNever] public Incident? Incident { get; set; }

        public IncidentAttachmentKind Kind { get; set; } = IncidentAttachmentKind.Supporting;

        /// <summary>The specific form item this file backs, e.g. "Written Statements" or "Evidence — Paper" (null = general).</summary>
        [StringLength(120)] public string? Category { get; set; }

        [StringLength(300)] public string? Description { get; set; }

        [StringLength(260)] public string StoredFileName { get; set; } = string.Empty;
        [StringLength(260)] public string OriginalFileName { get; set; } = string.Empty;
        [StringLength(150)] public string? ContentType { get; set; }
        public long FileSize { get; set; }
        [StringLength(50)] public string? StorageProvider { get; set; }

        public int? UploadedById { get; set; }
        [ValidateNever] public User? UploadedBy { get; set; }
        public DateTime UploadedAt { get; set; } = DateTime.Now;
    }

    /// <summary>Section C — a member of the investigation team.</summary>
    public class IncidentInvestigator
    {
        public int Id { get; set; }
        public int IncidentId { get; set; }
        [ValidateNever] public Incident? Incident { get; set; }

        [Required, StringLength(150)]
        public string Name { get; set; } = string.Empty;

        [StringLength(150), Display(Name = "Position / Title")]
        public string? Position { get; set; }

        [DataType(DataType.Date), Display(Name = "Date of Investigation")]
        public DateTime? InvestigationDate { get; set; }
    }

    /// <summary>Section G — a line of damage arising from the incident.</summary>
    public class IncidentDamage
    {
        public int Id { get; set; }
        public int IncidentId { get; set; }
        [ValidateNever] public Incident? Incident { get; set; }

        public IncidentDamageType Type { get; set; } = IncidentDamageType.Equipment;

        [StringLength(1000), Display(Name = "Description of Damage")]
        public string? Description { get; set; }

        [StringLength(200), Display(Name = "Damage Payer")]
        public string? Payer { get; set; }

        [StringLength(100), Display(Name = "Cost (USD)")]
        public string? Cost { get; set; }
    }

    /// <summary>Section K — a recommended remedial action.</summary>
    public class IncidentAction
    {
        public int Id { get; set; }
        public int IncidentId { get; set; }
        [ValidateNever] public Incident? Incident { get; set; }

        [Required, StringLength(1000), Display(Name = "Recommended Remedial Action")]
        public string Description { get; set; } = string.Empty;

        [StringLength(150), Display(Name = "Responsible Person")]
        public string? ResponsiblePerson { get; set; }

        [DataType(DataType.Date), Display(Name = "Planned Completion Date")]
        public DateTime? PlannedDate { get; set; }

        public IncidentActionStatus Status { get; set; } = IncidentActionStatus.Open;

        [DataType(DataType.Date)] public DateTime? CompletedDate { get; set; }

        [NotMapped] public bool IsOverdue =>
            Status != IncidentActionStatus.Completed
            && PlannedDate.HasValue && PlannedDate.Value.Date < DateTime.Now.Date;
    }
}
