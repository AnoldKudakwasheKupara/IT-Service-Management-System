namespace IT_Service_Management_System.Models
{
    public class AssetHistory
    {
        public int Id { get; set; }

        public int AssetId { get; set; }
        public Asset? Asset { get; set; }

        public DateTime Date { get; set; }

        public int? UserId { get; set; }
        public User? User { get; set; }

        public string EventType { get; set; } = string.Empty;   // Issued, Returned, Repair, etc
        public string Condition { get; set; } = string.Empty;   // New, Old

        public string PerformedBy { get; set; } = string.Empty;
        public string Remarks { get; set; } = string.Empty;
    }
}