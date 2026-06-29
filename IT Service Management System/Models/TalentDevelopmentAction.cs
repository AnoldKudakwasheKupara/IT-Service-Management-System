using IT_Service_Management_System.Enums;

namespace IT_Service_Management_System.Models
{
    public class TalentDevelopmentAction
    {
        public int Id { get; set; }

        public int TalentIdentificationId { get; set; }

        public string DevelopmentAction { get; set; } = string.Empty;

        public string Objective { get; set; } = string.Empty;

        public string Timeline { get; set; } = string.Empty;

        public DevelopmentStatus Status { get; set; }

        public TalentIdentification? TalentIdentification { get; set; }
    }
}