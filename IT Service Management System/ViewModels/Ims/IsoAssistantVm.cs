namespace IT_Service_Management_System.ViewModels.Ims
{
    /// <summary>Result set for the AI compliance assistant — a grounded answer plus the records it found.</summary>
    public class IsoAssistantVm
    {
        public string? Query { get; set; }
        public string Answer { get; set; } = string.Empty;
        public List<AssistantResult> Results { get; set; } = new();
        public List<string> Suggestions { get; set; } = new();
    }

    public class AssistantResult
    {
        public string Title { get; set; } = string.Empty;
        public string? Subtitle { get; set; }
        public string? Badge { get; set; }
        public string? Url { get; set; }
        public string Icon { get; set; } = "fa-file-lines";
    }
}
