namespace IT_Service_Management_System.Models
{
    public class ClearanceWorkflow
    {
        public int Id { get; set; }

        public int ExitClearanceId { get; set; }

        public ClearanceStage Stage { get; set; }

        public int AssignedToUserId { get; set; }

        public bool Completed { get; set; }

        public DateTime AssignedDate { get; set; }

        public DateTime? CompletedDate { get; set; }

        public string? Comments { get; set; }
    }
}