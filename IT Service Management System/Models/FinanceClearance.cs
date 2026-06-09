namespace IT_Service_Management_System.Models
{
    public class FinanceClearance
    {
        public int Id { get; set; }

        public int ExitClearanceId { get; set; }

        public string? StaffAdvanceReference { get; set; }
        public string? StaffAdvanceReceivedBy { get; set; }
        public DateTime? StaffAdvanceDate { get; set; }

        public string? ReceiptsReference { get; set; }
        public string? ReceiptsReceivedBy { get; set; }
        public DateTime? ReceiptsDate { get; set; }

        public string? FinalDuesReference { get; set; }
        public string? FinalDuesReceivedBy { get; set; }
        public DateTime? FinalDuesDate { get; set; }

        public string? Comments { get; set; }

        public int? ClearedById { get; set; }

        public DateTime? ClearedDate { get; set; }
    }
}