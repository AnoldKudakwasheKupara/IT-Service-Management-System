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

    // ── Major incident management ───────────────────────────────────────────────
    /// <summary>Priority band of a major incident (P1/P2 in ITIL terms).</summary>
    public enum MajorIncidentSeverity { Sev1, Sev2 }

    /// <summary>Coordinated-response lifecycle of a major incident.</summary>
    public enum MajorIncidentStatus { Declared, Investigating, Identified, Recovering, Resolved, Review, Closed }

    /// <summary>Kind of entry recorded on the response timeline.</summary>
    public enum MajorIncidentEventType { Update, StatusChange, Action, Decision, Escalation, Communication }

    /// <summary>Channel a stakeholder update was issued through.</summary>
    public enum StakeholderChannel { Email, StatusPage, Chat, Phone, Briefing, Other }

    /// <summary>Status of a post-incident follow-up action.</summary>
    public enum FollowUpStatus { Open, InProgress, Done, Cancelled }
}
