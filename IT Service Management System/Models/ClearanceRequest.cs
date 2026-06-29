namespace IT_Service_Management_System.Models
{
    public class ClearanceRequest
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Surname { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public string Supervisor { get; set; } = string.Empty;
        public string PositionHeld { get; set; } = string.Empty;
        public string LengthOfService { get; set; } = string.Empty;

        public DateTime DateOfNotice { get; set; }
        public DateTime LastDayOfWork { get; set; }

        public string Email { get; set; } = string.Empty;
        public string ForwardingAddress { get; set; } = string.Empty;

        public string EmployeeSignature { get; set; } = string.Empty;
        public DateTime? EmployeeSignatureDate { get; set; }

        public ClearanceStatus Status { get; set; }

        public DateTime CreatedDate { get; set; }

        public virtual ICollection<FinanceClearanceItem> FinanceItems { get; set; } = new List<FinanceClearanceItem>();
        public virtual ICollection<SystemAdminClearanceItem> SystemAdminItems { get; set; } = new List<SystemAdminClearanceItem>();
        public virtual ICollection<HRClearanceItem> HRItems { get; set; } = new List<HRClearanceItem>();
        public virtual ICollection<DevelopmentClearanceItem> DevelopmentItems { get; set; } = new List<DevelopmentClearanceItem>();
        public virtual ICollection<StockHandoverItem> StockItems { get; set; } = new List<StockHandoverItem>();
    }
}