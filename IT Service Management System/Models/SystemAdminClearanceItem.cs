namespace IT_Service_Management_System.Models
{
    public class SystemAdminClearanceItem
    {
        public int Id { get; set; }

        public int ClearanceRequestId { get; set; }

        public string ItemName { get; set; } = string.Empty;

        public string AssetSerialNumber { get; set; } = string.Empty;

        public string ReceivedBy { get; set; } = string.Empty;

        public DateTime? DateReceived { get; set; }

        public bool Completed { get; set; }
    }
}