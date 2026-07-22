using IT_Service_Management_System.Models.Efm;

namespace IT_Service_Management_System.ViewModels.Efm
{
    /// <summary>Backing model for the cross-employee document search screen.</summary>
    public class DocumentSearchVm
    {
        public List<EmployeeDocument> Results { get; set; } = new();
        public List<DocumentFolder> Folders { get; set; } = new();
        public List<DocumentCategory> Categories { get; set; } = new();

        // Current filter values (echoed back into the form).
        public string? Q { get; set; }
        public int? FolderId { get; set; }
        public int? CategoryId { get; set; }
        public DocumentStatus? Status { get; set; }
        public string? Expiry { get; set; }         // "", "expired", "expiring"
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public bool Archived { get; set; }           // browse the archive instead of active files
    }
}
