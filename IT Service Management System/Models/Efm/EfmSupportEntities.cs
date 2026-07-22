using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace IT_Service_Management_System.Models.Efm
{
    public class DocumentTag
    {
        public int Id { get; set; }

        [Required, StringLength(60)]
        public string Name { get; set; } = string.Empty;

        [ValidateNever]
        public ICollection<DocumentTagMap> Documents { get; set; } = new List<DocumentTagMap>();
    }

    /// <summary>Join table: document ↔ tag (many-to-many).</summary>
    public class DocumentTagMap
    {
        public int EmployeeDocumentId { get; set; }
        [ValidateNever]
        public EmployeeDocument? Document { get; set; }

        public int DocumentTagId { get; set; }
        [ValidateNever]
        public DocumentTag? Tag { get; set; }
    }

    /// <summary>Immutable record of a document action (view/download/edit/…) with who/when/where.</summary>
    public class DocumentAuditLog
    {
        public long Id { get; set; }

        public int? EmployeeDocumentId { get; set; }
        public int? EmployeeId { get; set; }

        public DocumentAuditAction Action { get; set; }

        public int? PerformedById { get; set; }
        [StringLength(150)]
        public string? PerformedByName { get; set; }

        [StringLength(64)]
        public string? IpAddress { get; set; }
        [StringLength(300)]
        public string? UserAgent { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.Now;

        [StringLength(1000)]
        public string? Details { get; set; }
    }

    /// <summary>A secure, optionally password-protected, expiring internal share link (read-only).</summary>
    public class DocumentShare
    {
        public int Id { get; set; }

        public int EmployeeDocumentId { get; set; }
        [ValidateNever]
        public EmployeeDocument? Document { get; set; }

        public int? DocumentVersionId { get; set; }

        [Required, StringLength(64)]
        public string Token { get; set; } = string.Empty;

        public int? CreatedById { get; set; }
        [StringLength(150)]
        public string? CreatedByName { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? ExpiresAt { get; set; }

        /// <summary>Optional password (PBKDF2 hash) required to open the link.</summary>
        [StringLength(400)]
        public string? PasswordHash { get; set; }

        public int? MaxDownloads { get; set; }
        public int DownloadCount { get; set; }
        public bool IsReadOnly { get; set; } = true;
        public DateTime? RevokedAt { get; set; }
    }

    /// <summary>One step in a (possibly multi-level) document approval workflow.</summary>
    public class DocumentApproval
    {
        public int Id { get; set; }

        public int EmployeeDocumentId { get; set; }
        [ValidateNever]
        public EmployeeDocument? Document { get; set; }

        public int? DocumentVersionId { get; set; }

        public int Level { get; set; } = 1;

        [StringLength(50)]
        public string? ApproverRole { get; set; }
        public int? ApproverUserId { get; set; }

        public ApprovalStatus Status { get; set; } = ApprovalStatus.Pending;

        public int? DecidedById { get; set; }
        [StringLength(150)]
        public string? DecidedByName { get; set; }
        public DateTime? DecidedAt { get; set; }

        [StringLength(1000)]
        public string? Comments { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    public class DocumentComment
    {
        public int Id { get; set; }

        public int EmployeeDocumentId { get; set; }
        [ValidateNever]
        public EmployeeDocument? Document { get; set; }

        public int? AuthorId { get; set; }
        [StringLength(150)]
        public string? AuthorName { get; set; }

        [Required, StringLength(2000)]
        public string Body { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    public class DocumentNotification
    {
        public int Id { get; set; }

        /// <summary>Null = addressed to the HR group rather than one user.</summary>
        public int? RecipientUserId { get; set; }

        public DocumentNotificationType Type { get; set; }

        public int? EmployeeDocumentId { get; set; }
        public int? EmployeeId { get; set; }

        [Required, StringLength(200)]
        public string Title { get; set; } = string.Empty;
        [StringLength(1000)]
        public string? Message { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public bool IsRead { get; set; }
        public DateTime? ReadAt { get; set; }
    }

    /// <summary>A configured storage backend. Secrets stay in configuration, not here.</summary>
    public class StorageProvider
    {
        public int Id { get; set; }

        [Required, StringLength(120)]
        public string Name { get; set; } = string.Empty;

        public StorageProviderType Type { get; set; } = StorageProviderType.LocalDisk;

        /// <summary>Root path / container / bucket name (non-secret).</summary>
        [StringLength(400)]
        public string? RootLocation { get; set; }

        public bool IsDefault { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    /// <summary>Retention rule: how long to keep documents of a category/folder before acting.</summary>
    public class RetentionPolicy
    {
        public int Id { get; set; }

        [Required, StringLength(150)]
        public string Name { get; set; } = string.Empty;

        public int? CategoryId { get; set; }
        [ValidateNever]
        public DocumentCategory? Category { get; set; }

        public int? FolderId { get; set; }
        [ValidateNever]
        public DocumentFolder? Folder { get; set; }

        /// <summary>Years to retain after the trigger (null = permanent — never auto-act).</summary>
        public int? RetentionYears { get; set; }

        public RetentionAction Action { get; set; } = RetentionAction.Archive;
        public bool IsActive { get; set; } = true;
    }

    /// <summary>Tracks which expiry threshold alerts (30/60/90/180d) have already fired for a document.</summary>
    public class ExpiryAlert
    {
        public int Id { get; set; }

        public int EmployeeDocumentId { get; set; }
        [ValidateNever]
        public EmployeeDocument? Document { get; set; }

        public int ThresholdDays { get; set; }     // 30 / 60 / 90 / 180
        public DateTime AlertedAt { get; set; } = DateTime.Now;
        public bool Acknowledged { get; set; }
    }
}
