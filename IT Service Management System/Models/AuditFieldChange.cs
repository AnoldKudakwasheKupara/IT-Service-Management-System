namespace IT_Service_Management_System.Models
{
    /// <summary>
    /// One property's before/after value inside an audit entry. Values are rendered as text;
    /// sensitive properties are stored as <see cref="Redacted"/> instead of their real values.
    /// </summary>
    public class AuditFieldChange
    {
        public string Field { get; set; } = string.Empty;
        public string? Old { get; set; }
        public string? New { get; set; }

        public const string Redacted = "«redacted»";

        public bool IsRedacted => Old == Redacted || New == Redacted;
    }
}
