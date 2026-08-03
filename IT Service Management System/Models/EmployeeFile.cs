using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace IT_Service_Management_System.Models
{
    /// <summary>
    /// A personal document attached to an employee by HR (contract, ID, certificate, payslip, …).
    /// Files are stored outside the web root and served only through an authorized download action.
    /// <para>
    /// <b>Retired.</b> This was the original employee-file store and has been superseded by
    /// Employee File Management (<see cref="Efm.EmployeeDocument"/>), which adds versioning,
    /// approvals, per-user permissions, retention policies and full-text search. Its controller and
    /// views have been removed, so nothing routes here any more.
    /// </para>
    /// <para>
    /// The entity and its table are deliberately kept: any rows and the files behind them in
    /// <c>employee-files/</c> are real HR records, and dropping the table would destroy them. Move
    /// the remaining rows into EFM, then remove this type and its DbSet in a single migration.
    /// Do not build anything new against it.
    /// </para>
    /// </summary>
    [Obsolete("Superseded by Efm.EmployeeDocument. Retained only so existing rows and files are not " +
              "destroyed — migrate them into EFM, then drop this entity and its table.")]
    public class EmployeeFile
    {
        public int Id { get; set; }

        public int EmployeeId { get; set; }

        [ValidateNever]
        public User? Employee { get; set; }

        /// <summary>Original file name as uploaded.</summary>
        [Required]
        [StringLength(260)]
        public string FileName { get; set; } = string.Empty;

        /// <summary>Opaque name on disk (GUID + extension). Not user-controlled.</summary>
        [Required]
        [StringLength(300)]
        public string StoredName { get; set; } = string.Empty;

        [StringLength(150)]
        public string ContentType { get; set; } = "application/octet-stream";

        public long FileSize { get; set; }

        /// <summary>Contract, ID Document, Certificate, Payslip, Appraisal, Other…</summary>
        [StringLength(60)]
        public string? Category { get; set; }

        [StringLength(500)]
        public string? Description { get; set; }

        public DateTime UploadedAt { get; set; } = DateTime.Now;

        [StringLength(150)]
        public string? UploadedBy { get; set; }
    }
}
