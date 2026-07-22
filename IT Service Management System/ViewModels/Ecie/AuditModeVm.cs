namespace IT_Service_Management_System.ViewModels.Ecie
{
    /// <summary>Audit-Mode view: the engine acts as an external auditor — readiness score + grounded gaps.</summary>
    public class AuditModeVm
    {
        public ComplianceHealthVm Health { get; set; } = new();
        public EcieResponse Readiness { get; set; } = new();
        public List<string> Questions { get; set; } = new();
    }
}
