namespace IT_Service_Management_System.Helpers
{
    /// <summary>
    /// Capability checks driven by the session role, for controllers and views alike.
    /// The product has two tiers:
    ///   • administrators (Admin / SystemsAdmin) — see and change everything, including deletions,
    ///     reports and system settings;
    ///   • everyone else — a minimal personal workspace. They may create and edit their own
    ///     records, but never delete, never open an administrative surface, and never see reports.
    /// Helpdesk agents sit in between: they work the ticket queue as staff, but they are not
    /// administrators, so destructive and system actions stay closed to them.
    /// </summary>
    public static class Permissions
    {
        public static string? RoleOf(this HttpContext context) =>
            context.Session.GetString("UserRole");

        /// <summary>True for administrators — the only tier allowed to change the system itself.</summary>
        public static bool IsAdministrator(this HttpContext context) =>
            Roles.IsFullAccess(context.RoleOf());

        /// <summary>Deleting records is an administrator-only action, everywhere in the product.</summary>
        public static bool CanDelete(this HttpContext context) => context.IsAdministrator();

        /// <summary>Administrative surfaces: users, departments, configuration, security, audit…</summary>
        public static bool CanAdminister(this HttpContext context) => context.IsAdministrator();

        /// <summary>Reports are an administrative view.</summary>
        public static bool CanViewReports(this HttpContext context) => context.IsAdministrator();

        /// <summary>True for helpdesk staff — may work tickets raised by other people.</summary>
        public static bool IsHelpdeskStaff(this HttpContext context) =>
            Roles.IsHelpdeskStaff(context.RoleOf());
    }
}
