using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace IT_Service_Management_System.Models.Ims
{
    // ── Training & Competency enumerations (ISO 9001 cl. 7.2 / ISO 27001 cl. 7.2) ──
    public enum TrainingType { Induction, Internal, External, Online, Certification, Toolbox, Awareness }
    public enum AttendanceStatus { Enrolled, Attended, Completed, Failed, NoShow, Cancelled }
    public enum CompetencyLevel { None, Basic, Intermediate, Advanced, Expert }

    /// <summary>A training course/programme. May be linked to a controlled document (e.g. a policy the training covers).</summary>
    public class TrainingCourse
    {
        public int Id { get; set; }

        [NotMapped] public string Reference => $"TRN-{Id:D5}";

        [Required, StringLength(200)]
        public string Title { get; set; } = string.Empty;

        public TrainingType Type { get; set; } = TrainingType.Internal;
        public IsoStandard Standard { get; set; } = IsoStandard.Both;

        [StringLength(2000)]
        public string? Description { get; set; }

        [StringLength(200)]
        public string? Provider { get; set; }

        [Range(0, 1000), Display(Name = "Duration (hours)")]
        public decimal DurationHours { get; set; }

        [Display(Name = "Linked Policy / Document")]
        public int? LinkedDocumentId { get; set; }
        [ValidateNever] public IsoDocument? LinkedDocument { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public int? CreatedById { get; set; }
        [ValidateNever] public User? CreatedBy { get; set; }

        [ValidateNever] public ICollection<TrainingRecord> Records { get; set; } = new List<TrainingRecord>();
    }

    /// <summary>An individual's attendance/completion record for a course, with certificate &amp; expiry tracking.</summary>
    public class TrainingRecord
    {
        public int Id { get; set; }

        public int TrainingCourseId { get; set; }
        [ValidateNever] public TrainingCourse? TrainingCourse { get; set; }

        public int UserId { get; set; }
        [ValidateNever] public User? User { get; set; }

        [Display(Name = "Scheduled Date"), DataType(DataType.Date)]
        public DateTime? ScheduledDate { get; set; }
        [Display(Name = "Completed Date"), DataType(DataType.Date)]
        public DateTime? CompletedDate { get; set; }

        public AttendanceStatus Status { get; set; } = AttendanceStatus.Enrolled;

        [Range(0, 100)]
        public int? Score { get; set; }

        [StringLength(200), Display(Name = "Certificate")]
        public string? CertificateName { get; set; }
        [Display(Name = "Certificate Expiry"), DataType(DataType.Date)]
        public DateTime? CertificateExpiry { get; set; }

        [StringLength(1000)]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [NotMapped] public bool IsCertificateExpired =>
            CertificateExpiry.HasValue && CertificateExpiry.Value.Date < DateTime.Now.Date;
        [NotMapped] public bool IsCertificateExpiringSoon =>
            CertificateExpiry.HasValue && CertificateExpiry.Value.Date >= DateTime.Now.Date
            && CertificateExpiry.Value.Date <= DateTime.Now.Date.AddDays(60);
    }

    /// <summary>A competency/skill that can be assessed against employees (competency matrix).</summary>
    public class Competency
    {
        public int Id { get; set; }

        [Required, StringLength(150)]
        public string Name { get; set; } = string.Empty;

        [StringLength(1000)]
        public string? Description { get; set; }

        [StringLength(100)]
        public string? Category { get; set; }

        public bool IsActive { get; set; } = true;

        [ValidateNever] public ICollection<UserCompetency> Assessments { get; set; } = new List<UserCompetency>();
    }

    /// <summary>An employee's assessed level for a competency, with optional expiry (revalidation).</summary>
    public class UserCompetency
    {
        public int Id { get; set; }

        public int CompetencyId { get; set; }
        [ValidateNever] public Competency? Competency { get; set; }

        public int UserId { get; set; }
        [ValidateNever] public User? User { get; set; }

        public CompetencyLevel Level { get; set; } = CompetencyLevel.None;

        [Display(Name = "Required Level")]
        public CompetencyLevel RequiredLevel { get; set; } = CompetencyLevel.Basic;

        [DataType(DataType.Date)]
        public DateTime? AssessedDate { get; set; }
        public int? AssessedById { get; set; }
        [ValidateNever] public User? AssessedBy { get; set; }

        [DataType(DataType.Date)]
        public DateTime? ExpiryDate { get; set; }

        [StringLength(1000)]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [NotMapped] public bool MeetsRequirement => Level >= RequiredLevel;
    }
}
