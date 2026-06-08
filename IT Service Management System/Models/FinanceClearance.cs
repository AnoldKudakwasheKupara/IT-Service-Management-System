namespace IT_Service_Management_System.Models
{
    public class FinanceClearance
    {
        public int Id { get; set; }

        public int ExitClearanceId { get; set; }

        public bool StaffAdvanceCleared { get; set; }

        public bool ReceiptsHandedOver { get; set; }

        public bool FinalDuesProcessed { get; set; }

        public string? Comments { get; set; }

        public int? ClearedById { get; set; }

        public DateTime? ClearedDate { get; set; }
    }
}