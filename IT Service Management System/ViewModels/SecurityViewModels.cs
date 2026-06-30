using IT_Service_Management_System.Models;
using IT_Service_Management_System.ViewModels.Reports;

namespace IT_Service_Management_System.ViewModels.Security
{
    public class SecurityDashboardVM
    {
        public DateTime GeneratedAt { get; set; }

        // KPIs
        public int FailedLogins24h { get; set; }
        public int FailedLogins7d { get; set; }
        public int LockedUsersCount { get; set; }
        public int ActiveSessionsCount { get; set; }
        public int ExpiredPasswordsCount { get; set; }
        public int DisabledAccountsCount { get; set; }
        public int MfaEnabledCount { get; set; }
        public int TotalUsers { get; set; }

        public int PasswordExpiryDays { get; set; }
        public bool MfaEnabled { get; set; }

        // Detail lists
        public List<User> LockedUsers { get; set; } = new();
        public List<User> DisabledAccounts { get; set; } = new();
        public List<User> ExpiredPasswordUsers { get; set; } = new();
        public List<UserSession> ActiveSessions { get; set; } = new();
        public List<AuditLog> RecentSecurityEvents { get; set; } = new();
        public List<NameCount> TopFailedLoginTargets { get; set; } = new();
    }
}
