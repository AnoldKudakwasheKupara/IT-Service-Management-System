using IT_Service_Management_System.Models.Pm;

namespace IT_Service_Management_System.Helpers.Pm
{
    /// <summary>
    /// Per-project permission checks. Role alone decides who may open the module; this decides who
    /// may act on <em>a particular</em> project — its manager, its sponsor and its team members get
    /// rights that a peer project manager elsewhere in the portfolio does not.
    /// </summary>
    public static class PmAccess
    {
        /// <summary>True when the user may see the project at all.</summary>
        public static bool CanView(Project project, int userId, string? role, IEnumerable<int> teamUserIds)
        {
            if (Roles.IsFullAccess(role)) return true;
            // Clients only ever reach a project through the portal, which scopes by client name.
            if (Roles.IsClient(role)) return false;
            if (IsOwner(project, userId)) return true;
            if (teamUserIds.Contains(userId)) return true;
            // Executives, finance, procurement and auditors have portfolio-wide read access.
            return role is Roles.GeneralManager or Roles.Finance or Roles.Procurement
                or Roles.Auditor or Roles.DepartmentManager or Roles.ProjectManager;
        }

        /// <summary>True when the user may change the project's own record and its plan.</summary>
        public static bool CanEdit(Project project, int userId, string? role)
        {
            if (Roles.IsFullAccess(role)) return true;
            if (!project.IsOpen) return false;      // closed/archived projects are read-only
            return IsOwner(project, userId) || role is Roles.GeneralManager;
        }

        /// <summary>
        /// True when the user may work the delivery surfaces — tasks, risks, issues, documents.
        /// Wider than <see cref="CanEdit"/>: team leads and members contribute without owning.
        /// </summary>
        public static bool CanContribute(Project project, int userId, string? role, IEnumerable<int> teamUserIds)
        {
            if (CanEdit(project, userId, role)) return true;
            if (!project.IsOpen) return false;
            return teamUserIds.Contains(userId) || role is Roles.TeamLead or Roles.DepartmentManager;
        }

        /// <summary>True when the user may approve the project itself, or close it out.</summary>
        public static bool CanApprove(string? role) =>
            role is Roles.Admin or Roles.SystemsAdmin or Roles.GeneralManager;

        /// <summary>True when the user may approve money — budgets, expenses, purchases.</summary>
        public static bool CanApproveSpend(string? role) =>
            role is Roles.Admin or Roles.SystemsAdmin or Roles.Finance or Roles.GeneralManager;

        /// <summary>True when the user may permanently remove project records.</summary>
        public static bool CanDelete(Project project, int userId, string? role) =>
            Roles.IsFullAccess(role) || (project.IsOpen && project.ProjectManagerId == userId);

        /// <summary>The project's manager or sponsor.</summary>
        public static bool IsOwner(Project project, int userId) =>
            project.ProjectManagerId == userId || project.SponsorId == userId || project.CreatedById == userId;
    }
}
