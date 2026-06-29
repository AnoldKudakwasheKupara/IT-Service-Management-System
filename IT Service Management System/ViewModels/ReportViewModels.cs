using IT_Service_Management_System.Models;

namespace IT_Service_Management_System.ViewModels.Reports
{
    // Small reusable shapes for breakdowns.
    public record NameCount(string Name, int Count);
    public record NameAmount(string Name, decimal Amount);

    public class ReportsDashboardVM
    {
        public DateTime GeneratedAt { get; set; }

        // Tickets
        public int TotalTickets { get; set; }
        public int OpenTickets { get; set; }
        public int ResolvedTickets { get; set; }
        public int ClosedTickets { get; set; }

        // Assets
        public int TotalAssets { get; set; }
        public int AssetsIssued { get; set; }
        public int AssetsInStock { get; set; }
        public int AssetsInRepair { get; set; }
        public decimal TotalAssetValue { get; set; }

        // Users
        public int TotalUsers { get; set; }
        public int ActiveUsers { get; set; }
        public int TotalDepartments { get; set; }

        // Activity
        public int TotalActivities { get; set; }
        public double TotalActivityHours { get; set; }

        // Payments
        public decimal TotalPaid { get; set; }
        public decimal TotalOutstanding { get; set; }
        public int OverduePayments { get; set; }

        // SSL
        public int CertsExpired { get; set; }
        public int CertsExpiringSoon { get; set; }

        // Maintenance
        public int MaintenanceRecords { get; set; }
    }

    public class TicketsReportVM
    {
        public int Total { get; set; }
        public int Open { get; set; }
        public int InProgress { get; set; }
        public int Resolved { get; set; }
        public int Closed { get; set; }
        public double ResolutionRate { get; set; } // % resolved or closed
        public List<NameCount> ByStatus { get; set; } = new();
        public List<NameCount> ByPriority { get; set; } = new();
        public List<NameCount> ByCategory { get; set; } = new();
        public List<NameCount> TopRequesters { get; set; } = new();
        public List<Ticket> Recent { get; set; } = new();
    }

    public class AssetsReportVM
    {
        public int Total { get; set; }
        public decimal TotalValue { get; set; }
        public List<NameCount> ByStatus { get; set; } = new();
        public List<NameCount> ByCondition { get; set; } = new();
        public List<NameCount> ByHolder { get; set; } = new();
        public List<NameCount> ByEventType { get; set; } = new();
        public List<AssetHistory> RecentActivity { get; set; } = new();
    }

    public class UsersReportVM
    {
        public int Total { get; set; }
        public int Active { get; set; }
        public int Inactive { get; set; }
        public List<NameCount> ByRole { get; set; } = new();
        public List<NameCount> ByDepartment { get; set; } = new();
        public List<NameCount> AssetsPerUser { get; set; } = new();
    }

    public class ActivityReportVM
    {
        public int Total { get; set; }
        public int Ongoing { get; set; }
        public int Completed { get; set; }
        public double TotalHours { get; set; }
        public List<NameCount> ByCategory { get; set; } = new();          // count
        public List<NameAmount> HoursByCategory { get; set; } = new();    // hours (decimal)
        public List<NameAmount> HoursByUser { get; set; } = new();
    }

    public class PaymentsReportVM
    {
        public decimal TotalAmount { get; set; }
        public decimal TotalPaid { get; set; }
        public decimal TotalOutstanding { get; set; }
        public decimal TotalOverdue { get; set; }
        public List<NameCount> CountByStatus { get; set; } = new();
        public List<NameAmount> AmountByStatus { get; set; } = new();
        public List<Payment> Upcoming { get; set; } = new();   // due in next 30 days, unpaid
        public List<Payment> Overdue { get; set; } = new();
    }

    public class CertificatesReportVM
    {
        public int Total { get; set; }
        public int Expired { get; set; }
        public int Within30 { get; set; }
        public int Within90 { get; set; }
        public int Healthy { get; set; }
        public List<SSLCertificate> Attention { get; set; } = new(); // expired or expiring soon
    }

    public class HrReportVM
    {
        public int TotalClearances { get; set; }
        public int ClearancesInProgress { get; set; }
        public int ClearancesCompleted { get; set; }
        public int ExitInterviews { get; set; }
        public int EngagementInterviews { get; set; }
        public int TalentRecords { get; set; }
        public List<NameCount> ClearancesByStatus { get; set; } = new();
        public List<NameCount> ClearancesByStage { get; set; } = new();
        public List<ExitClearance> RecentClearances { get; set; } = new();
    }

    public class MaintenanceReportVM
    {
        public int Total { get; set; }
        public int UpcomingCount { get; set; }
        public List<NameCount> ByType { get; set; } = new();
        public List<MaintenanceRecord> Recent { get; set; } = new();
        public List<MaintenanceRecord> Upcoming { get; set; } = new();
    }
}
