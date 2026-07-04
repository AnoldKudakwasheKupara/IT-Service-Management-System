using IT_Service_Management_System.Models.Efm;
using IT_Service_Management_System.ViewModels.Reports;

namespace IT_Service_Management_System.ViewModels.Efm
{
    public class EfmDashboardVm
    {
        public DateTime GeneratedAt { get; set; }

        public int TotalDocuments { get; set; }
        public int Expired { get; set; }
        public int ExpiringSoon { get; set; }
        public int PendingApproval { get; set; }
        public int Archived { get; set; }
        public int TotalVersions { get; set; }
        public long StorageBytes { get; set; }
        public int UnreadNotifications { get; set; }

        public List<NameCount> ByFolder { get; set; } = new();
        public List<NameCount> ByCategory { get; set; } = new();
        public List<NameCount> ByStatus { get; set; } = new();

        public List<EmployeeDocument> RecentlyUploaded { get; set; } = new();
        public List<EmployeeDocument> MostViewed { get; set; } = new();
    }

    /// <summary>One employee's completeness row for the compliance report.</summary>
    public class ComplianceRow
    {
        public int EmployeeId { get; set; }
        public string EmployeeName { get; set; } = "";
        public string? Department { get; set; }
        public int RequiredCount { get; set; }
        public int PresentCount { get; set; }
        public int Percent { get; set; }
        public string Missing { get; set; } = "";
    }
}
