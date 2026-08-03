using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace IT_Service_Management_System.Models.Hr
{
    /// <summary>
    /// A category of misconduct and the penalty range it carries.
    /// <para>
    /// Zimbabwe's default position is the National Employment Code of Conduct in Statutory
    /// Instrument 15 of 2006, which applies wherever an employer has no registered code of its own.
    /// An employer may register a code through its NEC or works council, and where it has, that code
    /// governs instead — so offences are data rather than an enum, and each records which code it
    /// comes from.
    /// </para>
    /// </summary>
    public class DisciplinaryOffence
    {
        public int Id { get; set; }

        [Required, StringLength(30)]
        public string Code { get; set; } = string.Empty;

        [Required, StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [StringLength(1500)]
        public string? Description { get; set; }

        /// <summary>
        /// Which code of conduct this offence comes from — the model code in SI 15 of 2006, or the
        /// employer's own registered code.
        /// </summary>
        [StringLength(200)]
        public string? Authority { get; set; }

        public OffenceSeriousness Seriousness { get; set; } = OffenceSeriousness.Minor;

        /// <summary>
        /// Whether a first offence may attract dismissal. Under the model code only a limited set
        /// of acts of misconduct — theft, fraud, wilful damage, gross insubordination, absence
        /// without leave for five or more days — justify dismissal on a first occasion.
        /// </summary>
        [Display(Name = "Dismissable on a first offence")]
        public bool DismissableFirstOffence { get; set; }

        /// <summary>Penalty normally imposed for a first proven occurrence.</summary>
        public DisciplinaryPenalty DefaultFirstPenalty { get; set; } = DisciplinaryPenalty.VerbalWarning;

        /// <summary>
        /// How long a warning for this offence stays live before it falls away. Warnings are not
        /// permanent; a spent warning cannot be counted towards progression.
        /// </summary>
        [Display(Name = "Warning valid for (months)")]
        public int WarningValidityMonths { get; set; } = 12;

        public bool IsActive { get; set; } = true;
        public int DisplayOrder { get; set; }

        [ValidateNever] public ICollection<DisciplinaryCase> Cases { get; set; } = new List<DisciplinaryCase>();
    }

    public enum OffenceSeriousness { Minor, Serious, Gross }

    /// <summary>
    /// The penalties available under the model code, in ascending order of severity.
    /// </summary>
    public enum DisciplinaryPenalty
    {
        None,
        VerbalWarning,
        WrittenWarning,
        FinalWritten,
        /// <summary>Suspension without pay, where the code of conduct allows it.</summary>
        SuspensionWithoutPay,
        Demotion,
        /// <summary>Termination on notice, as distinct from summary dismissal.</summary>
        DismissalOnNotice,
        /// <summary>Summary dismissal, for misconduct going to the root of the contract.</summary>
        SummaryDismissal
    }

    /// <summary>
    /// A disciplinary case, from allegation through hearing to penalty and appeal.
    /// <para>
    /// The stages exist because procedural fairness is what an unfair-dismissal claim turns on. The
    /// employee must know the charge, have time to prepare, be heard, be allowed representation,
    /// and be told of the right to appeal. The module records each of those as a fact with a date,
    /// so the employer can show it happened rather than assert it.
    /// </para>
    /// </summary>
    public class DisciplinaryCase
    {
        public int Id { get; set; }

        [NotMapped]
        public string Reference => $"DC-{Id:D5}";

        public int EmployeeId { get; set; }
        [ValidateNever] public Employee? Employee { get; set; }

        public int? OffenceId { get; set; }
        [ValidateNever] public DisciplinaryOffence? Offence { get; set; }

        [Required, StringLength(250)]
        [Display(Name = "Allegation")]
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// What is alleged, in enough detail that the employee can answer it. A charge the employee
        /// cannot understand is a charge they cannot defend.
        /// </summary>
        [Required, StringLength(4000)]
        [Display(Name = "Particulars of the allegation")]
        public string Particulars { get; set; } = string.Empty;

        [DataType(DataType.Date)]
        [Display(Name = "Date of the incident")]
        public DateTime IncidentDate { get; set; } = DateTime.Today;

        [DataType(DataType.Date)]
        [Display(Name = "Date reported")]
        public DateTime ReportedDate { get; set; } = DateTime.Today;

        public DisciplinaryStatus Status { get; set; } = DisciplinaryStatus.Reported;

        // ── The charge ───────────────────────────────────────────────────────────
        /// <summary>When the written notice of the charge was given to the employee.</summary>
        [DataType(DataType.Date)]
        [Display(Name = "Charge served on")]
        public DateTime? ChargeServedDate { get; set; }

        [StringLength(260)] public string? ChargeDocumentName { get; set; }
        [StringLength(500)] public string? ChargeDocumentPath { get; set; }

        // ── Suspension pending the hearing ───────────────────────────────────────
        /// <summary>
        /// Suspension while the matter is investigated. Normally on full pay — suspending without
        /// pay before anything is proven is itself open to challenge.
        /// </summary>
        [Display(Name = "Suspended pending the hearing")]
        public bool IsSuspended { get; set; }

        [Display(Name = "Suspension on full pay")]
        public bool SuspensionOnFullPay { get; set; } = true;

        [DataType(DataType.Date)] public DateTime? SuspensionFrom { get; set; }
        [DataType(DataType.Date)] public DateTime? SuspensionTo { get; set; }

        // ── The hearing ──────────────────────────────────────────────────────────
        public DateTime? HearingDate { get; set; }

        [StringLength(200)] public string? HearingVenue { get; set; }

        /// <summary>Who chaired. The chair should not be the complainant or a witness.</summary>
        public int? ChairpersonId { get; set; }
        [ValidateNever] public Employee? Chairperson { get; set; }

        /// <summary>
        /// Whether the employee was told they could be represented — by a fellow employee, a
        /// workers' committee member or a trade union representative.
        /// </summary>
        [Display(Name = "Right to representation explained")]
        public bool RepresentationOffered { get; set; }

        [StringLength(200)]
        [Display(Name = "Represented by")]
        public string? RepresentedBy { get; set; }

        [Display(Name = "Employee attended")]
        public bool EmployeeAttended { get; set; }

        /// <summary>Recorded when a hearing proceeded in the employee's absence, and why.</summary>
        [StringLength(1000)]
        public string? AbsenceExplanation { get; set; }

        [StringLength(8000)]
        [Display(Name = "Employee's response")]
        public string? EmployeeResponse { get; set; }

        [StringLength(8000)]
        [Display(Name = "Hearing minutes")]
        public string? HearingMinutes { get; set; }

        // ── Finding and penalty ──────────────────────────────────────────────────
        public DisciplinaryFinding Finding { get; set; } = DisciplinaryFinding.Pending;

        [StringLength(4000)]
        [Display(Name = "Reasons for the finding")]
        public string? FindingReasons { get; set; }

        public DisciplinaryPenalty Penalty { get; set; } = DisciplinaryPenalty.None;

        [StringLength(2000)]
        [Display(Name = "Reasons for the penalty")]
        public string? PenaltyReasons { get; set; }

        /// <summary>
        /// Mitigating and aggravating factors weighed before the penalty was set. A penalty imposed
        /// without considering mitigation is vulnerable on review.
        /// </summary>
        [StringLength(2000)]
        [Display(Name = "Mitigation considered")]
        public string? MitigationConsidered { get; set; }

        [DataType(DataType.Date)] public DateTime? PenaltyDate { get; set; }

        /// <summary>When a warning falls away. Null for penalties that do not expire.</summary>
        [DataType(DataType.Date)]
        [Display(Name = "Warning expires")]
        public DateTime? WarningExpiryDate { get; set; }

        // ── Appeal ───────────────────────────────────────────────────────────────
        /// <summary>Whether the employee was told of the right to appeal, and by when.</summary>
        [Display(Name = "Right of appeal explained")]
        public bool AppealRightExplained { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Appeal must be lodged by")]
        public DateTime? AppealDeadline { get; set; }

        public bool AppealLodged { get; set; }
        [DataType(DataType.Date)] public DateTime? AppealLodgedDate { get; set; }

        [StringLength(4000)]
        [Display(Name = "Grounds of appeal")]
        public string? AppealGrounds { get; set; }

        public int? AppealHeardById { get; set; }
        [ValidateNever] public Employee? AppealHeardBy { get; set; }

        [DataType(DataType.Date)] public DateTime? AppealHeardDate { get; set; }

        public AppealOutcome? AppealOutcome { get; set; }

        [StringLength(4000)]
        [Display(Name = "Appeal decision and reasons")]
        public string? AppealDecision { get; set; }

        /// <summary>Penalty substituted on appeal, where the appeal reduced it.</summary>
        public DisciplinaryPenalty? SubstitutedPenalty { get; set; }

        // ── Housekeeping ─────────────────────────────────────────────────────────
        public int RaisedById { get; set; }
        [ValidateNever] public User? RaisedBy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }
        public DateTime? ClosedAt { get; set; }

        [ValidateNever] public ICollection<DisciplinaryEvent> Events { get; set; } = new List<DisciplinaryEvent>();

        // ── Derived ──────────────────────────────────────────────────────────────
        [NotMapped]
        public bool IsOpen => Status is not (DisciplinaryStatus.Closed or DisciplinaryStatus.Withdrawn);

        /// <summary>The penalty that stands — the substituted one where an appeal reduced it.</summary>
        [NotMapped]
        public DisciplinaryPenalty EffectivePenalty => SubstitutedPenalty ?? Penalty;

        /// <summary>A warning still counts towards progression until its expiry date passes.</summary>
        [NotMapped]
        public bool IsWarningLive =>
            Penalty is DisciplinaryPenalty.VerbalWarning or DisciplinaryPenalty.WrittenWarning
                or DisciplinaryPenalty.FinalWritten
            && WarningExpiryDate.HasValue && WarningExpiryDate.Value >= DateTime.Today;

        [NotMapped]
        public bool ResultedInDismissal =>
            EffectivePenalty is DisciplinaryPenalty.DismissalOnNotice or DisciplinaryPenalty.SummaryDismissal;

        /// <summary>
        /// The procedural steps a fair process needs, and whether each is on record. This is what
        /// an employer has to be able to show, so it is computed rather than left to be inferred.
        /// </summary>
        [NotMapped]
        public List<(string Step, bool Done, string Why)> FairnessChecklist => new()
        {
            ("Written charge served", ChargeServedDate.HasValue,
                "The employee must know the allegation in writing before the hearing."),
            ("Notice before the hearing", ChargeServedDate.HasValue && HearingDate.HasValue
                && (HearingDate.Value.Date - ChargeServedDate.Value.Date).TotalDays >= 2,
                "Time to prepare a defence. Same-day hearings are hard to defend as fair."),
            ("Right to representation explained", RepresentationOffered,
                "By a fellow employee, workers' committee member or union representative."),
            ("Employee heard", EmployeeAttended || !string.IsNullOrWhiteSpace(AbsenceExplanation),
                "Either the employee attended, or there is a recorded reason the hearing proceeded without them."),
            ("Hearing minuted", !string.IsNullOrWhiteSpace(HearingMinutes),
                "A record of what was said, by whom."),
            ("Finding reasoned", !string.IsNullOrWhiteSpace(FindingReasons),
                "Why the allegation was found proven or not."),
            ("Mitigation considered", !string.IsNullOrWhiteSpace(MitigationConsidered),
                "A penalty set without weighing mitigation is vulnerable on review."),
            ("Right of appeal explained", AppealRightExplained,
                "The employee must be told they may appeal, and by when.")
        };

        [NotMapped]
        public int FairnessScore
        {
            get
            {
                var steps = FairnessChecklist;
                return steps.Count == 0 ? 0 : (int)Math.Round(steps.Count(s => s.Done) * 100.0 / steps.Count);
            }
        }
    }

    public enum DisciplinaryStatus
    {
        Reported,
        UnderInvestigation,
        ChargeServed,
        HearingScheduled,
        HearingHeld,
        PenaltyImposed,
        UnderAppeal,
        Closed,
        /// <summary>Dropped before a finding — the allegation was not pursued.</summary>
        Withdrawn
    }

    public enum DisciplinaryFinding { Pending, Proven, NotProven, PartiallyProven }

    public enum AppealOutcome { Upheld, Dismissed, PenaltyReduced, RemittedForRehearing }

    /// <summary>
    /// A dated entry on the case file. Disciplinary matters are reconstructed months later in front
    /// of a works council, a labour officer or an arbitrator, so every step is recorded as it
    /// happens rather than summarised afterwards.
    /// </summary>
    public class DisciplinaryEvent
    {
        public int Id { get; set; }

        public int CaseId { get; set; }
        [ValidateNever] public DisciplinaryCase? Case { get; set; }

        [Required, StringLength(120)]
        public string Step { get; set; } = string.Empty;

        [StringLength(2000)]
        public string? Detail { get; set; }

        public DateTime At { get; set; } = DateTime.Now;

        public int? RecordedById { get; set; }
        [ValidateNever] public User? RecordedBy { get; set; }

        [StringLength(260)] public string? DocumentName { get; set; }
        [StringLength(500)] public string? DocumentPath { get; set; }
    }
}
