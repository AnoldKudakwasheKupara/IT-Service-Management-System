namespace IT_Service_Management_System.Models
{
    public class ClearanceRequest
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Surname { get; set; }
        public string Department { get; set; }
        public string Supervisor { get; set; }
        public string PositionHeld { get; set; }
        public string LengthOfService { get; set; }

        public DateTime DateOfNotice { get; set; }
        public DateTime LastDayOfWork { get; set; }

        public string Email { get; set; }
        public string ForwardingAddress { get; set; }

        public string EmployeeSignature { get; set; }
        public DateTime? EmployeeSignatureDate { get; set; }

        public ClearanceStatus Status { get; set; }

        public DateTime CreatedDate { get; set; }

        public virtual ICollection<FinanceClearanceItem> FinanceItems { get; set; }
        public virtual ICollection<SystemAdminClearanceItem> SystemAdminItems { get; set; }
        public virtual ICollection<HRClearanceItem> HRItems { get; set; }
        public virtual ICollection<DevelopmentClearanceItem> DevelopmentItems { get; set; }
        public virtual ICollection<StockHandoverItem> StockItems { get; set; }
    }
}