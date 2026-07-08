namespace IT_Service_Management_System.ViewModels.Ecie
{
    /// <summary>A titled list of evidence references, rendered by the _Refs partial.</summary>
    public record RefSection(string Title, string Icon, List<EvidenceRef> Items);

    /// <summary>A titled list of note strings (recommendations / next actions / risks), rendered by _Notes.</summary>
    public record NoteSection(string Title, string Icon, List<string> Items);
}
