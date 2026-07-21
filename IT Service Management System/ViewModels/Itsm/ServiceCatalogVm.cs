using System.ComponentModel.DataAnnotations;
using IT_Service_Management_System.Models.Itsm;

namespace IT_Service_Management_System.ViewModels.Itsm
{
    public class ServiceCatalogIndexVm
    {
        public List<ServiceCatalogItem> Items { get; set; } = new();
        public List<string> Categories { get; set; } = new();
        public string? Query { get; set; }
        public string? Category { get; set; }
        public bool CanManage { get; set; }
    }

    public class SubmitServiceRequestVm
    {
        public int ServiceCatalogItemId { get; set; }
        public ServiceCatalogItem? Item { get; set; }

        [Required, StringLength(200)]
        public string Subject { get; set; } = string.Empty;

        [Required, StringLength(4000)]
        public string Details { get; set; } = string.Empty;

        [StringLength(2000)]
        public string? BusinessJustification { get; set; }
    }
}
