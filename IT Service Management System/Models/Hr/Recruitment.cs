using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace IT_Service_Management_System.Models.Hr
{
    /// <summary>
    /// A request to fill a post, raised before anything is advertised.
    /// <para>
    /// The requisition exists so that headcount is approved before a candidate is ever spoken to.
    /// Recruiting first and seeking approval afterwards is how an organisation ends up with an offer
    /// it cannot fund and a candidate it has to disappoint.
    /// </para>
    /// </summary>
    public class JobRequisition
    {
        public int Id { get; set; }

        [NotMapped] public string Reference => $"REQ-{Id:D5}";

        [Required, StringLength(200)]
        [Display(Name = "Job title")]
        public string JobTitle { get; set; } = string.Empty;

        public int? DepartmentId { get; set; }
        [ValidateNever] public Department? Department { get; set; }

        [StringLength(60)] public string? Grade { get; set; }
        [StringLength(120)] public string? Location { get; set; }

        [Display(Name = "Positions to fill")]
        [Range(1, 500)]
        public int Positions { get; set; } = 1;

        [Display(Name = "Employment type")]
        public EmploymentType EmploymentType { get; set; } = EmploymentType.Permanent;

        /// <summary>
        /// A fixed-term contract needs an end date. Rolling short contracts for work that is
        /// permanent in substance is a well-worn route to a dispute.
        /// </summary>
        [DataType(DataType.Date)]
        [Display(Name = "Contract ends")]
        public DateTime? ContractEndDate { get; set; }

        [Display(Name = "Replacement for")]
        public int? ReplacingEmployeeId { get; set; }
        [ValidateNever] public Employee? ReplacingEmployee { get; set; }

        [Display(Name = "Reporting to")]
        public int? ReportsToEmployeeId { get; set; }
        [ValidateNever] public Employee? ReportsToEmployee { get; set; }

        [StringLength(4000)]
        [Display(Name = "Purpose of the post")]
        public string? Purpose { get; set; }

        /// <summary>
        /// What the job actually requires. This is the yardstick every shortlisting and interview
        /// decision is measured against, so it is written before anyone applies rather than after.
        /// </summary>
        [StringLength(4000)]
        [Display(Name = "Essential requirements")]
        public string? EssentialRequirements { get; set; }

        [StringLength(4000)]
        [Display(Name = "Desirable requirements")]
        public string? DesirableRequirements { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Budgeted salary (monthly, minimum)")]
        public decimal? SalaryMin { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Budgeted salary (monthly, maximum)")]
        public decimal? SalaryMax { get; set; }

        [StringLength(3)] public string Currency { get; set; } = "USD";

        public RequisitionStatus Status { get; set; } = RequisitionStatus.Draft;

        [DataType(DataType.Date)]
        [Display(Name = "Needed by")]
        public DateTime? RequiredByDate { get; set; }

        public int RaisedById { get; set; }
        [ValidateNever] public User? RaisedBy { get; set; }

        public int? ApprovedById { get; set; }
        [ValidateNever] public User? ApprovedBy { get; set; }
        public DateTime? ApprovedAt { get; set; }

        [StringLength(1000)]
        [Display(Name = "Approval or rejection note")]
        public string? DecisionNote { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }
        public bool IsDeleted { get; set; }

        [ValidateNever] public ICollection<Vacancy> Vacancies { get; set; } = new List<Vacancy>();

        [NotMapped]
        public string SalaryBand => SalaryMin.HasValue || SalaryMax.HasValue
            ? $"{Currency} {SalaryMin:N0}–{SalaryMax:N0}"
            : "Not stated";
    }

    public enum RequisitionStatus { Draft, PendingApproval, Approved, Rejected, Advertised, Filled, Cancelled }

    /// <summary>
    /// An advertised vacancy against an approved requisition.
    /// <para>
    /// Section 5 of the Labour Act [Chapter 28:01] makes it unlawful to discriminate in advertising a
    /// vacancy or in recruitment on grounds of race, tribe, place of origin, political opinion,
    /// colour, creed, gender, pregnancy, HIV/AIDS status or disability. The advert text is stored so
    /// that what was published can be produced later, and the selection criteria are stored
    /// separately so decisions can be shown to have been made against the job, not the person.
    /// </para>
    /// </summary>
    public class Vacancy
    {
        public int Id { get; set; }

        [NotMapped] public string Reference => $"VAC-{Id:D5}";

        public int RequisitionId { get; set; }
        [ValidateNever] public JobRequisition? Requisition { get; set; }

        [Required, StringLength(200)]
        [Display(Name = "Advertised title")]
        public string Title { get; set; } = string.Empty;

        [Required, StringLength(8000)]
        [Display(Name = "Advertisement")]
        public string AdvertText { get; set; } = string.Empty;

        [Display(Name = "Open to internal applicants")]
        public bool OpenToInternal { get; set; } = true;

        [Display(Name = "Open to external applicants")]
        public bool OpenToExternal { get; set; } = true;

        [DataType(DataType.Date)]
        [Display(Name = "Opens")]
        public DateTime OpenDate { get; set; } = DateTime.Today;

        [DataType(DataType.Date)]
        [Display(Name = "Closes")]
        public DateTime CloseDate { get; set; } = DateTime.Today.AddDays(14);

        [StringLength(500)]
        [Display(Name = "Where it was advertised")]
        public string? AdvertisedIn { get; set; }

        public VacancyStatus Status { get; set; } = VacancyStatus.Open;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public bool IsDeleted { get; set; }

        [ValidateNever] public ICollection<JobApplication> Applications { get; set; } = new List<JobApplication>();
        [ValidateNever] public ICollection<SelectionCriterion> Criteria { get; set; } = new List<SelectionCriterion>();

        [NotMapped] public bool IsAcceptingApplications =>
            Status == VacancyStatus.Open && DateTime.Today >= OpenDate && DateTime.Today <= CloseDate;

        [NotMapped] public int DaysRemaining => (CloseDate.Date - DateTime.Today).Days;
    }

    public enum VacancyStatus { Draft, Open, Closed, Shortlisting, Interviewing, OfferMade, Filled, Cancelled }

    /// <summary>
    /// One thing the post is scored on, with the weight it carries.
    /// <para>
    /// Criteria are set before applications are seen. Deciding what mattered after meeting the
    /// candidates is how unlawful grounds get in without anyone intending it.
    /// </para>
    /// </summary>
    public class SelectionCriterion
    {
        public int Id { get; set; }

        public int VacancyId { get; set; }
        [ValidateNever] public Vacancy? Vacancy { get; set; }

        [Required, StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [StringLength(1000)]
        [Display(Name = "What good looks like")]
        public string? Descriptor { get; set; }

        [Range(1, 10)]
        public int Weight { get; set; } = 1;

        [Display(Name = "Essential")]
        public bool IsEssential { get; set; } = true;

        public int DisplayOrder { get; set; }

        [ValidateNever] public ICollection<CandidateScore> Scores { get; set; } = new List<CandidateScore>();
    }

    /// <summary>
    /// An application against a vacancy.
    /// <para>
    /// Note what is deliberately absent: date of birth, marital status, number of children, church,
    /// home language, political affiliation and health status. None of them bear on whether someone
    /// can do the job, and holding them makes it harder to show a decision was not tainted by them.
    /// Work-permit status is asked because the Immigration Act requires it of a non-citizen, and it
    /// is asked as one yes/no rather than as nationality.
    /// </para>
    /// </summary>
    public class JobApplication
    {
        public int Id { get; set; }

        [NotMapped] public string Reference => $"APP-{Id:D5}";

        public int VacancyId { get; set; }
        [ValidateNever] public Vacancy? Vacancy { get; set; }

        [Required, StringLength(100)]
        [Display(Name = "First name")]
        public string FirstName { get; set; } = string.Empty;

        [Required, StringLength(100)]
        [Display(Name = "Surname")]
        public string LastName { get; set; } = string.Empty;

        [StringLength(150)]
        [EmailAddress]
        public string? Email { get; set; }

        [StringLength(40)]
        [Display(Name = "Phone")]
        public string? Phone { get; set; }

        [StringLength(60)]
        [Display(Name = "National ID")]
        public string? NationalId { get; set; }

        /// <summary>
        /// An internal applicant is linked to their employee record so their service and record are
        /// visible to the panel without being re-keyed.
        /// </summary>
        public int? EmployeeId { get; set; }
        [ValidateNever] public Employee? Employee { get; set; }

        [Display(Name = "Entitled to work in Zimbabwe without a permit")]
        public bool EntitledToWork { get; set; } = true;

        [StringLength(200)]
        [Display(Name = "Highest qualification")]
        public string? HighestQualification { get; set; }

        [Display(Name = "Years of relevant experience")]
        [Range(0, 60)]
        public int YearsExperience { get; set; }

        [StringLength(4000)]
        [Display(Name = "Covering statement")]
        public string? CoveringStatement { get; set; }

        [StringLength(260)] public string? CvFileName { get; set; }
        [StringLength(500)] public string? CvFilePath { get; set; }

        public ApplicationStatus Status { get; set; } = ApplicationStatus.Received;

        /// <summary>
        /// Why an application went no further. Required on rejection, because a rejection nobody can
        /// explain is a rejection nobody can defend.
        /// </summary>
        [StringLength(2000)]
        [Display(Name = "Reason for the decision")]
        public string? DecisionReason { get; set; }

        public DateTime ReceivedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }
        public bool IsDeleted { get; set; }

        [ValidateNever] public ICollection<CandidateScore> Scores { get; set; } = new List<CandidateScore>();
        [ValidateNever] public ICollection<CandidateInterview> Interviews { get; set; } = new List<CandidateInterview>();

        [NotMapped] public string FullName => $"{FirstName} {LastName}".Trim();
        [NotMapped] public bool IsInternal => EmployeeId.HasValue;

        [NotMapped]
        public bool IsRejected => Status is ApplicationStatus.NotShortlisted
            or ApplicationStatus.Unsuccessful or ApplicationStatus.Withdrawn or ApplicationStatus.OfferDeclined;
    }

    public enum ApplicationStatus
    {
        Received,
        Screened,
        Shortlisted,
        NotShortlisted,
        Interviewed,
        ReferenceCheck,
        OfferMade,
        OfferAccepted,
        OfferDeclined,
        Unsuccessful,
        Withdrawn,
        Hired
    }

    /// <summary>A score against one criterion, with the note that justifies it.</summary>
    public class CandidateScore
    {
        public int Id { get; set; }

        public int ApplicationId { get; set; }
        [ValidateNever] public JobApplication? Application { get; set; }

        public int CriterionId { get; set; }
        [ValidateNever] public SelectionCriterion? Criterion { get; set; }

        /// <summary>Optional — set when the score came from an interview rather than from screening.</summary>
        public int? InterviewId { get; set; }
        [ValidateNever] public CandidateInterview? Interview { get; set; }

        [Range(0, 5)]
        public int Score { get; set; }

        [StringLength(1000)]
        [Display(Name = "Evidence for the score")]
        public string? Comment { get; set; }

        public int? ScoredById { get; set; }
        [ValidateNever] public User? ScoredBy { get; set; }

        public DateTime ScoredAt { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// An interview. The panel is recorded because a one-person panel is hard to defend, and the
    /// recommendation is separated from the score so the reasoning survives the arithmetic.
    /// </summary>
    public class CandidateInterview
    {
        public int Id { get; set; }

        public int ApplicationId { get; set; }
        [ValidateNever] public JobApplication? Application { get; set; }

        public InterviewStage Stage { get; set; } = InterviewStage.First;

        [Display(Name = "Scheduled for")]
        public DateTime ScheduledFor { get; set; } = DateTime.Now.Date.AddDays(3).AddHours(9);

        [StringLength(200)]
        public string? Venue { get; set; }

        [StringLength(500)]
        [Display(Name = "Panel")]
        public string? Panel { get; set; }

        public bool Held { get; set; }
        public bool CandidateAttended { get; set; }

        [StringLength(8000)]
        [Display(Name = "Panel notes")]
        public string? Notes { get; set; }

        [StringLength(2000)]
        [Display(Name = "Recommendation")]
        public string? Recommendation { get; set; }

        public InterviewOutcome? Outcome { get; set; }

        public int? ArrangedById { get; set; }
        [ValidateNever] public User? ArrangedBy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [ValidateNever] public ICollection<CandidateScore> Scores { get; set; } = new List<CandidateScore>();
    }

    public enum InterviewStage { Screening, First, Second, Panel, Technical, Final }

    public enum InterviewOutcome { Progress, Hold, Reject }

    /// <summary>
    /// An offer of employment.
    /// <para>
    /// The Labour Act requires an employee to be given written particulars of employment. The offer
    /// carries the terms that will become the contract, so they are recorded here and carried into
    /// the employee record on acceptance rather than retyped.
    /// </para>
    /// </summary>
    public class JobOffer
    {
        public int Id { get; set; }

        [NotMapped] public string Reference => $"OFF-{Id:D5}";

        public int ApplicationId { get; set; }
        [ValidateNever] public JobApplication? Application { get; set; }

        [Required, StringLength(200)]
        [Display(Name = "Job title")]
        public string JobTitle { get; set; } = string.Empty;

        [StringLength(60)] public string? Grade { get; set; }
        [StringLength(120)] public string? Location { get; set; }

        [Display(Name = "Employment type")]
        public EmploymentType EmploymentType { get; set; } = EmploymentType.Permanent;

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Basic salary (monthly)")]
        public decimal BasicSalary { get; set; }

        [StringLength(3)] public string Currency { get; set; } = "USD";

        [DataType(DataType.Date)]
        [Display(Name = "Start date")]
        public DateTime StartDate { get; set; } = DateTime.Today.AddDays(30);

        /// <summary>
        /// Probation, where the contract provides for it. The Labour Act limits probation to three
        /// months for a permanent contract and one month for a casual or seasonal one, and it can
        /// only be served once with the same employer for the same class of work.
        /// </summary>
        [Display(Name = "Probation (months)")]
        [Range(0, 6)]
        public int ProbationMonths { get; set; } = 3;

        [DataType(DataType.Date)]
        [Display(Name = "Fixed term ends")]
        public DateTime? ContractEndDate { get; set; }

        [StringLength(4000)]
        [Display(Name = "Other terms")]
        public string? OtherTerms { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Offer expires")]
        public DateTime? ExpiryDate { get; set; }

        public OfferStatus Status { get; set; } = OfferStatus.Draft;

        [DataType(DataType.Date)] public DateTime? IssuedDate { get; set; }
        [DataType(DataType.Date)] public DateTime? RespondedDate { get; set; }

        [StringLength(2000)]
        [Display(Name = "Candidate's response")]
        public string? ResponseNote { get; set; }

        /// <summary>The employee record created when the offer was accepted, if one was.</summary>
        public int? CreatedEmployeeId { get; set; }
        [ValidateNever] public Employee? CreatedEmployee { get; set; }

        public int? IssuedById { get; set; }
        [ValidateNever] public User? IssuedBy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [NotMapped]
        public DateTime? ProbationEndDate =>
            ProbationMonths > 0 ? StartDate.AddMonths(ProbationMonths) : null;

        [NotMapped]
        public bool HasLapsed => Status == OfferStatus.Issued
            && ExpiryDate.HasValue && DateTime.Today > ExpiryDate.Value.Date;
    }

    public enum OfferStatus { Draft, Issued, Accepted, Declined, Withdrawn, Lapsed }
}
