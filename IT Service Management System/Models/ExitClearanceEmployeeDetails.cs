namespace IT_Service_Management_System.Models
{
    public class ExitClearanceEmployeeDetails
    {
        public int Id { get; set; }

        public int ExitClearanceId { get; set; }
        public ExitClearance? ExitClearance { get; set; }

        public string Surname { get; set; } = string.Empty;

        public string Department { get; set; } = string.Empty;

        public string Supervisor { get; set; } = string.Empty;

        public string PositionHeld { get; set; } = string.Empty;

        public string LengthOfService { get; set; } = string.Empty;

        public DateTime? DateOfNotice { get; set; }

        public DateTime? LastDayOfWork { get; set; }

        public string Email { get; set; } = string.Empty;

        public string? ForwardingAddress { get; set; }

        public string? EmployeeSignature { get; set; }

        public DateTime? SignedDate { get; set; }
    }
}