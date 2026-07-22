using IT_Service_Management_System.Models;
using IT_Service_Management_System.Models.Efm;

namespace IT_Service_Management_System.ViewModels.Efm
{
    /// <summary>Backing model for the HR document-requests workspace.</summary>
    public class DocumentRequestsVm
    {
        public List<DocumentRequest> Requests { get; set; } = new();
        public List<User> Employees { get; set; } = new();
        public List<DocumentCategory> Categories { get; set; } = new();

        // Filters
        public string? Status { get; set; }
        public int? EmployeeId { get; set; }
        public string? Q { get; set; }

        // Stats
        public int PendingCount { get; set; }
        public int OverdueCount { get; set; }
        public int FulfilledCount { get; set; }
        public int CancelledCount { get; set; }
    }
}
