using IT_Service_Management_System.Models;

namespace IT_Service_Management_System.ViewModels
{
    public class ExitClearanceDetailsVM
    {
        public ExitClearance? ExitClearance { get; set; }

        public ExitClearanceEmployeeDetails? EmployeeDetails { get; set; }

        public FinanceClearance? FinanceClearance { get; set; }

        public SystemsAdminClearance? SystemsAdminClearance { get; set; }

        public DevelopmentClearance? DevelopmentClearance { get; set; }

        public SupervisorApproval? SupervisorApproval { get; set; }

        public HodApproval? HodApproval { get; set; }

        public HrApproval? HrApproval { get; set; }

        public List<ClearanceWorkflow> WorkflowHistory { get; set; }
            = new();
    }
}