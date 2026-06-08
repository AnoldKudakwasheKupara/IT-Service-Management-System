using Microsoft.AspNetCore.Mvc.Rendering;

namespace IT_Service_Management_System.ViewModels
{
    public class CreateExitClearanceVM
    {
        public int EmployeeId { get; set; }

        public List<SelectListItem> Employees { get; set; }
            = new();
    }
}