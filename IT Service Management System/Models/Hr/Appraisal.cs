using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace IT_Service_Management_System.Models.Hr
{
    /// <summary>
    /// A review period — annual, half-yearly, or a probation review.
    /// <para>
    /// Cycles are opened and closed deliberately rather than running continuously, because a rating
    /// only means something against a stated period and a stated standard.
    /// </para>
    /// </summary>
    public class AppraisalCycle
    {
        public int Id { get; set; }

        [Required, StringLength(150)]
        public string Name { get; set; } = string.Empty;

        [StringLength(1000)]
        public string? Description { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Period from")]
        public DateTime PeriodStart { get; set; } = new DateTime(DateTime.Today.Year, 1, 1);

        [DataType(DataType.Date)]
        [Display(Name = "Period to")]
        public DateTime PeriodEnd { get; set; } = new DateTime(DateTime.Today.Year, 12, 31);

        [DataType(DataType.Date)]
        [Display(Name = "Self-assessment due")]
        public DateTime? SelfAssessmentDue { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Manager review due")]
        public DateTime? ManagerReviewDue { get; set; }

        public AppraisalCycleStatus Status { get; set; } = AppraisalCycleStatus.Planned;

        /// <summary>
        /// A probation review is a cycle in its own right — the standard, the evidence and the
        /// decision all belong on the record, not in a conversation nobody wrote down.
        /// </summary>
        [Display(Name = "Probation review cycle")]
        public bool IsProbationReview { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [ValidateNever] public ICollection<Appraisal> Appraisals { get; set; } = new List<Appraisal>();
    }

    public enum AppraisalCycleStatus { Planned, ObjectiveSetting, InProgress, Moderation, Closed }

    /// <summary>
    /// One person's appraisal for one cycle.
    /// <para>
    /// The self-assessment is captured before the manager's, and both are kept. Where they disagree,
    /// the disagreement is the useful part — and if the appraisal is ever relied on to justify a
    /// dismissal for poor performance, an employee's own recorded account of their year is what
    /// stops the exercise looking like it was written backwards from the outcome.
    /// </para>
    /// </summary>
    public class Appraisal
    {
        public int Id { get; set; }

        [NotMapped] public string Reference => $"APR-{Id:D5}";

        public int CycleId { get; set; }
        [ValidateNever] public AppraisalCycle? Cycle { get; set; }

        public int EmployeeId { get; set; }
        [ValidateNever] public Employee? Employee { get; set; }

        /// <summary>The manager doing the review, captured at the time — managers change.</summary>
        public int? ReviewerId { get; set; }
        [ValidateNever] public Employee? Reviewer { get; set; }

        public AppraisalStatus Status { get; set; } = AppraisalStatus.NotStarted;

        // ── Self-assessment ──────────────────────────────────────────────────────
        [StringLength(8000)]
        [Display(Name = "What went well")]
        public string? SelfAchievements { get; set; }

        [StringLength(8000)]
        [Display(Name = "What was difficult")]
        public string? SelfChallenges { get; set; }

        [StringLength(4000)]
        [Display(Name = "Support or development needed")]
        public string? SelfDevelopmentNeeds { get; set; }

        public DateTime? SelfAssessedAt { get; set; }

        // ── Manager's review ─────────────────────────────────────────────────────
        [StringLength(8000)]
        [Display(Name = "Reviewer's assessment")]
        public string? ReviewerComments { get; set; }

        [StringLength(4000)]
        [Display(Name = "Development plan")]
        public string? DevelopmentPlan { get; set; }

        /// <summary>
        /// The overall rating. Set from the objectives and competencies rather than instead of them —
        /// a rating with no scored evidence behind it is an opinion with a number on it.
        /// </summary>
        [Display(Name = "Overall rating")]
        public PerformanceRating? OverallRating { get; set; }

        [StringLength(2000)]
        [Display(Name = "Reasons for the rating")]
        public string? RatingReasons { get; set; }

        public DateTime? ReviewedAt { get; set; }

        // ── Moderation ───────────────────────────────────────────────────────────
        /// <summary>
        /// Moderation exists because two managers rating the same performance differently is normal.
        /// Where a rating is changed, the original is kept alongside it.
        /// </summary>
        [Display(Name = "Moderated rating")]
        public PerformanceRating? ModeratedRating { get; set; }

        [StringLength(2000)]
        [Display(Name = "Reasons for moderation")]
        public string? ModerationReasons { get; set; }

        public int? ModeratedById { get; set; }
        [ValidateNever] public User? ModeratedBy { get; set; }
        public DateTime? ModeratedAt { get; set; }

        // ── Sign-off ─────────────────────────────────────────────────────────────
        /// <summary>
        /// The employee acknowledges having seen it. Acknowledgement is not agreement, and the
        /// distinction matters — so a disagreement can be recorded without blocking the sign-off.
        /// </summary>
        [Display(Name = "Seen by the employee")]
        public bool EmployeeAcknowledged { get; set; }

        [StringLength(4000)]
        [Display(Name = "Employee's comments on the review")]
        public string? EmployeeComments { get; set; }

        [Display(Name = "Employee disagrees with the rating")]
        public bool EmployeeDisagrees { get; set; }

        public DateTime? AcknowledgedAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [ValidateNever] public ICollection<AppraisalObjective> Objectives { get; set; } = new List<AppraisalObjective>();

        // ── Derived ──────────────────────────────────────────────────────────────
        [NotMapped] public PerformanceRating? EffectiveRating => ModeratedRating ?? OverallRating;

        /// <summary>
        /// The weighted achievement across the objectives, as a percentage. Unscored objectives are
        /// left out rather than counted as zero, so a half-finished review does not read as failure.
        /// </summary>
        [NotMapped]
        public decimal? ObjectiveScore
        {
            get
            {
                var scored = Objectives.Where(o => o.AchievementPercent.HasValue).ToList();
                if (scored.Count == 0) return null;

                var weight = scored.Sum(o => o.Weight);
                if (weight == 0) return null;

                return Math.Round(scored.Sum(o => o.AchievementPercent!.Value * o.Weight) / weight, 1);
            }
        }

        [NotMapped]
        public bool IsComplete => Status == AppraisalStatus.Closed;
    }

    public enum AppraisalStatus
    {
        NotStarted,
        SelfAssessment,
        ReviewerAssessment,
        AwaitingModeration,
        AwaitingAcknowledgement,
        Closed
    }

    /// <summary>
    /// Five points, worded as descriptions rather than as marks out of five, because "3" means
    /// whatever the reader assumes and "met the standard" does not.
    /// </summary>
    public enum PerformanceRating
    {
        [Display(Name = "Did not meet the standard")] Unsatisfactory = 1,
        [Display(Name = "Partially met the standard")] Developing = 2,
        [Display(Name = "Met the standard")] Effective = 3,
        [Display(Name = "Exceeded the standard")] Strong = 4,
        [Display(Name = "Consistently well above the standard")] Outstanding = 5
    }

    /// <summary>
    /// One objective, with what it was measured by and what was achieved.
    /// <para>
    /// Objectives are set at the start of the cycle. An objective invented at review time is not an
    /// objective — it is a justification, and it is worth nothing if the appraisal is ever relied on.
    /// </para>
    /// </summary>
    public class AppraisalObjective
    {
        public int Id { get; set; }

        public int AppraisalId { get; set; }
        [ValidateNever] public Appraisal? Appraisal { get; set; }

        [Required, StringLength(300)]
        public string Title { get; set; } = string.Empty;

        [StringLength(2000)]
        [Display(Name = "What success looks like")]
        public string? SuccessMeasure { get; set; }

        [Range(0, 100)]
        [Display(Name = "Weight (%)")]
        public decimal Weight { get; set; } = 20;

        [DataType(DataType.Date)]
        [Display(Name = "Target date")]
        public DateTime? TargetDate { get; set; }

        [Range(0, 150)]
        [Display(Name = "Achieved (%)")]
        public decimal? AchievementPercent { get; set; }

        [StringLength(2000)]
        [Display(Name = "Evidence")]
        public string? Evidence { get; set; }

        [Display(Name = "Set at the start of the cycle")]
        public bool AgreedUpFront { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public int DisplayOrder { get; set; }
    }

    /// <summary>
    /// A performance improvement plan.
    /// <para>
    /// Section 12B of the Labour Act [Chapter 28:01] treats a dismissal as unfair unless the employee
    /// was told the standard they were required to meet, given a reasonable opportunity to meet it,
    /// and failed to do so. A PIP is how an employer shows all three. The module therefore requires
    /// the standard in writing, a review date far enough out to be a real opportunity, and a recorded
    /// outcome — because a plan with no review is not an opportunity, it is a formality.
    /// </para>
    /// </summary>
    public class PerformanceImprovementPlan
    {
        public int Id { get; set; }

        [NotMapped] public string Reference => $"PIP-{Id:D5}";

        public int EmployeeId { get; set; }
        [ValidateNever] public Employee? Employee { get; set; }

        public int? AppraisalId { get; set; }
        [ValidateNever] public Appraisal? Appraisal { get; set; }

        public int? ManagerId { get; set; }
        [ValidateNever] public Employee? Manager { get; set; }

        [Required, StringLength(4000)]
        [Display(Name = "The shortfall")]
        public string Concern { get; set; } = string.Empty;

        /// <summary>
        /// The standard the employee has to reach, stated so plainly that whether it was met is a
        /// question of fact rather than of opinion.
        /// </summary>
        [Required, StringLength(4000)]
        [Display(Name = "The standard required")]
        public string RequiredStandard { get; set; } = string.Empty;

        [StringLength(4000)]
        [Display(Name = "Support the employer will give")]
        public string? SupportOffered { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Starts")]
        public DateTime StartDate { get; set; } = DateTime.Today;

        [DataType(DataType.Date)]
        [Display(Name = "Reviewed on")]
        public DateTime ReviewDate { get; set; } = DateTime.Today.AddDays(60);

        public PipStatus Status { get; set; } = PipStatus.Open;

        [StringLength(4000)]
        [Display(Name = "Outcome and reasons")]
        public string? Outcome { get; set; }

        public DateTime? ClosedAt { get; set; }

        [Display(Name = "Discussed with the employee")]
        public bool DiscussedWithEmployee { get; set; }

        [Display(Name = "Employee's comments")]
        [StringLength(4000)]
        public string? EmployeeComments { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // ── Derived ──────────────────────────────────────────────────────────────
        [NotMapped] public int DaysAllowed => (ReviewDate.Date - StartDate.Date).Days;

        /// <summary>
        /// Whether the period allowed could reasonably be called an opportunity to improve. Short
        /// plans are not forbidden — some shortfalls can be fixed in a fortnight — but a plan this
        /// short has to be justifiable on its own facts.
        /// </summary>
        [NotMapped] public bool IsShortNotice => DaysAllowed < 30;

        [NotMapped] public bool IsOverdue => Status == PipStatus.Open && ReviewDate < DateTime.Today;
    }

    public enum PipStatus
    {
        Open,
        /// <summary>The standard was met.</summary>
        Met,
        /// <summary>Not met, and the period was extended rather than escalated.</summary>
        Extended,
        /// <summary>Not met at the end of the period.</summary>
        NotMet,
        Withdrawn
    }
}
