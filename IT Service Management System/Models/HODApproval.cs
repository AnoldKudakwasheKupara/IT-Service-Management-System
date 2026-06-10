namespace IT_Service_Management_System.Models
{
    public class HodApproval
    {
        public int Id { get; set; }

        public int ExitClearanceId { get; set; }

        public string? ConfirmedBy { get; set; }

        public string? HandoverSignature { get; set; }

        public DateTime? HandoverDate { get; set; }

        public string? Comments { get; set; }

        public int? ApprovedById { get; set; }

        public DateTime? ApprovedDate { get; set; }
    }
}