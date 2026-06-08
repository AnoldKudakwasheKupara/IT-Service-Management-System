namespace IT_Service_Management_System.Models
{
    public class ExitClearance
    {
        public int Id { get; set; }

        public int EmployeeId { get; set; }
        public User Employee { get; set; }

        public ClearanceStage CurrentStage { get; set; }

        public ClearanceStatus Status { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public ICollection<ClearanceWorkflow> Workflows { get; set; }
            = new List<ClearanceWorkflow>();
    }
}