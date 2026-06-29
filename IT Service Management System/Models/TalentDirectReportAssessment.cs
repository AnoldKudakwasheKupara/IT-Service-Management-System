using IT_Service_Management_System.Enums;

namespace IT_Service_Management_System.Models
{
    public class TalentDirectReportAssessment
    {
        public int Id { get; set; }

        public int TalentIdentificationId { get; set; }

        public string EmployeeName { get; set; } = string.Empty;

        public PerformanceRating PerformanceRating { get; set; }

        public string Comments { get; set; } = string.Empty;

        public TalentIdentification? TalentIdentification { get; set; }
    }
}