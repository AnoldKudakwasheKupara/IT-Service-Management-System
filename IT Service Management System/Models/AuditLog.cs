using System.Text.Json;

namespace IT_Service_Management_System.Models
{
    /// <summary>
    /// One immutable entry in the system-wide audit trail.
    ///
    /// Entries arrive from two sources: <see cref="AuditSource.Automatic"/> rows written by the
    /// EF Core save interceptor (every insert/update/delete, with per-field before/after values),
    /// and <see cref="AuditSource.Manual"/> rows written explicitly for things that are not row
    /// changes — logins, exports, approvals, background jobs.
    ///
    /// Rows are chained: each carries the hash of its predecessor, so any later edit or deletion
    /// breaks the chain and is detectable. Nothing in the application updates or deletes an entry
    /// except the retention purge, which records what it removed.
    /// </summary>
    public class AuditLog
    {
        public int Id { get; set; }

        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;

        /// <summary>Role of the acting user at the time of the event.</summary>
        public string? UserRole { get; set; }

        public string Action { get; set; } = string.Empty;
        public string Entity { get; set; } = string.Empty;
        public int? EntityId { get; set; }

        /// <summary>
        /// Primary key rendered as text. Carries composite and non-integer keys that
        /// <see cref="EntityId"/> cannot represent.
        /// </summary>
        public string? EntityKey { get; set; }

        public string Details { get; set; } = string.Empty;

        /// <summary>
        /// JSON array of <see cref="AuditFieldChange"/> — the per-property before/after values for
        /// an automatic change. Null for events that are not row changes.
        /// </summary>
        public string? Changes { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        public string IpAddress { get; set; } = string.Empty;
        public string? Location { get; set; }
        public string? Device { get; set; }

        // ── Request context ──────────────────────────────────────────────────────
        /// <summary>Groups every entry produced by one HTTP request or background job run.</summary>
        public string? CorrelationId { get; set; }
        public string? RequestPath { get; set; }
        public string? HttpMethod { get; set; }

        public AuditSource Source { get; set; } = AuditSource.Manual;

        // ── Tamper evidence ──────────────────────────────────────────────────────
        /// <summary>SHA-256 over this entry's content plus <see cref="PreviousHash"/>.</summary>
        public string? Hash { get; set; }
        /// <summary>Hash of the previous entry; null on the first entry of the chain.</summary>
        public string? PreviousHash { get; set; }

        /// <summary>Deserialized field changes, or an empty list when there are none.</summary>
        public IReadOnlyList<AuditFieldChange> ChangeList()
        {
            if (string.IsNullOrWhiteSpace(Changes)) return Array.Empty<AuditFieldChange>();
            try
            {
                return JsonSerializer.Deserialize<List<AuditFieldChange>>(Changes)
                       ?? (IReadOnlyList<AuditFieldChange>)Array.Empty<AuditFieldChange>();
            }
            catch (JsonException)
            {
                return Array.Empty<AuditFieldChange>();
            }
        }
    }

    public enum AuditSource
    {
        /// <summary>Written by an explicit LogAsync call — logins, exports, workflow steps.</summary>
        Manual = 0,
        /// <summary>Written by the EF Core interceptor from a tracked entity change.</summary>
        Automatic = 1,
        /// <summary>Written by a background job with no acting user.</summary>
        System = 2
    }
}
