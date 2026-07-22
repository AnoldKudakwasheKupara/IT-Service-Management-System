using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;

namespace IT_Service_Management_System.Models
{
    public class Asset
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Transaction date is required.")]
        [DataType(DataType.Date)]
        [Display(Name = "Transaction Date")]
        public DateTime Date { get; set; }

        public int? UserId { get; set; }
        [ValidateNever]
        public User? User { get; set; }

        [StringLength(50, ErrorMessage = "Asset tag cannot exceed 50 characters.")]
        public string? AssetTag { get; set; }

        [Required(ErrorMessage = "Item name is required.")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Item name must be between 2 and 100 characters.")]
        [Display(Name = "Item Name")]
        public string ItemName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Serial number is required.")]
        [StringLength(100, ErrorMessage = "Serial number cannot exceed 100 characters.")]
        [Display(Name = "Serial Number")]
        public string SerialNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Action type is required.")]
        [Display(Name = "Action Type")]
        public string ActionType { get; set; } = string.Empty;

        [Required(ErrorMessage = "Condition is required.")]
        public string Condition { get; set; } = string.Empty;

        [Required(ErrorMessage = "Issued by is required.")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Issued by must be between 2 and 100 characters.")]
        [Display(Name = "Issued By")]
        public string IssuedBy { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "Remarks cannot exceed 500 characters.")]
        public string Remarks { get; set; } = string.Empty;

        [DataType(DataType.Date)]
        [Display(Name = "Purchase Date")]
        public DateTime? PurchaseDate { get; set; }

        [Range(0, 9999999, ErrorMessage = "Purchase cost must be a positive amount.")]
        [Display(Name = "Purchase Cost")]
        public decimal? PurchaseCost { get; set; }

        public string? Status { get; set; }

        public string? EventType { get; set; }
        [ValidateNever]
        public ICollection<AssetHistory> History { get; set; } = new List<AssetHistory>();
    }
}