using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace IT_Service_Management_System.Models.Ims
{
    /// <summary>
    /// Reference table grouping controlled documents (e.g. "Quality", "Information Security", "HR").
    /// Seeded with defaults; managed by the Document Controller.
    /// </summary>
    public class IsoDocumentCategory
    {
        public int Id { get; set; }

        [Required, StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required, StringLength(20), Display(Name = "Code")]
        public string Code { get; set; } = string.Empty;

        [StringLength(300)]
        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;

        [ValidateNever] public ICollection<IsoDocument> Documents { get; set; } = new List<IsoDocument>();
    }

    /// <summary>
    /// A controlled document (policy, procedure, work instruction, form, record, …). This is the header record;
    /// the actual file content lives on <see cref="IsoDocumentVersion"/> so history is never overwritten.
    /// </summary>
    public class IsoDocument : ISoftDelete
    {
        public int Id { get; set; }

        [Required, StringLength(40), Display(Name = "Document No.")]
        public string DocumentNumber { get; set; } = string.Empty;

        [Required, StringLength(250)]
        public string Title { get; set; } = string.Empty;

        [Display(Name = "Type")]
        public DocumentType Type { get; set; } = DocumentType.Policy;

        [Display(Name = "Category")]
        public int? CategoryId { get; set; }
        [ValidateNever] public IsoDocumentCategory? Category { get; set; }

        [Display(Name = "Department")]
        public int? DepartmentId { get; set; }
        [ValidateNever] public Department? Department { get; set; }

        [Display(Name = "Owner")]
        public int? OwnerId { get; set; }
        [ValidateNever] public User? Owner { get; set; }

        [Display(Name = "Approver")]
        public int? ApproverId { get; set; }
        [ValidateNever] public User? Approver { get; set; }

        [Display(Name = "ISO Standard")]
        public IsoStandard Standard { get; set; } = IsoStandard.Iso9001;

        [StringLength(30), Display(Name = "ISO Clause")]
        public string? IsoClause { get; set; }

        public DocumentClassification Classification { get; set; } = DocumentClassification.Internal;

        public DocumentStatus Status { get; set; } = DocumentStatus.Draft;

        [Display(Name = "Current Version")]
        [StringLength(20)]
        public string CurrentVersion { get; set; } = "0.1";

        [Display(Name = "Issue Date"), DataType(DataType.Date)]
        public DateTime? IssueDate { get; set; }

        [Display(Name = "Effective Date"), DataType(DataType.Date)]
        public DateTime? EffectiveDate { get; set; }

        [Display(Name = "Review Frequency")]
        public ReviewFrequency ReviewFrequency { get; set; } = ReviewFrequency.Annual;

        [Display(Name = "Next Review"), DataType(DataType.Date)]
        public DateTime? ReviewDate { get; set; }

        [Display(Name = "Expiry Date"), DataType(DataType.Date)]
        public DateTime? ExpiryDate { get; set; }

        [StringLength(400), Display(Name = "Keywords")]
        public string? Keywords { get; set; }

        [StringLength(2000)]
        public string? Summary { get; set; }

        /// <summary>Pointer to the version currently regarded as authoritative/published.</summary>
        public int? CurrentVersionId { get; set; }
        [ValidateNever] public IsoDocumentVersion? CurrentVersionRef { get; set; }

        public bool IsArchived { get; set; }
        public DateTime? ArchivedAt { get; set; }

        // Audit / soft delete (house convention)
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public int? CreatedById { get; set; }
        [ValidateNever] public User? CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? PublishedAt { get; set; }

        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }

        [ValidateNever] public ICollection<IsoDocumentVersion> Versions { get; set; } = new List<IsoDocumentVersion>();
        [ValidateNever] public ICollection<IsoDocumentApproval> Approvals { get; set; } = new List<IsoDocumentApproval>();
        [ValidateNever] public ICollection<IsoDocumentAcknowledgement> Acknowledgements { get; set; } = new List<IsoDocumentAcknowledgement>();
        [ValidateNever] public ICollection<IsoDocumentDistribution> Distributions { get; set; } = new List<IsoDocumentDistribution>();
        [ValidateNever] public ICollection<IsoDocumentReview> Reviews { get; set; } = new List<IsoDocumentReview>();

        [NotMapped] public bool IsPublished => Status == DocumentStatus.Published;
        [NotMapped] public bool IsInWorkflow =>
            Status is DocumentStatus.DepartmentReview or DocumentStatus.QualityReview or DocumentStatus.ManagementApproval;
        [NotMapped] public bool IsReviewDue => ReviewDate.HasValue && ReviewDate.Value.Date <= DateTime.Now.Date;
        [NotMapped] public bool IsExpired => ExpiryDate.HasValue && ExpiryDate.Value.Date < DateTime.Now.Date;
    }

    /// <summary>
    /// An immutable revision of a document. New versions are appended (never overwritten) so the full revision
    /// history and rollback are preserved. The file content is stored via the shared EFM document storage.
    /// </summary>
    public class IsoDocumentVersion
    {
        public int Id { get; set; }

        public int IsoDocumentId { get; set; }
        [ValidateNever] public IsoDocument? Document { get; set; }

        [Required, StringLength(20), Display(Name = "Version")]
        public string VersionNumber { get; set; } = "0.1";

        [StringLength(2000), Display(Name = "Revision Notes")]
        public string? RevisionNotes { get; set; }

        // Stored file (mirrors EFM version storage fields)
        [StringLength(260)] public string? StoredFileName { get; set; }
        [StringLength(260)] public string? OriginalFileName { get; set; }
        [StringLength(150)] public string? ContentType { get; set; }
        public long FileSize { get; set; }
        [StringLength(50)] public string? StorageProvider { get; set; }

        public DocumentStatus Status { get; set; } = DocumentStatus.Draft;
        public bool IsCurrent { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public int? CreatedById { get; set; }
        [ValidateNever] public User? CreatedBy { get; set; }

        public DateTime? ApprovedAt { get; set; }
        public int? ApprovedById { get; set; }
        [ValidateNever] public User? ApprovedBy { get; set; }

        [NotMapped] public bool HasFile => !string.IsNullOrEmpty(StoredFileName);
    }

    /// <summary>A single approval action within the document workflow (department / quality / management stage).</summary>
    public class IsoDocumentApproval
    {
        public int Id { get; set; }

        public int IsoDocumentId { get; set; }
        [ValidateNever] public IsoDocument? Document { get; set; }

        public int? IsoDocumentVersionId { get; set; }
        [ValidateNever] public IsoDocumentVersion? Version { get; set; }

        public ApprovalStage Stage { get; set; }
        public int Sequence { get; set; }

        public int? ApproverId { get; set; }
        [ValidateNever] public User? Approver { get; set; }
        [StringLength(60)] public string? ApproverRole { get; set; }

        public ApprovalDecision Decision { get; set; } = ApprovalDecision.Pending;

        [StringLength(1500)] public string? Comments { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? DecisionAt { get; set; }

        [NotMapped] public bool IsComplete => Decision != ApprovalDecision.Pending;
    }

    /// <summary>An employee's acknowledgement of a published document version — read / open / download / accept / sign.</summary>
    public class IsoDocumentAcknowledgement
    {
        public int Id { get; set; }

        public int IsoDocumentId { get; set; }
        [ValidateNever] public IsoDocument? Document { get; set; }

        public int? IsoDocumentVersionId { get; set; }
        [ValidateNever] public IsoDocumentVersion? Version { get; set; }

        public int UserId { get; set; }
        [ValidateNever] public User? User { get; set; }

        public AcknowledgementStatus Status { get; set; } = AcknowledgementStatus.Pending;

        public DateTime AssignedAt { get; set; } = DateTime.Now;
        public DateTime? OpenedAt { get; set; }
        public DateTime? DownloadedAt { get; set; }
        public DateTime? AcknowledgedAt { get; set; }

        /// <summary>The user typed their name as an electronic signature when acknowledging.</summary>
        public bool Accepted { get; set; }
        [StringLength(150), Display(Name = "Electronic Signature")]
        public string? SignatureName { get; set; }
        [StringLength(128)] public string? SignatureHash { get; set; }
        [StringLength(45)] public string? SignedIp { get; set; }

        [StringLength(1000)] public string? Comments { get; set; }

        [NotMapped] public bool IsAcknowledged => Status == AcknowledgementStatus.Acknowledged;
    }

    /// <summary>A distribution-list entry defining who must receive/acknowledge the document once published.</summary>
    public class IsoDocumentDistribution
    {
        public int Id { get; set; }

        public int IsoDocumentId { get; set; }
        [ValidateNever] public IsoDocument? Document { get; set; }

        public DistributionTargetType TargetType { get; set; } = DistributionTargetType.Department;

        public int? UserId { get; set; }
        [ValidateNever] public User? User { get; set; }

        public int? DepartmentId { get; set; }
        [ValidateNever] public Department? Department { get; set; }

        [StringLength(60)] public string? RoleName { get; set; }

        public bool RequiresAcknowledgement { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public int? CreatedById { get; set; }
    }

    /// <summary>A scheduled (or completed) periodic review of a document — supports review scheduling &amp; history.</summary>
    public class IsoDocumentReview
    {
        public int Id { get; set; }

        public int IsoDocumentId { get; set; }
        [ValidateNever] public IsoDocument? Document { get; set; }

        [Display(Name = "Scheduled Date"), DataType(DataType.Date)]
        public DateTime ScheduledDate { get; set; }

        [Display(Name = "Actual Date"), DataType(DataType.Date)]
        public DateTime? ActualDate { get; set; }

        public int? ReviewerId { get; set; }
        [ValidateNever] public User? Reviewer { get; set; }

        public ReviewOutcome Outcome { get; set; } = ReviewOutcome.Pending;

        [StringLength(2000)] public string? Notes { get; set; }

        [Display(Name = "Next Review"), DataType(DataType.Date)]
        public DateTime? NextReviewDate { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [NotMapped] public bool IsOverdue => Outcome == ReviewOutcome.Pending && ScheduledDate.Date < DateTime.Now.Date;
    }
}
