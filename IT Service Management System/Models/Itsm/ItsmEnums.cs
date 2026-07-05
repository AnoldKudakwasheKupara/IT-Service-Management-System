namespace IT_Service_Management_System.Models.Itsm
{
    // ── CMDB / Configuration Items ──────────────────────────────────────────────
    public enum CiType { Server, Workstation, Application, Database, NetworkDevice, Service, CloudResource, Other }
    public enum CiStatus { Active, Inactive, UnderMaintenance, Retired }
    public enum CiCriticality { Low, Medium, High, Critical }
    public enum CiEnvironment { Production, Staging, Development, Test, DR }

    // ── Problem management ──────────────────────────────────────────────────────
    public enum ProblemStatus { New, Investigating, RootCauseIdentified, KnownError, Resolved, Closed }

    // ── Change management ───────────────────────────────────────────────────────
    public enum ChangeType { Standard, Normal, Emergency }
    public enum ChangeStatus { Draft, SubmittedForApproval, Approved, Rejected, Scheduled, InProgress, Implemented, Closed, Failed, Cancelled }
    public enum ChangeRisk { Low, Medium, High }
    public enum ChangeImpact { Low, Medium, High }
}
