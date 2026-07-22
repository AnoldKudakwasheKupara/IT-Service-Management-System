using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace IT_Service_Management_System.Models
{
    public class UserAccessRightItem
    {
        public int Id { get; set; }

        public int UserAccessRightsId { get; set; }

        [ValidateNever]
        public UserAccessRight? UserAccessRight { get; set; }

        public string? UserName { get; set; }

        public bool UserManagement { get; set; }

        public bool Initiate { get; set; }

        public bool Confirmation { get; set; }

        public bool Approval { get; set; }

        public bool AccountManagement { get; set; }

        public bool Reports { get; set; }
    }
}