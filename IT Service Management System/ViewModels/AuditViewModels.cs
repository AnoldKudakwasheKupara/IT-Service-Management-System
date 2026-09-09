using IT_Service_Management_System.Models;
using IT_Service_Management_System.Services.Auditing;

namespace IT_Service_Management_System.ViewModels
{
    /// <summary>
    /// The filter behind the audit-trail list, its export and its stat cards. Bound straight from
    /// the query string so every view, the pager and the export link share one shape.
    /// </summary>
    public class AuditFilter
    {
        public string? Q { get; set; }
        public string? User { get; set; }
        public string? Action { get; set; }
        public string? Entity { get; set; }
        public AuditSource? Source { get; set; }
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }

        public bool IsActive =>
            !string.IsNullOrWhiteSpace(Q) || !string.IsNullOrWhiteSpace(User) ||
            !string.IsNullOrWhiteSpace(Action) || !string.IsNullOrWhiteSpace(Entity) ||
            Source.HasValue || From.HasValue || To.HasValue;

        /// <summary>Route values for links that must keep the current filter (export, pager).</summary>
        public Dictionary<string, string?> RouteValues()
        {
            var values = new Dictionary<string, string?>();
            if (!string.IsNullOrWhiteSpace(Q)) values["q"] = Q;
            if (!string.IsNullOrWhiteSpace(User)) values["user"] = User;
            if (!string.IsNullOrWhiteSpace(Action)) values["action"] = Action;
            if (!string.IsNullOrWhiteSpace(Entity)) values["entity"] = Entity;
            if (Source.HasValue) values["source"] = Source.Value.ToString();
            if (From.HasValue) values["from"] = From.Value.ToString("yyyy-MM-dd");
            if (To.HasValue) values["to"] = To.Value.ToString("yyyy-MM-dd");
            return values;
        }

        /// <summary>A human description of the filter, for the export header and audit details.</summary>
        public string Describe()
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(Q)) parts.Add($"text “{Q}”");
            if (!string.IsNullOrWhiteSpace(User)) parts.Add($"user {User}");
            if (!string.IsNullOrWhiteSpace(Action)) parts.Add($"action {Action}");
            if (!string.IsNullOrWhiteSpace(Entity)) parts.Add($"entity {Entity}");
            if (Source.HasValue) parts.Add($"source {Source}");
            if (From.HasValue) parts.Add($"from {From:yyyy-MM-dd}");
            if (To.HasValue) parts.Add($"to {To:yyyy-MM-dd}");
            return parts.Count == 0 ? "no filter" : string.Join(", ", parts);
        }
    }

    /// <summary>The audit-trail list: entries, the filter that produced them, and its dropdowns.</summary>
    public class AuditIndexViewModel
    {
        public List<AuditLog> Entries { get; set; } = new();
        public AuditFilter Filter { get; set; } = new();

        public int TotalCount { get; set; }
        public int CreatedCount { get; set; }
        public int UpdatedCount { get; set; }
        public int DeletedCount { get; set; }

        public List<string> Actions { get; set; } = new();
        public List<string> Entities { get; set; } = new();
        public List<string> Users { get; set; } = new();
    }

    /// <summary>One entry, its field-level diff, and the rest of the request it belonged to.</summary>
    public class AuditDetailsViewModel
    {
        public AuditLog Entry { get; set; } = null!;
        public IReadOnlyList<AuditFieldChange> Changes { get; set; } = Array.Empty<AuditFieldChange>();
        /// <summary>Other entries written by the same request, so one action reads as one story.</summary>
        public List<AuditLog> RelatedInRequest { get; set; } = new();
        /// <summary>True when this entry's own hash still matches its contents and its predecessor.</summary>
        public bool HashValid { get; set; }
    }

    /// <summary>Everything that ever happened to one record, oldest first.</summary>
    public class AuditTimelineViewModel
    {
        public string Entity { get; set; } = string.Empty;
        public int EntityId { get; set; }
        public string? Subject { get; set; }
        public List<AuditLog> Entries { get; set; } = new();
    }

    /// <summary>Result of a tamper-evidence check over the stored chain.</summary>
    public class AuditIntegrityViewModel
    {
        public AuditIntegrityResult Result { get; set; } = new();
        public AuditLog? BrokenEntry { get; set; }
    }
}
