using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace IT_Service_Management_System.Models
{
    public class Asset
    {
        public int Id { get; set; }

        public DateTime Date { get; set; }

        public int? UserId { get; set; }
        [ValidateNever]
        public User? User { get; set; }

        public string? AssetTag { get; set; }

        public string ItemName { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;

        public string ActionType { get; set; } = string.Empty;
        public string Condition { get; set; } = string.Empty;

        public string IssuedBy { get; set; } = string.Empty;
        public string Remarks { get; set; } = string.Empty;
        public DateTime? PurchaseDate { get; set; }
        public decimal? PurchaseCost { get; set; }
        public string? Status { get; set; }

        public string? EventType { get; set; }
        [ValidateNever]
        public ICollection<AssetHistory> History { get; set; } = new List<AssetHistory>();
    }
}