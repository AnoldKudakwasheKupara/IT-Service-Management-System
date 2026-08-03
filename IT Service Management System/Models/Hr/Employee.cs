using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace IT_Service_Management_System.Models.Hr
{
    /// <summary>
    /// The employee master record — the single source of truth for who somebody is, what they do
    /// and when they joined.
    /// <para>
    /// This is deliberately separate from <see cref="User"/>. A <see cref="User"/> is a login
    /// account: credentials, role, MFA. An <see cref="Employee"/> is a person on the payroll, and
    /// the two are not the same set — a contractor may have an account with no employee record, and
    /// a leaver keeps an employee record long after their account is deactivated. Every HR module
    /// points at this record rather than re-typing a name, so one person's history stays joined up.
    /// </para>
    /// </summary>
    public class Employee
    {
        public int Id { get; set; }

        /// <summary>Payroll / staff number. Unique, and the identifier people actually quote.</summary>
        [Required, StringLength(30)]
        [Display(Name = "Employee number")]
        public string EmployeeNumber { get; set; } = string.Empty;

        /// <summary>The login account, when the employee has one. Null for staff without system access.</summary>
        public int? UserId { get; set; }
        [ValidateNever] public User? User { get; set; }

        // ── Identity ─────────────────────────────────────────────────────────────
        [Required, StringLength(60)]
        public string FirstName { get; set; } = string.Empty;

        [Required, StringLength(60)]
        public string LastName { get; set; } = string.Empty;

        [StringLength(60)]
        public string? MiddleName { get; set; }

        /// <summary>Name the person prefers to be called, when it differs from their first name.</summary>
        [StringLength(60)]
        public string? PreferredName { get; set; }

        [StringLength(20)]
        public string? Gender { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Date of birth")]
        public DateTime? DateOfBirth { get; set; }

        /// <summary>National ID / passport number. Restricted — only HR and administrators see it.</summary>
        [StringLength(60)]
        [Display(Name = "National ID")]
        public string? NationalId { get; set; }

        [StringLength(60)]
        public string? Nationality { get; set; }

        [StringLength(30)]
        [Display(Name = "Marital status")]
        public string? MaritalStatus { get; set; }

        // ── Contact ──────────────────────────────────────────────────────────────
        [EmailAddress, StringLength(200)]
        [Display(Name = "Work email")]
        public string? WorkEmail { get; set; }

        [EmailAddress, StringLength(200)]
        [Display(Name = "Personal email")]
        public string? PersonalEmail { get; set; }

        [Phone, StringLength(30)]
        [Display(Name = "Mobile")]
        public string? MobileNumber { get; set; }

        [Phone, StringLength(30)]
        [Display(Name = "Work telephone")]
        public string? WorkNumber { get; set; }

        [StringLength(400)]
        [Display(Name = "Residential address")]
        public string? Address { get; set; }

        [StringLength(100)]
        public string? City { get; set; }

        [StringLength(100)]
        public string? Country { get; set; }

        // ── Emergency contact ────────────────────────────────────────────────────
        [StringLength(120)]
        [Display(Name = "Next of kin")]
        public string? NextOfKinName { get; set; }

        [StringLength(60)]
        [Display(Name = "Relationship")]
        public string? NextOfKinRelationship { get; set; }

        [Phone, StringLength(30)]
        [Display(Name = "Next-of-kin phone")]
        public string? NextOfKinPhone { get; set; }

        [StringLength(120)]
        [Display(Name = "Emergency contact")]
        public string? EmergencyContactName { get; set; }

        [Phone, StringLength(30)]
        [Display(Name = "Emergency phone")]
        public string? EmergencyContactPhone { get; set; }

        // ── Employment ───────────────────────────────────────────────────────────
        [Required, StringLength(120)]
        [Display(Name = "Job title")]
        public string JobTitle { get; set; } = string.Empty;

        public int? DepartmentId { get; set; }
        [ValidateNever] public Department? Department { get; set; }

        /// <summary>The employee's line manager, as an employee rather than an account.</summary>
        public int? ManagerId { get; set; }
        [ValidateNever] public Employee? Manager { get; set; }
        [ValidateNever] public ICollection<Employee> DirectReports { get; set; } = new List<Employee>();

        public EmploymentType EmploymentType { get; set; } = EmploymentType.Permanent;

        public EmploymentStatus Status { get; set; } = EmploymentStatus.Active;

        [StringLength(60)]
        public string? Grade { get; set; }

        [StringLength(120)]
        [Display(Name = "Work location")]
        public string? Location { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Hire date")]
        public DateTime? HireDate { get; set; }

        /// <summary>End of probation. Used to surface people whose confirmation is due.</summary>
        [DataType(DataType.Date)]
        [Display(Name = "Probation ends")]
        public DateTime? ProbationEndDate { get; set; }

        /// <summary>Contract expiry, for fixed-term and contractor engagements.</summary>
        [DataType(DataType.Date)]
        [Display(Name = "Contract ends")]
        public DateTime? ContractEndDate { get; set; }

        /// <summary>Last working day. Set when the employee leaves.</summary>
        [DataType(DataType.Date)]
        [Display(Name = "Termination date")]
        public DateTime? TerminationDate { get; set; }

        [StringLength(200)]
        [Display(Name = "Reason for leaving")]
        public string? TerminationReason { get; set; }

        /// <summary>Client or engagement the employee is billed to, for outsourced staff.</summary>
        [StringLength(200)]
        public string? Client { get; set; }

        // ── Payroll (bank details are deliberately the only financial fields held here) ──
        [StringLength(120)]
        [Display(Name = "Bank name")]
        public string? BankName { get; set; }

        [StringLength(60)]
        [Display(Name = "Bank account number")]
        public string? BankAccountNumber { get; set; }

        [StringLength(60)]
        [Display(Name = "Tax number")]
        public string? TaxNumber { get; set; }

        [StringLength(60)]
        [Display(Name = "Social security number")]
        public string? SocialSecurityNumber { get; set; }

        // ── Housekeeping ─────────────────────────────────────────────────────────
        [StringLength(2000)]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }

        /// <summary>Soft delete. Employee records are never destroyed — statutory retention applies.</summary>
        public bool IsDeleted { get; set; }

        // ── Derived ──────────────────────────────────────────────────────────────
        [NotMapped]
        public string FullName => $"{FirstName} {LastName}";

        /// <summary>Preferred name when one is set, otherwise the legal first name.</summary>
        [NotMapped]
        public string DisplayName => string.IsNullOrWhiteSpace(PreferredName)
            ? FullName
            : $"{PreferredName} {LastName}";

        [NotMapped]
        public bool IsCurrentEmployee => Status is EmploymentStatus.Active or EmploymentStatus.OnProbation
            or EmploymentStatus.OnLeave or EmploymentStatus.Suspended;

        /// <summary>Completed years of service, to the termination date when the employee has left.</summary>
        [NotMapped]
        public double? YearsOfService
        {
            get
            {
                if (!HireDate.HasValue) return null;
                var end = TerminationDate ?? DateTime.Today;
                return Math.Round((end - HireDate.Value).TotalDays / 365.25, 1);
            }
        }

        /// <summary>Human-readable tenure, e.g. "3 years 2 months".</summary>
        [NotMapped]
        public string LengthOfService
        {
            get
            {
                if (!HireDate.HasValue) return "—";
                var end = TerminationDate ?? DateTime.Today;
                var months = (end.Year - HireDate.Value.Year) * 12 + end.Month - HireDate.Value.Month;
                if (end.Day < HireDate.Value.Day) months--;
                if (months < 0) return "—";
                var (y, m) = (months / 12, months % 12);
                if (y == 0) return $"{m} month{(m == 1 ? "" : "s")}";
                return m == 0 ? $"{y} year{(y == 1 ? "" : "s")}" : $"{y} year{(y == 1 ? "" : "s")} {m} month{(m == 1 ? "" : "s")}";
            }
        }

        [NotMapped]
        public int? Age => DateOfBirth.HasValue
            ? (int)((DateTime.Today - DateOfBirth.Value).TotalDays / 365.25)
            : null;

        /// <summary>Probation is running and its end date is within the next 30 days.</summary>
        [NotMapped]
        public bool ProbationDueSoon =>
            Status == EmploymentStatus.OnProbation && ProbationEndDate.HasValue
            && ProbationEndDate.Value.Date <= DateTime.Today.AddDays(30);

        /// <summary>A fixed-term contract expiring within the next 60 days.</summary>
        [NotMapped]
        public bool ContractExpiringSoon =>
            IsCurrentEmployee && ContractEndDate.HasValue
            && ContractEndDate.Value.Date <= DateTime.Today.AddDays(60);

        // ── Navigation into the HR process modules ───────────────────────────────
        [ValidateNever] public ICollection<TalentIdentification> TalentAssessments { get; set; } = new List<TalentIdentification>();
        [ValidateNever] public ICollection<ExitInterview> ExitInterviews { get; set; } = new List<ExitInterview>();
        [ValidateNever] public ICollection<EngagementStayInterview> StayInterviews { get; set; } = new List<EngagementStayInterview>();
    }

    /// <summary>The contractual basis on which somebody is engaged.</summary>
    public enum EmploymentType
    {
        Permanent,
        FixedTerm,
        Contractor,
        Intern,
        Temporary,
        Consultant,
        PartTime
    }

    /// <summary>Where the employee stands today. Drives headcount and turnover reporting.</summary>
    public enum EmploymentStatus
    {
        Active,
        OnProbation,
        OnLeave,
        Suspended,
        Resigned,
        Terminated,
        Retired,
        EndOfContract
    }
}
