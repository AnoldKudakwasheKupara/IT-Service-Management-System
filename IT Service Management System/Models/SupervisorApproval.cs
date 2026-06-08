namespace IT_Service_Management_System.Models
{
    public class SupervisorApproval
    {
        public int Id { get; set; }

        public int ExitClearanceId { get; set; }

        public int SupervisorId { get; set; }

        public bool Approved { get; set; }

        public string? Comments { get; set; }

        public DateTime? ApprovedDate { get; set; }
    }
}