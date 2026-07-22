using IT_Service_Management_System.Models.Efm;

namespace IT_Service_Management_System.ViewModels.Efm
{
    /// <summary>One audit-trail row with resolved employee + document names.</summary>
    public class DocumentAuditRow
    {
        public long Id { get; set; }
        public DateTime Timestamp { get; set; }
        public DocumentAuditAction Action { get; set; }
        public string? PerformedByName { get; set; }
        public string? IpAddress { get; set; }
        public string? UserAgent { get; set; }
        public int? EmployeeId { get; set; }
        public string? EmployeeName { get; set; }
        public int? EmployeeDocumentId { get; set; }
        public string? DocumentTitle { get; set; }
        public string? Details { get; set; }
    }

    public class DocumentAuditVm
    {
        public List<DocumentAuditRow> Rows { get; set; } = new();

        // Filters
        public string? Q { get; set; }
        public DocumentAuditAction? Action { get; set; }
        public int? EmployeeId { get; set; }
        public int? DocumentId { get; set; }
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }

        // Scope labels (when filtered to one employee/document)
        public string? EmployeeName { get; set; }
        public string? DocumentTitle { get; set; }
    }
}
