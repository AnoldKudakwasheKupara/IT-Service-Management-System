namespace IT_Service_Management_System.Models
{
    public class HRClearanceItem
    {
        public int Id { get; set; }

        public int ClearanceRequestId { get; set; }

        public string ItemName { get; set; } = string.Empty;

        public bool TickIfDone { get; set; }

        public string ReceivedBy { get; set; } = string.Empty;

        public DateTime? DateReceived { get; set; }
    }
}