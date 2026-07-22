using IT_Service_Management_System.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IT_Service_Management_System.Models
{
    public class MaintenanceRecord
    {
        public int Id { get; set; }
        public string? AssetName { get; set; }
        public string? EmployeeName { get; set; }
        public DateTime MaintenanceDate { get; set; }
        public MaintenanceType MaintenanceType { get; set; }

        [Display(Name = "Problem / Reason")]
        public string? ProblemDescription { get; set; }

        [Display(Name = "Work Done")]
        public string? WorkDone { get; set; }
        public string? PartsReplaced { get; set; }
        public string? SoftwareInstalled { get; set; }
        public string? Comments { get; set; }
        public DateTime? NextMaintenanceDate { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}