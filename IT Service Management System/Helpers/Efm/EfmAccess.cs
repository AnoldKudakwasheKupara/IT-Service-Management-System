using IT_Service_Management_System.Models.Efm;

namespace IT_Service_Management_System.Helpers.Efm
{
    /// <summary>Granular capabilities within Employee File Management.</summary>
    public enum EfmPermission
    {
        View, Download, Print, Upload, NewVersion, RestoreVersion,
        Delete, Archive, Restore, Approve, Reject, ManageConfig,
        BulkUpload, BulkDelete, ViewAudit
    }

    /// <summary>
    /// Central permission map for the document module. Maps the app's session roles onto the
    /// EFM role tiers from the spec and enforces confidentiality:
    ///   Admin / SystemsAdmin → HR Administrator + System Administrator (full access)
    ///   HR                   → HR Officer (all document actions except system config)
    ///   Auditor              → read-only + audit
    ///   everyone else        → self-service only (their own documents, via ownership checks)
    /// </summary>
    public static class EfmAccess
    {
        public static bool IsFullAccess(string? role) => role == "Admin" || role == "SystemsAdmin";
        public static bool IsHrOfficer(string? role) => role == "HR";
        public static bool IsAuditor(string? role) => role == "Auditor";

        /// <summary>Can this role work with the HR document workspace at all (vs. self-service only)?</summary>
        public static bool IsStaff(string? role) => IsFullAccess(role) || IsHrOfficer(role) || IsAuditor(role);

        /// <summary>Highest confidentiality a role may view. Restricted is admin-only.</summary>
        public static bool CanSeeConfidentiality(string? role, ConfidentialityLevel level)
        {
            if (IsFullAccess(role)) return true;
            if (IsHrOfficer(role) || IsAuditor(role)) return level < ConfidentialityLevel.Restricted;
            return false;
        }

        /// <summary>Does the role hold a given capability (before ownership/confidentiality checks)?</summary>
        public static bool Can(string? role, EfmPermission p)
        {
            if (IsFullAccess(role)) return true;                       // everything
            if (IsAuditor(role))
                return p is EfmPermission.View or EfmPermission.Download or EfmPermission.Print or EfmPermission.ViewAudit;
            if (IsHrOfficer(role))
                return p != EfmPermission.ManageConfig;                // all doc actions, not system config
            return false;                                             // self-service handled by ownership
        }
    }
}
