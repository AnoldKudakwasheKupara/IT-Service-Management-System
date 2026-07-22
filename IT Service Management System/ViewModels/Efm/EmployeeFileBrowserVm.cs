using IT_Service_Management_System.Models;
using IT_Service_Management_System.Models.Efm;

namespace IT_Service_Management_System.ViewModels.Efm
{
    /// <summary>Backing model for an employee's digital-file browser page.</summary>
    public class EmployeeFileBrowserVm
    {
        public User Employee { get; set; } = null!;
        public List<DocumentFolder> Folders { get; set; } = new();
        public Dictionary<int, int> FolderCounts { get; set; } = new();
        public int? SelectedFolderId { get; set; }
        public List<EmployeeDocument> Documents { get; set; } = new();
        public List<DocumentCategory> Categories { get; set; } = new();
        public int TotalDocuments { get; set; }

        /// <summary>Open document requests addressed to this employee (self-service page only).</summary>
        public List<DocumentRequest> PendingRequests { get; set; } = new();
    }
}
