namespace IT_Service_Management_System.Models
{
    public class HrApproval
    {
        public int Id { get; set; }

        public int ExitClearanceId { get; set; }

        public bool Approved { get; set; }

        public string? Comments { get; set; }

        public int HrUserId { get; set; }

        public DateTime? ApprovedDate { get; set; }
    }
}
