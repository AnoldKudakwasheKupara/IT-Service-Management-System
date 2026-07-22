using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace IT_Service_Management_System.Models.Efm
{
    /// <summary>Lifecycle of a document request sent to an employee.</summary>
    public enum DocumentRequestStatus
    {
        Pending = 0,
        Fulfilled = 1,
        Cancelled = 2
    }

    /// <summary>
    /// An HR-initiated ask for an employee to supply a document (e.g. "upload a copy of your
    /// National ID"). The employee fulfils it from My Documents; the resulting upload goes through
    /// the normal HR approval workflow and is linked back here for traceability.
    /// </summary>
    public class DocumentRequest
    {
        public int Id { get; set; }

        public int EmployeeId { get; set; }
        [ValidateNever]
        public User? Employee { get; set; }

        public int CategoryId { get; set; }
        [ValidateNever]
        public DocumentCategory? Category { get; set; }

        /// <summary>What is being asked for, shown to the employee (e.g. "National ID copy").</summary>
        [Required, StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [StringLength(1000)]
        public string? Instructions { get; set; }

        public DateTime? DueDate { get; set; }

        public DocumentRequestStatus Status { get; set; } = DocumentRequestStatus.Pending;

        public int? RequestedById { get; set; }
        [StringLength(150)]
        public string? RequestedByName { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? LastReminderAt { get; set; }

        public DateTime? FulfilledAt { get; set; }

        /// <summary>The document the employee uploaded to satisfy this request.</summary>
        public int? FulfilledDocumentId { get; set; }
        [ValidateNever]
        public EmployeeDocument? FulfilledDocument { get; set; }

        public DateTime? CancelledAt { get; set; }
        [StringLength(150)]
        public string? CancelledByName { get; set; }

        [NotMapped]
        public bool IsOverdue => Status == DocumentRequestStatus.Pending
            && DueDate.HasValue && DueDate.Value.Date < DateTime.Today;
    }
}
