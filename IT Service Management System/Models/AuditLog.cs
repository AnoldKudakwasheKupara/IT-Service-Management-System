namespace IT_Service_Management_System.Models
{
    public class AuditLog
    {
        public int Id { get; set; }

        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;

        public string Action { get; set; } = string.Empty;
        public string Entity { get; set; } = string.Empty;
        public int? EntityId { get; set; }

        public string Details { get; set; } = string.Empty;

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        public string IpAddress { get; set; } = string.Empty;
        public string? Location { get; set; } 
        public string? Device { get; set; }  
    }
}