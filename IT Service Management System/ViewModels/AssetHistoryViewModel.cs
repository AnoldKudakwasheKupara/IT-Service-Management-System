using IT_Service_Management_System.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace IT_Service_Management_System.ViewModels
{
    public class AssetHistoryViewModel
    {
        public int AssetId { get; set; }

        public DateTime Date { get; set; }

        public int? UserId { get; set; }

        public string EventType { get; set; }
        public string Condition { get; set; }

        public string PerformedBy { get; set; }
        public string Remarks { get; set; }

        [ValidateNever]
        public List<User> Users { get; set; }
    }
}