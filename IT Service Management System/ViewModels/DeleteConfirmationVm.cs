namespace IT_Service_Management_System.ViewModels
{
    /// <summary>
    /// Drives the shared delete-confirmation page (Views/Shared/DeleteConfirm.cshtml), which mirrors
    /// the pattern established by Views/Tickets/Delete.cshtml: show exactly what is about to be
    /// destroyed, spell out the consequences, and gate the button behind an explicit acknowledgement.
    /// Modules whose records don't warrant a bespoke Delete view can reuse it from their GET Delete.
    /// </summary>
    public class DeleteConfirmationVm
    {
        /// <summary>Human name of the thing being deleted, e.g. "Risk".</summary>
        public string EntityName { get; set; } = "record";

        /// <summary>Font Awesome icon for the record, e.g. "fa-gauge-high".</summary>
        public string Icon { get; set; } = "fa-file-lines";

        /// <summary>Primary label of the record being deleted.</summary>
        public string RecordTitle { get; set; } = "";

        /// <summary>Optional reference/code shown as a badge, e.g. "RSK-00007".</summary>
        public string? Reference { get; set; }

        /// <summary>Field/value pairs summarising the record.</summary>
        public List<KeyValuePair<string, string>> Details { get; set; } = new();

        /// <summary>What will be lost, rendered as the red consequences panel.</summary>
        public List<string> Consequences { get; set; } = new();

        /// <summary>Controller that owns the delete (defaults to the current one when null).</summary>
        public string? Controller { get; set; }

        /// <summary>Id posted back to the Delete action.</summary>
        public int Id { get; set; }

        /// <summary>Where Cancel and the back arrow go. Defaults to the record's Details page.</summary>
        public string CancelAction { get; set; } = "Details";

        /// <summary>When false, Cancel links to the action without an id (e.g. Index).</summary>
        public bool CancelWithId { get; set; } = true;

        public void Add(string label, string? value) =>
            Details.Add(new KeyValuePair<string, string>(label, string.IsNullOrWhiteSpace(value) ? "—" : value));
    }
}
