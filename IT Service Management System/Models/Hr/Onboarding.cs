using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace IT_Service_Management_System.Models.Hr
{
    /// <summary>
    /// A reusable list of what has to happen when someone joins.
    /// <para>
    /// Templates exist because the statutory steps are the same every time and are the ones most
    /// easily forgotten — a new joiner has to be registered with NSSA, given written particulars of
    /// employment, given the code of conduct they will be disciplined under, and inducted on safety.
    /// None of those are optional, and none of them should depend on someone remembering.
    /// </para>
    /// </summary>
    public class OnboardingTemplate
    {
        public int Id { get; set; }

        [Required, StringLength(150)]
        public string Name { get; set; } = string.Empty;

        [StringLength(1000)]
        public string? Description { get; set; }

        /// <summary>
        /// Applies to one employment type, or to all where null. A contractor's induction is not a
        /// permanent employee's.
        /// </summary>
        [Display(Name = "Applies to")]
        public EmploymentType? AppliesTo { get; set; }

        public bool IsActive { get; set; } = true;
        public bool IsDefault { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [ValidateNever] public ICollection<OnboardingTaskTemplate> Tasks { get; set; } = new List<OnboardingTaskTemplate>();
    }

    /// <summary>One step on a template.</summary>
    public class OnboardingTaskTemplate
    {
        public int Id { get; set; }

        public int TemplateId { get; set; }
        [ValidateNever] public OnboardingTemplate? Template { get; set; }

        [Required, StringLength(250)]
        public string Title { get; set; } = string.Empty;

        [StringLength(2000)]
        public string? Detail { get; set; }

        public OnboardingCategory Category { get; set; } = OnboardingCategory.Administration;

        /// <summary>Which side of the organisation owns the step.</summary>
        public OnboardingOwner Owner { get; set; } = OnboardingOwner.Hr;

        /// <summary>
        /// Days from the start date. Negative for anything that must happen before day one — a
        /// contract signed on the first morning is a contract signed late.
        /// </summary>
        [Display(Name = "Due (days from start)")]
        public int DueDayOffset { get; set; }

        /// <summary>
        /// A step required by law rather than by policy. Statutory steps cannot be dropped from a
        /// programme, and an overdue one is reported separately from an overdue policy step.
        /// </summary>
        [Display(Name = "Required by law")]
        public bool IsStatutory { get; set; }

        /// <summary>The provision the step comes from, so the requirement can be checked.</summary>
        [StringLength(250)]
        public string? Authority { get; set; }

        public int DisplayOrder { get; set; }
    }

    public enum OnboardingCategory
    {
        Contract,
        StatutoryRegistration,
        Payroll,
        Induction,
        Safety,
        Equipment,
        SystemAccess,
        Training,
        Administration
    }

    public enum OnboardingOwner { Hr, LineManager, Finance, It, Employee, Safety }

    /// <summary>
    /// One person's onboarding, built from a template at the point of hire.
    /// <para>
    /// The steps are copied rather than referenced, so changing a template later does not rewrite
    /// what someone was actually asked to do. What happened to a joiner in March is what the record
    /// should still say in December.
    /// </para>
    /// </summary>
    public class OnboardingProgramme
    {
        public int Id { get; set; }

        [NotMapped] public string Reference => $"ONB-{Id:D5}";

        public int EmployeeId { get; set; }
        [ValidateNever] public Employee? Employee { get; set; }

        public int? TemplateId { get; set; }
        [ValidateNever] public OnboardingTemplate? Template { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Start date")]
        public DateTime StartDate { get; set; } = DateTime.Today;

        /// <summary>Who is looking after the joiner day to day.</summary>
        public int? BuddyId { get; set; }
        [ValidateNever] public Employee? Buddy { get; set; }

        public OnboardingStatus Status { get; set; } = OnboardingStatus.NotStarted;

        [StringLength(2000)]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? CompletedAt { get; set; }

        [ValidateNever] public ICollection<OnboardingTask> Tasks { get; set; } = new List<OnboardingTask>();

        // ── Derived ──────────────────────────────────────────────────────────────
        [NotMapped] public int Total => Tasks.Count;
        [NotMapped] public int Done => Tasks.Count(t => t.IsComplete);

        [NotMapped]
        public int PercentComplete => Total == 0 ? 0 : (int)Math.Round(Done * 100.0 / Total);

        /// <summary>
        /// Statutory steps still outstanding. Reported on its own because a missed safety induction
        /// or an unregistered employee is a different kind of problem from an unissued laptop.
        /// </summary>
        [NotMapped]
        public List<OnboardingTask> OutstandingStatutory =>
            Tasks.Where(t => t.IsStatutory && !t.IsComplete).ToList();

        [NotMapped]
        public List<OnboardingTask> Overdue =>
            Tasks.Where(t => !t.IsComplete && t.DueDate < DateTime.Today).ToList();
    }

    public enum OnboardingStatus { NotStarted, InProgress, Complete, Abandoned }

    /// <summary>A step on one person's programme.</summary>
    public class OnboardingTask
    {
        public int Id { get; set; }

        public int ProgrammeId { get; set; }
        [ValidateNever] public OnboardingProgramme? Programme { get; set; }

        [Required, StringLength(250)]
        public string Title { get; set; } = string.Empty;

        [StringLength(2000)]
        public string? Detail { get; set; }

        public OnboardingCategory Category { get; set; } = OnboardingCategory.Administration;
        public OnboardingOwner Owner { get; set; } = OnboardingOwner.Hr;

        [DataType(DataType.Date)]
        [Display(Name = "Due")]
        public DateTime DueDate { get; set; } = DateTime.Today;

        [Display(Name = "Required by law")]
        public bool IsStatutory { get; set; }

        [StringLength(250)]
        public string? Authority { get; set; }

        public bool IsComplete { get; set; }
        public DateTime? CompletedAt { get; set; }

        public int? CompletedById { get; set; }
        [ValidateNever] public User? CompletedBy { get; set; }

        /// <summary>
        /// What was done, or the reference of what was issued. A tick on its own proves nothing —
        /// "registered, NSSA reference 123456" proves something.
        /// </summary>
        [StringLength(1000)]
        [Display(Name = "Evidence")]
        public string? Evidence { get; set; }

        public int DisplayOrder { get; set; }

        [NotMapped] public bool IsOverdue => !IsComplete && DueDate < DateTime.Today;
    }
}
