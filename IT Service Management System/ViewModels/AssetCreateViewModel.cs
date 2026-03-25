using IT_Service_Management_System.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace IT_Service_Management_System.ViewModels
{
    public class AssetCreateViewModel
    {
        public Asset Asset { get; set; }
        [ValidateNever]
        public List<User> Users { get; set; }
    }
}