using IT_Service_Management_System.Models.Ims;

namespace IT_Service_Management_System.ViewModels.Ims
{
    /// <summary>Executive dashboard aggregates for the Integrated Management System.</summary>
    public class ImsDashboardVm
    {
        // Documents
        public int TotalDocuments { get; set; }
        public int PublishedDocuments { get; set; }
        public int DraftDocuments { get; set; }
        public int InWorkflowDocuments { get; set; }
        public int DocumentsDueReview { get; set; }
        public int ExpiredDocuments { get; set; }
        public int Policies { get; set; }
        public int Procedures { get; set; }
        public int Forms { get; set; }

        // Acknowledgements
        public int TotalAcknowledgements { get; set; }
        public int CompletedAcknowledgements { get; set; }
        public int AcknowledgementPercent { get; set; }

        // CAPA / NC
        public int OpenCapas { get; set; }
        public int ClosedCapas { get; set; }
        public int OverdueCapas { get; set; }
        public int OpenNonConformances { get; set; }

        // Audits & findings
        public int OpenAudits { get; set; }
        public int CompletedAudits { get; set; }
        public int OpenFindings { get; set; }

        // Risk
        public int OpenRisks { get; set; }
        public int CriticalRisks { get; set; }
        public int[] RiskHeatmap { get; set; } = new int[25]; // index = (impact-1)*5 + (likelihood-1)

        // Training
        public int TrainingRecords { get; set; }
        public int TrainingCompleted { get; set; }
        public int TrainingCompletionPercent { get; set; }
        public int ExpiringCertificates { get; set; }

        // Suppliers
        public int Suppliers { get; set; }
        public int AverageSupplierScore { get; set; }

        // Objectives / compliance / improvement
        public int ActiveObjectives { get; set; }
        public int ObjectivesAtRisk { get; set; }
        public int ComplianceObligations { get; set; }
        public int CompliancePercent { get; set; }
        public int OpenImprovements { get; set; }

        // Lists for the dashboard panels
        public List<IsoDocument> UpcomingReviews { get; set; } = new();
        public List<Capa> CapaWatchlist { get; set; } = new();
        public List<Risk> TopRisks { get; set; } = new();
    }
}
