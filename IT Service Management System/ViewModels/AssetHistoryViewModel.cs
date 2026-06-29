using IT_Service_Management_System.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;

namespace IT_Service_Management_System.ViewModels
{
    public class AssetHistoryViewModel
    {
        public int AssetId { get; set; }

        [Required(ErrorMessage = "Date is required.")]
        [DataType(DataType.Date)]
        public DateTime Date { get; set; }

        [Display(Name = "User")]
        public int? UserId { get; set; }

        [Required(ErrorMessage = "Event type is required.")]
        [Display(Name = "Event Type")]
        public string EventType { get; set; } = string.Empty;

        [Required(ErrorMessage = "Condition is required.")]
        public string Condition { get; set; } = string.Empty;

        [Required(ErrorMessage = "Performed by is required.")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Performed by must be between 2 and 100 characters.")]
        [Display(Name = "Performed By")]
        public string PerformedBy { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "Remarks cannot exceed 500 characters.")]
        public string Remarks { get; set; } = string.Empty;

        [ValidateNever]
        public List<User> Users { get; set; } = new List<User>();
    }
}