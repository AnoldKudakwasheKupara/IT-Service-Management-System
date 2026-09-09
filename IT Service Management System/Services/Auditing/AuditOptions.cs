namespace IT_Service_Management_System.Services.Auditing
{
    /// <summary>
    /// What the automatic (interceptor-driven) half of the audit trail captures.
    ///
    /// Capture is opt-out: every entity is audited unless its CLR type name appears in
    /// <see cref="ExcludedEntities"/>. The exclusions are all log-like or high-churn tables whose
    /// own rows are already the record of what happened — auditing them would multiply write
    /// volume without adding accountability, and auditing AuditLog itself would recurse.
    /// </summary>
    public class AuditOptions
    {
        /// <summary>Master switch for interceptor capture. Manual entries are unaffected.</summary>
        public bool CaptureEntityChanges { get; set; } = true;

        /// <summary>
        /// Record entities whose only change is a soft-delete flag as a Deleted event rather than
        /// an Updated one.
        /// </summary>
        public bool TreatSoftDeleteAsDelete { get; set; } = true;

        /// <summary>Values longer than this are truncated in the stored diff.</summary>
        public int MaxValueLength { get; set; } = 512;

        /// <summary>CLR type names that are never captured automatically.</summary>
        public HashSet<string> ExcludedEntities { get; } = new(StringComparer.OrdinalIgnoreCase)
        {
            // Auditing the audit trail would recurse forever.
            nameof(Models.AuditLog),
            // Append-only logs and notification queues — the rows are themselves the record.
            "NotificationLog",
            "DocumentAuditLog",
            "DocumentNotification",
            "IsoNotification",
            "PmNotification",
            "ProjectActivityLog",
            // Session rows churn on every request through last-seen/heartbeat updates.
            "UserSession"
        };

        /// <summary>
        /// Property names whose values never reach the trail. Matched case-insensitively against
        /// the exact property name, and by substring for the token list below, so a new column
        /// called e.g. "ResetPasswordTokenHash" is covered without being listed.
        /// </summary>
        public HashSet<string> RedactedProperties { get; } = new(StringComparer.OrdinalIgnoreCase)
        {
            "PasswordHash", "PasswordSalt", "Password", "ConfirmPassword",
            "MfaSecret", "RecoveryCodes", "ApiKey", "ClientSecret"
        };

        /// <summary>Any property whose name contains one of these is redacted.</summary>
        public string[] RedactedTokens { get; } =
            { "password", "secret", "token", "otp", "apikey", "salt", "credential" };

        /// <summary>Concurrency tokens and other plumbing that carries no business meaning.</summary>
        public HashSet<string> IgnoredProperties { get; } = new(StringComparer.OrdinalIgnoreCase)
        {
            "RowVersion", "ConcurrencyStamp"
        };

        public bool IsExcluded(string entityName) => ExcludedEntities.Contains(entityName);

        public bool IsRedacted(string propertyName)
        {
            if (RedactedProperties.Contains(propertyName)) return true;
            foreach (var token in RedactedTokens)
                if (propertyName.Contains(token, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        public bool IsIgnored(string propertyName) => IgnoredProperties.Contains(propertyName);
    }
}
