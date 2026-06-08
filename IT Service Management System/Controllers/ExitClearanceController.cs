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

        [HttpGet]
        public IActionResult FinanceQueue()
        {
            var clearances = _context.ExitClearances
                .Include(x => x.Employee)
                .Where(x => x.CurrentStage == ClearanceStage.Finance)
                .ToList();

            return View(clearances);
        }

        [HttpGet]
        public IActionResult FinanceReview(int id)
        {
            var clearance = _context.ExitClearances
                .Include(x => x.Employee)
                .FirstOrDefault(x => x.Id == id);

            if (clearance == null)
                return NotFound();

            var finance = _context.FinanceClearances
                .FirstOrDefault(x => x.ExitClearanceId == id);

            if (finance == null)
            {
                finance = new FinanceClearance
                {
                    ExitClearanceId = id
                };
            }

            return View(finance);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult FinanceReview(FinanceClearance model)
        {
            var clearance = _context.ExitClearances
                .FirstOrDefault(x => x.Id == model.ExitClearanceId);

            if (clearance == null)
                return NotFound();

            var finance = _context.FinanceClearances
                .FirstOrDefault(x =>
                    x.ExitClearanceId == model.ExitClearanceId);

            if (finance == null)
            {
                finance = model;

                _context.FinanceClearances.Add(finance);
            }
            else
            {
                finance.StaffAdvanceCleared = model.StaffAdvanceCleared;
                finance.ReceiptsHandedOver = model.ReceiptsHandedOver;
                finance.FinalDuesProcessed = model.FinalDuesProcessed;
                finance.Comments = model.Comments;
            }

            finance.ClearedDate = DateTime.Now;

            var currentWorkflow = _context.ClearanceWorkflows
                .FirstOrDefault(x =>
                    x.ExitClearanceId == clearance.Id &&
                    x.Stage == ClearanceStage.Finance &&
                    !x.Completed);

            if (currentWorkflow != null)
            {
                currentWorkflow.Completed = true;
                currentWorkflow.CompletedDate = DateTime.Now;
            }

            var systemsAdmin = _context.Users
                .FirstOrDefault(x =>
                    x.Role == UserRole.SystemsAdmin);

            clearance.CurrentStage = ClearanceStage.SystemsAdmin;

            _context.ClearanceWorkflows.Add(
                new ClearanceWorkflow
                {
                    ExitClearanceId = clearance.Id,
                    Stage = ClearanceStage.SystemsAdmin,
                    AssignedToUserId = systemsAdmin.Id,
                    AssignedDate = DateTime.Now
                });

            _context.SaveChanges();

            return RedirectToAction(nameof(FinanceQueue));
        }

        [HttpGet]
        public IActionResult SystemsAdminQueue()
        {
            var clearances = _context.ExitClearances
                .Include(x => x.Employee)
                .Where(x => x.CurrentStage == ClearanceStage.SystemsAdmin)
                .ToList();

            return View(clearances);
        }

        [HttpGet]
        public IActionResult SystemsAdminReview(int id)
        {
            var clearance = _context.ExitClearances
                .FirstOrDefault(x => x.Id == id);

            if (clearance == null)
                return NotFound();

            var model = _context.SystemsAdminClearances
                .FirstOrDefault(x => x.ExitClearanceId == id);

            if (model == null)
            {
                model = new SystemsAdminClearance
                {
                    ExitClearanceId = id
                };
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SystemsAdminReview(SystemsAdminClearance model)
        {
            var clearance = _context.ExitClearances
                .FirstOrDefault(x => x.Id == model.ExitClearanceId);

            if (clearance == null)
                return NotFound();

            var existing = _context.SystemsAdminClearances
                .FirstOrDefault(x =>
                    x.ExitClearanceId == model.ExitClearanceId);

            if (existing == null)
            {
                existing = model;

                _context.SystemsAdminClearances.Add(existing);
            }
            else
            {
                existing.LaptopReturned = model.LaptopReturned;
                existing.LaptopBagReturned = model.LaptopBagReturned;
                existing.AccessoriesReturned = model.AccessoriesReturned;
                existing.LockerReturned = model.LockerReturned;
                existing.EndpointSecurityRemoved = model.EndpointSecurityRemoved;
                existing.SophosCredentialsRemoved = model.SophosCredentialsRemoved;
                existing.AccessRemoved = model.AccessRemoved;
                existing.EmailDisabled = model.EmailDisabled;
                existing.EmailRedirected = model.EmailRedirected;
                existing.SocialMediaRemoved = model.SocialMediaRemoved;
                existing.Comments = model.Comments;
            }

            existing.ClearedDate = DateTime.Now;

            var workflow = _context.ClearanceWorkflows
                .FirstOrDefault(x =>
                    x.ExitClearanceId == clearance.Id &&
                    x.Stage == ClearanceStage.SystemsAdmin &&
                    !x.Completed);

            if (workflow != null)
            {
                workflow.Completed = true;
                workflow.CompletedDate = DateTime.Now;
            }

            var developer = _context.Users
                .FirstOrDefault(x =>
                    x.Role == UserRole.Employee);

            clearance.CurrentStage = ClearanceStage.Development;

            _context.ClearanceWorkflows.Add(
                new ClearanceWorkflow
                {
                    ExitClearanceId = clearance.Id,
                    Stage = ClearanceStage.Development,
                    AssignedToUserId = developer.Id,
                    AssignedDate = DateTime.Now
                });

            _context.SaveChanges();

            return RedirectToAction(nameof(SystemsAdminQueue));
        }

        [HttpGet]
        public IActionResult DevelopmentQueue()
        {
            var clearances = _context.ExitClearances
                .Include(x => x.Employee)
                .Where(x => x.CurrentStage == ClearanceStage.Development)
                .ToList();

            return View(clearances);
        }

        [HttpGet]
        public IActionResult DevelopmentReview(int id)
        {
            var clearance = _context.ExitClearances
                .FirstOrDefault(x => x.Id == id);

            if (clearance == null)
                return NotFound();

            var model = _context.DevelopmentClearances
                .FirstOrDefault(x => x.ExitClearanceId == id);

            if (model == null)
            {
                model = new DevelopmentClearance
                {
                    ExitClearanceId = id
                };
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DevelopmentReview(DevelopmentClearance model)
        {
            var clearance = _context.ExitClearances
                .Include(x => x.Employee)
                    .ThenInclude(x => x.Supervisor)
                .Include(x => x.Employee)
                    .ThenInclude(x => x.Department)
                        .ThenInclude(x => x.Hod)
                .FirstOrDefault(x => x.Id == model.ExitClearanceId);

            if (clearance == null)
                return NotFound();

            var existing = _context.DevelopmentClearances
                .FirstOrDefault(x =>
                    x.ExitClearanceId == model.ExitClearanceId);

            if (existing == null)
            {
                existing = model;

                _context.DevelopmentClearances.Add(existing);
            }
            else
            {
                existing.BitbucketRemoved = model.BitbucketRemoved;
                existing.ProjectManagementRemoved = model.ProjectManagementRemoved;
                existing.ManageEngineRemoved = model.ManageEngineRemoved;
                existing.Comments = model.Comments;
            }

            existing.ClearedDate = DateTime.Now;

            // Complete current Development workflow
            var workflow = _context.ClearanceWorkflows
                .FirstOrDefault(x =>
                    x.ExitClearanceId == clearance.Id &&
                    x.Stage == ClearanceStage.Development &&
                    !x.Completed);

            if (workflow != null)
            {
                workflow.Completed = true;
                workflow.CompletedDate = DateTime.Now;
            }

            // Check if employee has a supervisor
            var supervisor = clearance.Employee.Supervisor;

            if (supervisor != null)
            {
                // Route to Supervisor

                clearance.CurrentStage = ClearanceStage.Supervisor;

                _context.ClearanceWorkflows.Add(
                    new ClearanceWorkflow
                    {
                        ExitClearanceId = clearance.Id,
                        Stage = ClearanceStage.Supervisor,
                        AssignedToUserId = supervisor.Id,
                        AssignedDate = DateTime.Now
                    });
            }
            else
            {
                // No Supervisor - route directly to HOD

                var hod = clearance.Employee.Department?.Hod;

                if (hod == null)
                {
                    ModelState.AddModelError("",
                        "No Supervisor or HOD configured for this employee.");

                    return View(model);
                }

                clearance.CurrentStage = ClearanceStage.HOD;

                _context.ClearanceWorkflows.Add(
                    new ClearanceWorkflow
                    {
                        ExitClearanceId = clearance.Id,
                        Stage = ClearanceStage.HOD,
                        AssignedToUserId = hod.Id,
                        AssignedDate = DateTime.Now
                    });
            }

            _context.SaveChanges();

            return RedirectToAction(nameof(DevelopmentQueue));
        }

        [HttpGet]
        public IActionResult SupervisorQueue()
        {
            var userId = GetCurrentUserId(); // Replace with your method

            var clearances = _context.ExitClearances
                .Include(x => x.Employee)
                .Where(x => x.CurrentStage == ClearanceStage.Supervisor)
                .Join(
                    _context.ClearanceWorkflows.Where(w =>
                        !w.Completed &&
                        w.AssignedToUserId == userId),
                    c => c.Id,
                    w => w.ExitClearanceId,
                    (c, w) => c
                )
                .ToList();

            return View(clearances);
        }

        private int GetCurrentUserId()
        {
            return HttpContext.Session.GetInt32("UserId") ?? 0;
        }

        [HttpGet]
        public IActionResult SupervisorReview(int id)
        {
            var clearance = _context.ExitClearances
                .Include(x => x.Employee)
                .FirstOrDefault(x => x.Id == id);

            if (clearance == null)
                return NotFound();

            var model = _context.SupervisorApprovals
                .FirstOrDefault(x => x.ExitClearanceId == id);

            if (model == null)
            {
                model = new SupervisorApproval
                {
                    ExitClearanceId = id
                };
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SupervisorReview(SupervisorApproval model)
        {
            var clearance = _context.ExitClearances
                .Include(x => x.Employee)
                    .ThenInclude(x => x.Department)
                        .ThenInclude(x => x.Hod)
                .FirstOrDefault(x => x.Id == model.ExitClearanceId);

            if (clearance == null)
                return NotFound();

            var existing = _context.SupervisorApprovals
                .FirstOrDefault(x =>
                    x.ExitClearanceId == model.ExitClearanceId);

            if (existing == null)
            {
                existing = model;
                _context.SupervisorApprovals.Add(existing);
            }
            else
            {
                existing.Approved = model.Approved;
                existing.Comments = model.Comments;
            }

            existing.ApprovedDate = DateTime.Now;

            var workflow = _context.ClearanceWorkflows
                .FirstOrDefault(x =>
                    x.ExitClearanceId == clearance.Id &&
                    x.Stage == ClearanceStage.Supervisor &&
                    !x.Completed);

            if (workflow != null)
            {
                workflow.Completed = true;
                workflow.CompletedDate = DateTime.Now;
            }

            var hod = clearance.Employee.Department?.Hod;

            if (hod == null)
            {
                ModelState.AddModelError("",
                    "No HOD configured for employee department.");

                return View(model);
            }

            clearance.CurrentStage = ClearanceStage.HOD;

            _context.ClearanceWorkflows.Add(
                new ClearanceWorkflow
                {
                    ExitClearanceId = clearance.Id,
                    Stage = ClearanceStage.HOD,
                    AssignedToUserId = hod.Id,
                    AssignedDate = DateTime.Now
                });

            _context.SaveChanges();

            return RedirectToAction(nameof(SupervisorQueue));
        }



    }
}
