using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Models;
using IT_Service_Management_System.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using static IT_Service_Management_System.Models.Ticket;

namespace IT_Service_Management_System.Controllers
{
    public class ExitClearanceController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ExitClearanceController(ApplicationDbContext context)
        {
            _context = context;
        }




        [HttpGet]
        public IActionResult Create()
        {
            var vm = new CreateExitClearanceVM();

            vm.Employees = _context.Users
                .Where(x => x.IsActive)
                .Select(x => new SelectListItem
                {
                    Value = x.Id.ToString(),
                    Text = x.FirstName + " " + x.LastName
                })
                .ToList();

            return View(vm);
        }

        [HttpPost]
        public IActionResult Create(CreateExitClearanceVM vm)
        {
            var employee = _context.Users
                .FirstOrDefault(x => x.Id == vm.EmployeeId);

            if (employee == null)
            {
                return NotFound();
            }

            var clearance = new ExitClearance
            {
                EmployeeId = employee.Id,
                Status = ClearanceStatus.InProgress,
                CurrentStage = ClearanceStage.Employee,
                AccessToken = Guid.NewGuid().ToString()
            };

            _context.ExitClearances.Add(clearance);

            _context.SaveChanges();

            return RedirectToAction(nameof(Details),
                new { id = clearance.Id });
        }

        [HttpGet]
        public IActionResult Complete(string id)
        {
            var clearance = _context.ExitClearances
                .Include(x => x.Employee)
                .FirstOrDefault(x => x.AccessToken == id);

            if (clearance == null)
                return NotFound();

            var vm = new EmployeeClearanceVM
            {
                ExitClearanceId = clearance.Id,
                AccessToken = clearance.AccessToken,

                Surname = clearance.Employee.LastName,
                Email = clearance.Employee.Email,
                Department = clearance.Employee.Department?.Name ?? "",
                Supervisor = clearance.Employee.Supervisor != null
                    ? $"{clearance.Employee.Supervisor.FirstName} {clearance.Employee.Supervisor.LastName}"
                    : ""
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Complete(EmployeeClearanceVM vm)
        {
            if (!ModelState.IsValid)
            {
                return View(vm);
            }

            var clearance = _context.ExitClearances
                .FirstOrDefault(x => x.Id == vm.ExitClearanceId);

            if (clearance == null)
            {
                return NotFound();
            }

            var details = new ExitClearanceEmployeeDetails
            {
                ExitClearanceId = clearance.Id,

                Surname = vm.Surname,
                Department = vm.Department,
                Supervisor = vm.Supervisor,
                PositionHeld = vm.PositionHeld,
                LengthOfService = vm.LengthOfService,

                DateOfNotice = vm.DateOfNotice,
                LastDayOfWork = vm.LastDayOfWork,

                Email = vm.Email,
                ForwardingAddress = vm.ForwardingAddress,

                EmployeeSignature = vm.EmployeeSignature,
                SignedDate = DateTime.Now
            };

            _context.ExitClearanceEmployeeDetails.Add(details);

            var employeeWorkflow = _context.ClearanceWorkflows
                .FirstOrDefault(x =>
                    x.ExitClearanceId == clearance.Id &&
                    x.Stage == ClearanceStage.Employee &&
                    !x.Completed);

            if (employeeWorkflow != null)
            {
                employeeWorkflow.Completed = true;
                employeeWorkflow.CompletedDate = DateTime.Now;
            }

            var financeUser = _context.Users
                .FirstOrDefault(x => x.Role == UserRole.Finance);

            if (financeUser == null)
            {
                ModelState.AddModelError("", "No Finance user configured.");
                return View(vm);
            }

            clearance.CurrentStage = ClearanceStage.Finance;
            clearance.EmployeeSubmittedDate = DateTime.Now;

            _context.ClearanceWorkflows.Add(
                new ClearanceWorkflow
                {
                    ExitClearanceId = clearance.Id,
                    Stage = ClearanceStage.Finance,
                    AssignedToUserId = financeUser.Id,
                    AssignedDate = DateTime.Now
                });

            _context.SaveChanges();

            return View("SubmissionSuccess");
        }

        public IActionResult Details(int id)
        {
            var clearance = _context.ExitClearances
                .Include(x => x.Employee)
                .FirstOrDefault(x => x.Id == id);

            if (clearance == null)
                return NotFound();

            return View(clearance);
        }
    }
}
