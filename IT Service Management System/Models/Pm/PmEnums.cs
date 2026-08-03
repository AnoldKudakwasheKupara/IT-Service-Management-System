namespace IT_Service_Management_System.Models.Pm
{
    // ── Portfolio ───────────────────────────────────────────────────────────────
    /// <summary>Lifecycle of a project, from first draft through to the archive.</summary>
    public enum ProjectStatus { Draft, Planning, Approved, Active, OnHold, Delayed, Completed, Cancelled, Archived }

    public enum ProjectPriority { Low, Medium, High, Critical }

    /// <summary>Delivery approach — drives which planning surfaces make sense.</summary>
    public enum ProjectType { Internal, Client, Capital, Operational, Research, Maintenance, Compliance }

    /// <summary>Business category, used for portfolio grouping and reporting.</summary>
    public enum ProjectCategory { Software, Infrastructure, Construction, Marketing, Research, Maintenance, IsoCompliance, Training, Procurement, Other }

    /// <summary>Traffic-light roll-up derived from schedule, budget and risk.</summary>
    public enum ProjectHealth { Green, Amber, Red }

    /// <summary>Role a person plays on a project team.</summary>
    public enum TeamRole { Manager, Sponsor, TeamLead, Member, Reviewer, Stakeholder, Observer }

    // ── Planning ────────────────────────────────────────────────────────────────
    public enum PhaseStatus { NotStarted, InProgress, Completed, OnHold, Cancelled }

    public enum MilestoneStatus { Planned, AtRisk, Achieved, Missed, Cancelled }

    public enum DeliverableStatus { NotStarted, InProgress, Submitted, UnderReview, Accepted, Rejected }

    /// <summary>Predecessor→successor relationship type (PMI standard four).</summary>
    public enum DependencyType { FinishToStart, StartToStart, FinishToFinish, StartToFinish }

    // ── Tasks ───────────────────────────────────────────────────────────────────
    public enum ProjectTaskStatus { NotStarted, Assigned, InProgress, Waiting, Blocked, UnderReview, Completed, Cancelled }

    /// <summary>Kanban swim-lane the task currently sits in (independent of its status).</summary>
    public enum KanbanColumn { Backlog, Ready, InProgress, Review, Testing, Completed }

    public enum TaskPriority { Low, Medium, High, Critical }

    // ── Resources & time ────────────────────────────────────────────────────────
    public enum ResourceType { Person, Equipment, Vehicle, SoftwareLicence, MeetingRoom, Material, Facility }

    public enum ResourceStatus { Available, Allocated, Unavailable, InMaintenance, Retired }

    public enum TimeEntryType { Regular, Overtime, Break, Travel, Training }

    public enum TimeEntryStatus { Draft, Submitted, Approved, Rejected }

    // ── Budget, expenses & procurement ──────────────────────────────────────────
    /// <summary>Where a budget line or expense sits in the standard cost breakdown.</summary>
    public enum CostCategory { Labour, Equipment, Software, Travel, Accommodation, Fuel, Internet, Meals, OfficeSupplies, Subcontract, Training, Contingency, Other }

    public enum ExpenseStatus { Draft, Submitted, Approved, Rejected, Reimbursed }

    public enum ProcurementStatus { Draft, Submitted, Approved, Rejected, RfqIssued, SupplierSelected, Ordered, GoodsReceived, Invoiced, Paid, Cancelled }

    public enum InvoiceStatus { Received, UnderReview, Approved, Disputed, Paid, Cancelled }

    // ── Governance: risk, issue, change, quality ────────────────────────────────
    public enum PmRiskStatus { Identified, Assessed, Mitigating, Monitoring, Realised, Closed }

    /// <summary>PMI risk response strategy.</summary>
    public enum RiskResponse { Avoid, Mitigate, Transfer, Accept, Escalate }

    public enum IssueSeverity { Low, Medium, High, Critical }

    public enum IssueStatus { Open, Investigating, InProgress, Resolved, Closed, Deferred }

    public enum ChangeRequestStatus { Draft, Submitted, UnderReview, Approved, Rejected, Implemented, Cancelled }

    public enum ChangeImpactLevel { Negligible, Minor, Moderate, Major, Severe }

    public enum QualityCheckType { Inspection, Test, Review, Audit, AcceptanceCheck }

    public enum QualityResult { Pending, Pass, Fail, PassWithObservations }

    // ── Documents ───────────────────────────────────────────────────────────────
    public enum ProjectDocumentType { Contract, Quotation, Design, Report, MeetingMinutes, Photo, Video, Drawing, Specification, Certificate, Invoice, Other }

    public enum ProjectDocumentStatus { Draft, UnderReview, Approved, Rejected, Superseded, Archived }

    // ── Approvals ───────────────────────────────────────────────────────────────
    /// <summary>What is being approved — drives routing and the return link.</summary>
    public enum ApprovalSubject { Project, Budget, Expense, ChangeRequest, Document, Purchase, Milestone, Closure, Timesheet }

    public enum ApprovalStatus { Pending, Approved, Rejected, Cancelled, Delegated }

    // ── Meetings & communication ────────────────────────────────────────────────
    public enum ProjectMeetingStatus { Scheduled, InProgress, Completed, Cancelled }

    public enum AttendanceState { Invited, Accepted, Declined, Attended, Absent }

    // ── Closure ─────────────────────────────────────────────────────────────────
    public enum ClosureStatus { NotStarted, InProgress, AwaitingAcceptance, Accepted, Closed }

    /// <summary>Whether a lesson learned was a positive or negative experience.</summary>
    public enum LessonCategory { WhatWentWell, WhatWentWrong, Recommendation, ProcessImprovement }

    // ── Notifications ───────────────────────────────────────────────────────────
    public enum PmNotificationType { TaskAssigned, DeadlineApproaching, TaskOverdue, BudgetExceeded, RiskRaised, IssueRaised, ApprovalPending, ApprovalDecided, MilestoneAchieved, StatusChanged, Mention, Comment }
}
