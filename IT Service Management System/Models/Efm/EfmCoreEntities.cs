using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace IT_Service_Management_System.Models.Efm
{
    /// <summary>
    /// A section of every employee's personnel file (Personal, Employment, Medical, …).
    /// System folders are seeded; HR may add more. Documents are filed into exactly one folder.
    /// </summary>
    public class DocumentFolder
    {
        public int Id { get; set; }

        [Required, StringLength(120)]
        public string Name { get; set; } = string.Empty;

        [StringLength(300)]
        public string? Description { get; set; }

        [StringLength(50)]
        public string? Icon { get; set; }          // Font Awesome class, e.g. "fa-id-card"

        public int SortOrder { get; set; }
        public bool IsSystem { get; set; }          // seeded, cannot be deleted
        public bool IsActive { get; set; } = true;

        [ValidateNever]
        public ICollection<EmployeeDocument> Documents { get; set; } = new List<EmployeeDocument>();
    }

    /// <summary>
    /// An HR-defined document type (Passport, Contract, Degree …). Unlimited, no code change
    /// needed to add one. Optionally tied to a default folder and expiry/retention behaviour.
    /// </summary>
    public class DocumentCategory
    {
        public int Id { get; set; }

        [Required, StringLength(120)]
        public string Name { get; set; } = string.Empty;

        [StringLength(300)]
        public string? Description { get; set; }

        public int? DefaultFolderId { get; set; }
        [ValidateNever]
        public DocumentFolder? DefaultFolder { get; set; }

        /// <summary>When true, documents of this category are expiry-tracked and alerted on.</summary>
        public bool IsExpiryTracked { get; set; }

        /// <summary>Default retention in years after employee termination (null = permanent).</summary>
        public int? DefaultRetentionYears { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [ValidateNever]
        public ICollection<EmployeeDocument> Documents { get; set; } = new List<EmployeeDocument>();
    }

    /// <summary>
    /// Declares that a category is required — used for missing-document detection and file
    /// completeness. May be scoped to a role and/or department (null scope = applies to everyone).
    /// </summary>
    public class RequiredDocument
    {
        public int Id { get; set; }

        public int CategoryId { get; set; }
        [ValidateNever]
        public DocumentCategory? Category { get; set; }

        [StringLength(50)]
        public string? AppliesToRole { get; set; }   // null = all roles

        public int? AppliesToDepartmentId { get; set; }
        [ValidateNever]
        public Department? AppliesToDepartment { get; set; }

        public bool IsMandatory { get; set; } = true;
        public bool IsActive { get; set; } = true;
    }

    /// <summary>
    /// The logical document (metadata + pointer to its current version). The physical file bytes
    /// live in <see cref="DocumentVersion"/> via a storage provider — never in SQL Server.
    /// </summary>
    public class EmployeeDocument
    {
        public int Id { get; set; }

        public int EmployeeId { get; set; }
        [ValidateNever]
        public User? Employee { get; set; }

        public int FolderId { get; set; }
        [ValidateNever]
        public DocumentFolder? Folder { get; set; }

        public int CategoryId { get; set; }
        [ValidateNever]
        public DocumentCategory? Category { get; set; }

        [Required, StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [StringLength(1000)]
        public string? Description { get; set; }

        [StringLength(100)]
        public string? DocumentNumber { get; set; }

        public DateTime? IssueDate { get; set; }
        public DateTime? ExpiryDate { get; set; }

        [StringLength(500)]
        public string? Keywords { get; set; }

        public ConfidentialityLevel ConfidentialityLevel { get; set; } = ConfidentialityLevel.Confidential;
        public DocumentStatus Status { get; set; } = DocumentStatus.Active;

        public int? CurrentVersionId { get; set; }
        [ValidateNever]
        public DocumentVersion? CurrentVersion { get; set; }

        public int ViewCount { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public int? CreatedById { get; set; }
        [StringLength(150)]
        public string? CreatedByName { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public bool IsArchived { get; set; }
        public DateTime? ArchivedAt { get; set; }

        /// <summary>Delete-after date computed from the applicable retention policy.</summary>
        public DateTime? RetentionUntil { get; set; }

        public bool IsDeleted { get; set; }          // soft delete (global query filter)

        [Timestamp]
        public byte[]? RowVersion { get; set; }       // optimistic concurrency

        [ValidateNever]
        public ICollection<DocumentVersion> Versions { get; set; } = new List<DocumentVersion>();
        [ValidateNever]
        public ICollection<DocumentTagMap> Tags { get; set; } = new List<DocumentTagMap>();
        [ValidateNever]
        public ICollection<DocumentApproval> Approvals { get; set; } = new List<DocumentApproval>();
        [ValidateNever]
        public ICollection<DocumentComment> Comments { get; set; } = new List<DocumentComment>();

        [NotMapped]
        public bool IsExpired => ExpiryDate.HasValue && ExpiryDate.Value.Date < DateTime.Today;

        [NotMapped]
        public int? DaysUntilExpiry => ExpiryDate.HasValue
            ? (int)(ExpiryDate.Value.Date - DateTime.Today).TotalDays
            : (int?)null;
    }

    /// <summary>
    /// One immutable version of a document's file. A new upload adds a version rather than
    /// overwriting, giving full version history and restore.
    /// </summary>
    public class DocumentVersion
    {
        public int Id { get; set; }

        public int EmployeeDocumentId { get; set; }
        [ValidateNever]
        public EmployeeDocument? Document { get; set; }

        public int VersionNumber { get; set; }

        [Required, StringLength(260)]
        public string FileName { get; set; } = string.Empty;

        /// <summary>Opaque key/path within the storage provider (GUID-based; not user input).</summary>
        [Required, StringLength(400)]
        public string StoredKey { get; set; } = string.Empty;

        public StorageProviderType StorageProvider { get; set; } = StorageProviderType.LocalDisk;

        [StringLength(150)]
        public string ContentType { get; set; } = "application/octet-stream";

        public long FileSizeBytes { get; set; }

        [StringLength(64)]
        public string? Sha256 { get; set; }

        /// <summary>OCR-extracted text for full-text search (populated by the OCR pipeline).</summary>
        public string? OcrText { get; set; }

        [StringLength(500)]
        public string? ChangeNote { get; set; }

        public DateTime UploadedAt { get; set; } = DateTime.Now;
        public int? UploadedById { get; set; }
        [StringLength(150)]
        public string? UploadedByName { get; set; }

        public bool IsCurrent { get; set; }
    }
}
