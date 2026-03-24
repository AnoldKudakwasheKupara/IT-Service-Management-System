namespace IT_Service_Management_System.Models
{
    public class AuditLog
    {
        public int Id { get; set; }

        public string UserId { get; set; }
        public string UserName { get; set; }

        public string Action { get; set; } 
        public string Entity { get; set; } 
        public int? EntityId { get; set; }

        public string Details { get; set; } 

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        public string IpAddress { get; set; }
        public string? Location { get; set; } 
        public string? Device { get; set; }  
    }
}