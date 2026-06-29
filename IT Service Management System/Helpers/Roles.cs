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

        /// <summary>Roles with full visibility across every module.</summary>
        public static readonly string[] FullAccess = { Admin, SystemsAdmin };

        /// <summary>Full-access roles plus HR (for the HR Management module &amp; HR reports).</summary>
        public static readonly string[] HrAndAdmins = { Admin, SystemsAdmin, HR };

        public static bool IsFullAccess(string? role) => role == Admin || role == SystemsAdmin;
        public static bool IsHr(string? role) => role == HR;
    }
}
