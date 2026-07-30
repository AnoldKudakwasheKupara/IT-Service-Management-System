namespace IT_Service_Management_System.Helpers
{
    /// <summary>Role name constants (match Ticket.UserRole) and common role groups.</summary>
    public static class Roles
    {
        public const string Admin = "Admin";
        public const string Finance = "Finance";
        public const string SystemsAdmin = "SystemsAdmin";
        public const string Development = "Development";
        public const string HR = "HR";
        public const string Employee = "Employee";
        public const string SupportAgent = "SupportAgent";

        // ── IMS / ISO roles (Integrated Management System module) ──
        public const string QualityManager = "QualityManager";
        public const string GeneralManager = "GeneralManager";
        public const string DepartmentManager = "DepartmentManager";
        public const string Auditor = "Auditor";
        public const string DocumentController = "DocumentController";
        public const string ExternalAuditor = "ExternalAuditor";

        /// <summary>Roles with full visibility across every module.</summary>
        public static readonly string[] FullAccess = { Admin, SystemsAdmin };

        /// <summary>Helpdesk staff — full ticket-queue access (admins plus front-line support agents).</summary>
        public static readonly string[] HelpdeskStaff = { Admin, SystemsAdmin, SupportAgent };

        /// <summary>True when the role works the helpdesk queue as staff (not just as a requester).</summary>
        public static bool IsHelpdeskStaff(string? role) =>
            role == Admin || role == SystemsAdmin || role == SupportAgent;

        /// <summary>
        /// Enum overload, for checks against a persisted <see cref="Models.Ticket.UserRole"/> rather than
        /// the role string carried in session (e.g. vetting a proposed ticket assignee).
        /// </summary>
        public static bool IsHelpdeskStaff(Models.Ticket.UserRole role) =>
            role is Models.Ticket.UserRole.Admin
                or Models.Ticket.UserRole.SystemsAdmin
                or Models.Ticket.UserRole.SupportAgent;

        /// <summary>Full-access roles plus HR (for the HR Management module &amp; HR reports).</summary>
        public static readonly string[] HrAndAdmins = { Admin, SystemsAdmin, HR };

        /// <summary>IMS administrators — configure the management system and approve at management-review level.</summary>
        public static readonly string[] ImsManagers = { Admin, SystemsAdmin, QualityManager };

        /// <summary>Roles that create &amp; maintain ISO records (documents, audits, CAPAs, risks, suppliers…).</summary>
        public static readonly string[] ImsContributors = { Admin, SystemsAdmin, QualityManager, DocumentController, DepartmentManager, Auditor };

        /// <summary>Every role permitted to open an IMS / ISO surface, including signatories and read-only users.</summary>
        public static readonly string[] ImsAll = { Admin, SystemsAdmin, QualityManager, GeneralManager, DocumentController, DepartmentManager, Auditor, Employee, ExternalAuditor };

        public static bool IsFullAccess(string? role) => role == Admin || role == SystemsAdmin;
        public static bool IsHr(string? role) => role == HR;

        /// <summary>Administrators and the Quality Manager, who run the management system.</summary>
        public static bool IsImsManager(string? role) => role == Admin || role == SystemsAdmin || role == QualityManager;
    }
}
