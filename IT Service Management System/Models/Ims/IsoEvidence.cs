using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace IT_Service_Management_System.Models.Ims
{
    // ── Evidence Repository enumerations ─────────────────────────────────────────
    public enum EvidenceType { Document, Photo, Certificate, Report, Record, Screenshot, EmailProof, Minutes, Other }

    /// <summary>
    /// A piece of objective evidence supporting conformity to an ISO clause. Can be linked to any IMS record
    /// (audit, finding, CAPA, risk…) via <see cref="LinkedEntityType"/> / <see cref="LinkedEntityId"/>.
    /// Files are stored through the shared EFM document storage.
    /// </summary>
    public class IsoEvidence
    {
        public int Id { get; set; }

        [NotMapped] public string Reference => $"EVD-{Id:D5}";

        [Required, StringLength(250)]
        public string Title { get; set; } = string.Empty;

        [StringLength(2000)]
        public string? Description { get; set; }

        public EvidenceType Type { get; set; } = EvidenceType.Document;
        public IsoStandard Standard { get; set; } = IsoStandard.Both;

        [StringLength(30), Display(Name = "ISO Clause")]
        public string? IsoClause { get; set; }

        // Stored file (shared EFM storage)
        [StringLength(260)] public string? StoredFileName { get; set; }
        [StringLength(260)] public string? OriginalFileName { get; set; }
        [StringLength(150)] public string? ContentType { get; set; }
        public long FileSize { get; set; }
        [StringLength(50)] public string? StorageProvider { get; set; }

        /// <summary>Optional back-link to another IMS record, e.g. "Audit", "Capa", "Risk".</summary>
        [StringLength(50)] public string? LinkedEntityType { get; set; }
        public int? LinkedEntityId { get; set; }

        public int? UploadedById { get; set; }
        [ValidateNever] public User? UploadedBy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [NotMapped] public bool HasFile => !string.IsNullOrEmpty(StoredFileName);
    }
}
