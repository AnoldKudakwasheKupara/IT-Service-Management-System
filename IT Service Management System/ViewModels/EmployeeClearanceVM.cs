namespace IT_Service_Management_System.ViewModels
{
    public class EmployeeClearanceVM
    {
        public int ExitClearanceId { get; set; }

        public string AccessToken { get; set; }

        public string Surname { get; set; }

        public string Department { get; set; }

        public string Supervisor { get; set; }

        public string PositionHeld { get; set; }

        public string LengthOfService { get; set; }

        public DateTime? DateOfNotice { get; set; }

        public DateTime? LastDayOfWork { get; set; }

        public string Email { get; set; }

        public string? ForwardingAddress { get; set; }

        public string? EmployeeSignature { get; set; }
    }
}