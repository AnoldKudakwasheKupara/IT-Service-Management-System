namespace IT_Service_Management_System.Models
{
    public class AuditLog
    {
        public int Id { get; set; }

        public string UserId { get; set; } // Who did it
        public string UserName { get; set; }

        public string Action { get; set; } // e.g. "Created Ticket"
        public string Entity { get; set; } // e.g. "Ticket"
        public int? EntityId { get; set; } // Ticket ID

        public string Details { get; set; } // Optional description

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        public string IpAddress { get; set; }
    }
}