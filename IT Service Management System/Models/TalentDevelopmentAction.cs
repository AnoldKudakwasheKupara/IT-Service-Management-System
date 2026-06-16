namespace IT_Service_Management_System.Models
{
    public class TalentDevelopmentAction
    {
        public int Id { get; set; }

        public int TalentIdentificationId { get; set; }

        public string DevelopmentAction { get; set; }

        public string Objective { get; set; }

        public string Timeline { get; set; }

        public DevelopmentStatus Status { get; set; }

        public TalentIdentification TalentIdentification { get; set; }
    }
}