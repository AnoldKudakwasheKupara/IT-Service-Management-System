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
        public IActionResult Index()
        {
            var clearances = _context.ExitClearances
                .Include(x => x.Employee)
                .OrderByDescending(x => x.CreatedDate)
                .ToList();


            return View(clearances);
        }

        public IActionResult Open(int id)
        {
            var clearance = _context.ExitClearances
                .FirstOrDefault(x => x.Id == id);

            if (clearance == null)
                return NotFound();

            switch (clearance.CurrentStage)
            {
                case ClearanceStage.Finance:
                    return RedirectToAction(
                        nameof(FinanceReview),
                        new { id });

                case ClearanceStage.SystemsAdmin:
                    return RedirectToAction(
                        nameof(SystemsAdminReview),
                        new { id });

                case ClearanceStage.Development:
                    return RedirectToAction(
                        nameof(DevelopmentReview),
                        new { id });

                case ClearanceStage.Supervisor:
                    return RedirectToAction(
                        nameof(SupervisorReview),
                        new { id });

                case ClearanceStage.HOD:
                    return RedirectToAction(
                        nameof(HodReview),
                        new { id });

                case ClearanceStage.HR:
                    return RedirectToAction(
                        nameof(HrReview),
                        new { id });

                default:
                    return RedirectToAction(
                        nameof(Details),
                        new { id });
            }
        }


        [HttpGet]
        public IActionResult Send(int id)
        {
            var clearance = _context.ExitClearances
                .Include(x => x.Employee)
                .FirstOrDefault(x => x.Id == id);

            if (clearance == null)
                return NotFound();

            var link =
                $"{Request.Scheme}://{Request.Host}" +
                Url.Action("Complete",
                    "ExitClearance",
                    new { id = clearance.AccessToken });

            // Email logic here

            TempData["Success"] =
                $"Clearance sent to {clearance.Employee.Email}";

            return RedirectToAction(nameof(Index));
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

            _context.ClearanceWorkflows.Add(
                new ClearanceWorkflow
                {
                    ExitClearanceId = clearance.Id,
                    Stage = ClearanceStage.Employee,
                    AssignedToUserId = employee.Id,
                    AssignedDate = DateTime.Now
                });

            _context.SaveChanges();

            return RedirectToAction(
                "Complete",
                new { id = clearance.AccessToken });
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
                finance = new FinanceClearance
                {
                    ExitClearanceId = model.ExitClearanceId,

                    StaffAdvanceReference = model.StaffAdvanceReference,
                    StaffAdvanceReceivedBy = model.StaffAdvanceReceivedBy,
                    StaffAdvanceDate = model.StaffAdvanceDate,

                    ReceiptsReference = model.ReceiptsReference,
                    ReceiptsReceivedBy = model.ReceiptsReceivedBy,
                    ReceiptsDate = model.ReceiptsDate,

                    FinalDuesReference = model.FinalDuesReference,
                    FinalDuesReceivedBy = model.FinalDuesReceivedBy,
                    FinalDuesDate = model.FinalDuesDate,

                    Comments = model.Comments,
                    ClearedDate = DateTime.Now
                };

                _context.FinanceClearances.Add(finance);
            }
            else
            {
                finance.StaffAdvanceReference = model.StaffAdvanceReference;
                finance.StaffAdvanceReceivedBy = model.StaffAdvanceReceivedBy;
                finance.StaffAdvanceDate = model.StaffAdvanceDate;

                finance.ReceiptsReference = model.ReceiptsReference;
                finance.ReceiptsReceivedBy = model.ReceiptsReceivedBy;
                finance.ReceiptsDate = model.ReceiptsDate;

                finance.FinalDuesReference = model.FinalDuesReference;
                finance.FinalDuesReceivedBy = model.FinalDuesReceivedBy;
                finance.FinalDuesDate = model.FinalDuesDate;

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
                existing = new SystemsAdminClearance
                {
                    ExitClearanceId = model.ExitClearanceId,

                    LaptopAsset = model.LaptopAsset,
                    LaptopReceivedBy = model.LaptopReceivedBy,
                    LaptopDate = model.LaptopDate,

                    LaptopBagAsset = model.LaptopBagAsset,
                    LaptopBagReceivedBy = model.LaptopBagReceivedBy,
                    LaptopBagDate = model.LaptopBagDate,

                    AccessoriesAsset = model.AccessoriesAsset,
                    AccessoriesReceivedBy = model.AccessoriesReceivedBy,
                    AccessoriesDate = model.AccessoriesDate,

                    LockerAsset = model.LockerAsset,
                    LockerReceivedBy = model.LockerReceivedBy,
                    LockerDate = model.LockerDate,

                    EndpointSecurityAsset = model.EndpointSecurityAsset,
                    EndpointSecurityReceivedBy = model.EndpointSecurityReceivedBy,
                    EndpointSecurityDate = model.EndpointSecurityDate,

                    SophosAsset = model.SophosAsset,
                    SophosReceivedBy = model.SophosReceivedBy,
                    SophosDate = model.SophosDate,

                    AccessRemovalAsset = model.AccessRemovalAsset,
                    AccessRemovalReceivedBy = model.AccessRemovalReceivedBy,
                    AccessRemovalDate = model.AccessRemovalDate,

                    EmailAsset = model.EmailAsset,
                    EmailReceivedBy = model.EmailReceivedBy,
                    EmailDate = model.EmailDate,

                    SocialMediaAsset = model.SocialMediaAsset,
                    SocialMediaReceivedBy = model.SocialMediaReceivedBy,
                    SocialMediaDate = model.SocialMediaDate,

                    Comments = model.Comments,
                    ClearedDate = DateTime.Now
                };

                _context.SystemsAdminClearances.Add(existing);
            }
            else
            {
                existing.LaptopAsset = model.LaptopAsset;
                existing.LaptopReceivedBy = model.LaptopReceivedBy;
                existing.LaptopDate = model.LaptopDate;

                existing.LaptopBagAsset = model.LaptopBagAsset;
                existing.LaptopBagReceivedBy = model.LaptopBagReceivedBy;
                existing.LaptopBagDate = model.LaptopBagDate;

                existing.AccessoriesAsset = model.AccessoriesAsset;
                existing.AccessoriesReceivedBy = model.AccessoriesReceivedBy;
                existing.AccessoriesDate = model.AccessoriesDate;

                existing.LockerAsset = model.LockerAsset;
                existing.LockerReceivedBy = model.LockerReceivedBy;
                existing.LockerDate = model.LockerDate;

                existing.EndpointSecurityAsset = model.EndpointSecurityAsset;
                existing.EndpointSecurityReceivedBy = model.EndpointSecurityReceivedBy;
                existing.EndpointSecurityDate = model.EndpointSecurityDate;

                existing.SophosAsset = model.SophosAsset;
                existing.SophosReceivedBy = model.SophosReceivedBy;
                existing.SophosDate = model.SophosDate;

                existing.AccessRemovalAsset = model.AccessRemovalAsset;
                existing.AccessRemovalReceivedBy = model.AccessRemovalReceivedBy;
                existing.AccessRemovalDate = model.AccessRemovalDate;

                existing.EmailAsset = model.EmailAsset;
                existing.EmailReceivedBy = model.EmailReceivedBy;
                existing.EmailDate = model.EmailDate;

                existing.SocialMediaAsset = model.SocialMediaAsset;
                existing.SocialMediaReceivedBy = model.SocialMediaReceivedBy;
                existing.SocialMediaDate = model.SocialMediaDate;

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
                    x.Role == UserRole.Employee); // Change this later

            if (developer == null)
            {
                ModelState.AddModelError("",
                    "No Development user configured.");

                return View(model);
            }

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
                existing = new DevelopmentClearance
                {
                    ExitClearanceId = model.ExitClearanceId,

                    BitbucketAsset = model.BitbucketAsset,
                    BitbucketReceivedBy = model.BitbucketReceivedBy,
                    BitbucketDate = model.BitbucketDate,

                    ProjectManagementAsset = model.ProjectManagementAsset,
                    ProjectManagementReceivedBy = model.ProjectManagementReceivedBy,
                    ProjectManagementDate = model.ProjectManagementDate,

                    ManageEngineAsset = model.ManageEngineAsset,
                    ManageEngineReceivedBy = model.ManageEngineReceivedBy,
                    ManageEngineDate = model.ManageEngineDate,

                    Comments = model.Comments,
                    ClearedDate = DateTime.Now
                };

                _context.DevelopmentClearances.Add(existing);
            }
            else
            {
                existing.BitbucketAsset = model.BitbucketAsset;
                existing.BitbucketReceivedBy = model.BitbucketReceivedBy;
                existing.BitbucketDate = model.BitbucketDate;

                existing.ProjectManagementAsset = model.ProjectManagementAsset;
                existing.ProjectManagementReceivedBy = model.ProjectManagementReceivedBy;
                existing.ProjectManagementDate = model.ProjectManagementDate;

                existing.ManageEngineAsset = model.ManageEngineAsset;
                existing.ManageEngineReceivedBy = model.ManageEngineReceivedBy;
                existing.ManageEngineDate = model.ManageEngineDate;

                existing.Comments = model.Comments;
            }

            existing.ClearedDate = DateTime.Now;

            // Complete Development workflow
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

            // Route to Supervisor if configured
            var supervisor = clearance.Employee.Supervisor;

            if (supervisor != null)
            {
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
                // No supervisor, send directly to HOD

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
        [HttpGet]
        public IActionResult HodQueue()
        {
            var userId = GetCurrentUserId(); // replace with your implementation

            var clearances = _context.ExitClearances
                .Include(x => x.Employee)
                .Where(x => x.CurrentStage == ClearanceStage.HOD)
                .Join(
                    _context.ClearanceWorkflows.Where(x =>
                        !x.Completed &&
                        x.AssignedToUserId == userId),
                    c => c.Id,
                    w => w.ExitClearanceId,
                    (c, w) => c
                )
                .ToList();

            return View(clearances);
        }

        [HttpGet]
        public IActionResult HodReview(int id)
        {
            var clearance = _context.ExitClearances
                .Include(x => x.Employee)
                .FirstOrDefault(x => x.Id == id);

            if (clearance == null)
                return NotFound();

            var model = _context.HodApprovals
                .FirstOrDefault(x => x.ExitClearanceId == id);

            if (model == null)
            {
                model = new HodApproval
                {
                    ExitClearanceId = id,
                    HandoverDate = DateTime.Today
                };
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult HodReview(HodApproval model)
        {
            var clearance = _context.ExitClearances
                .FirstOrDefault(x => x.Id == model.ExitClearanceId);

            if (clearance == null)
                return NotFound();

            var existing = _context.HodApprovals
                .FirstOrDefault(x =>
                    x.ExitClearanceId == model.ExitClearanceId);

            if (existing == null)
            {
                existing = new HodApproval
                {
                    ExitClearanceId = model.ExitClearanceId,

                    ConfirmedBy = model.ConfirmedBy,
                    HandoverSignature = model.HandoverSignature,
                    HandoverDate = model.HandoverDate,
                    Comments = model.Comments,

                    ApprovedDate = DateTime.Now
                };

                _context.HodApprovals.Add(existing);
            }
            else
            {
                existing.ConfirmedBy = model.ConfirmedBy;
                existing.HandoverSignature = model.HandoverSignature;
                existing.HandoverDate = model.HandoverDate;
                existing.Comments = model.Comments;

                existing.ApprovedDate = DateTime.Now;
            }

            var workflow = _context.ClearanceWorkflows
                .FirstOrDefault(x =>
                    x.ExitClearanceId == clearance.Id &&
                    x.Stage == ClearanceStage.HOD &&
                    !x.Completed);

            if (workflow != null)
            {
                workflow.Completed = true;
                workflow.CompletedDate = DateTime.Now;
            }

            var hrUser = _context.Users
                .FirstOrDefault(x =>
                    x.Role == UserRole.HR);

            if (hrUser == null)
            {
                ModelState.AddModelError("",
                    "No HR user configured.");

                return View(model);
            }

            clearance.CurrentStage = ClearanceStage.HR;

            _context.ClearanceWorkflows.Add(
                new ClearanceWorkflow
                {
                    ExitClearanceId = clearance.Id,
                    Stage = ClearanceStage.HR,
                    AssignedToUserId = hrUser.Id,
                    AssignedDate = DateTime.Now
                });

            _context.SaveChanges();

            return RedirectToAction(nameof(HodQueue));
        }

        [HttpGet]
        public IActionResult HrQueue()
        {
            var userId = GetCurrentUserId(); 

            var clearances = _context.ExitClearances
                .Include(x => x.Employee)
                .Where(x => x.CurrentStage == ClearanceStage.HR)
                .Join(
                    _context.ClearanceWorkflows.Where(x =>
                        !x.Completed &&
                        x.AssignedToUserId == userId),
                    c => c.Id,
                    w => w.ExitClearanceId,
                    (c, w) => c
                )
                .ToList();

            return View(clearances);
        }

        [HttpGet]
        public IActionResult HrReview(int id)
        {
            var clearance = _context.ExitClearances
                .Include(x => x.Employee)
                .FirstOrDefault(x => x.Id == id);

            if (clearance == null)
                return NotFound();

            var model = _context.HrApprovals
                .FirstOrDefault(x => x.ExitClearanceId == id);

            if (model == null)
            {
                model = new HrApproval
                {
                    ExitClearanceId = id
                };
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult HrReview(HrApproval model)
        {
            var clearance = _context.ExitClearances
                .FirstOrDefault(x => x.Id == model.ExitClearanceId);

            if (clearance == null)
                return NotFound();

            var existing = _context.HrApprovals
                .FirstOrDefault(x =>
                    x.ExitClearanceId == model.ExitClearanceId);

            if (existing == null)
            {
                existing = new HrApproval
                {
                    ExitClearanceId = model.ExitClearanceId,

                    ResignationLetterReceived = model.ResignationLetterReceived,
                    ResignationLetterReceivedBy = model.ResignationLetterReceivedBy,
                    ResignationLetterDate = model.ResignationLetterDate,

                    StaffIdReturned = model.StaffIdReturned,
                    StaffIdReceivedBy = model.StaffIdReceivedBy,
                    StaffIdDate = model.StaffIdDate,

                    MedicalAidCancelled = model.MedicalAidCancelled,
                    MedicalAidReceivedBy = model.MedicalAidReceivedBy,
                    MedicalAidDate = model.MedicalAidDate,

                    NSSACancelled = model.NSSACancelled,
                    NSSAReceivedBy = model.NSSAReceivedBy,
                    NSSADate = model.NSSADate,

                    FuneralPolicyCancelled = model.FuneralPolicyCancelled,
                    FuneralPolicyReceivedBy = model.FuneralPolicyReceivedBy,
                    FuneralPolicyDate = model.FuneralPolicyDate,

                    ExitInterviewCompleted = model.ExitInterviewCompleted,
                    ExitInterviewReceivedBy = model.ExitInterviewReceivedBy,
                    ExitInterviewDate = model.ExitInterviewDate,

                    HandoverReportSubmitted = model.HandoverReportSubmitted,
                    HandoverReportReceivedBy = model.HandoverReportReceivedBy,
                    HandoverReportDate = model.HandoverReportDate,

                    Comments = model.Comments,

                    ApprovedDate = DateTime.Now
                };

                _context.HrApprovals.Add(existing);
            }
            else
            {
                existing.ResignationLetterReceived = model.ResignationLetterReceived;
                existing.ResignationLetterReceivedBy = model.ResignationLetterReceivedBy;
                existing.ResignationLetterDate = model.ResignationLetterDate;

                existing.StaffIdReturned = model.StaffIdReturned;
                existing.StaffIdReceivedBy = model.StaffIdReceivedBy;
                existing.StaffIdDate = model.StaffIdDate;

                existing.MedicalAidCancelled = model.MedicalAidCancelled;
                existing.MedicalAidReceivedBy = model.MedicalAidReceivedBy;
                existing.MedicalAidDate = model.MedicalAidDate;

                existing.NSSACancelled = model.NSSACancelled;
                existing.NSSAReceivedBy = model.NSSAReceivedBy;
                existing.NSSADate = model.NSSADate;

                existing.FuneralPolicyCancelled = model.FuneralPolicyCancelled;
                existing.FuneralPolicyReceivedBy = model.FuneralPolicyReceivedBy;
                existing.FuneralPolicyDate = model.FuneralPolicyDate;

                existing.ExitInterviewCompleted = model.ExitInterviewCompleted;
                existing.ExitInterviewReceivedBy = model.ExitInterviewReceivedBy;
                existing.ExitInterviewDate = model.ExitInterviewDate;

                existing.HandoverReportSubmitted = model.HandoverReportSubmitted;
                existing.HandoverReportReceivedBy = model.HandoverReportReceivedBy;
                existing.HandoverReportDate = model.HandoverReportDate;

                existing.Comments = model.Comments;

                existing.ApprovedDate = DateTime.Now;
            }

            var workflow = _context.ClearanceWorkflows
                .FirstOrDefault(x =>
                    x.ExitClearanceId == clearance.Id &&
                    x.Stage == ClearanceStage.HR &&
                    !x.Completed);

            if (workflow != null)
            {
                workflow.Completed = true;
                workflow.CompletedDate = DateTime.Now;
            }

            clearance.CurrentStage = ClearanceStage.Completed;
            clearance.Status = ClearanceStatus.Completed;

            _context.SaveChanges();

            return RedirectToAction(nameof(HrQueue));
        }


        [HttpGet]
        public IActionResult Details(int id)
        {
            var clearance = _context.ExitClearances
                .Include(x => x.Employee)
                .FirstOrDefault(x => x.Id == id);

            if (clearance == null)
                return NotFound();

            var vm = new ExitClearanceDetailsVM
            {
                ExitClearance = clearance,

                EmployeeDetails = _context.ExitClearanceEmployeeDetails
                    .FirstOrDefault(x => x.ExitClearanceId == id),

                FinanceClearance = _context.FinanceClearances
                    .FirstOrDefault(x => x.ExitClearanceId == id),

                SystemsAdminClearance = _context.SystemsAdminClearances
                    .FirstOrDefault(x => x.ExitClearanceId == id),

                DevelopmentClearance = _context.DevelopmentClearances
                    .FirstOrDefault(x => x.ExitClearanceId == id),

                SupervisorApproval = _context.SupervisorApprovals
                    .FirstOrDefault(x => x.ExitClearanceId == id),

                HodApproval = _context.HodApprovals
                    .FirstOrDefault(x => x.ExitClearanceId == id),

                HrApproval = _context.HrApprovals
                    .FirstOrDefault(x => x.ExitClearanceId == id),

                WorkflowHistory = _context.ClearanceWorkflows
                    .Where(x => x.ExitClearanceId == id)
                    .OrderBy(x => x.AssignedDate)
                    .ToList()
            };

            return View(vm);
        }

        [HttpGet]
        public IActionResult Edit(int id)
        {
            var clearance = _context.ExitClearances
                .FirstOrDefault(x => x.Id == id);

            if (clearance == null)
                return NotFound();

            var vm = new CreateExitClearanceVM
            {
                EmployeeId = clearance.EmployeeId,

                Employees = _context.Users
                    .Where(x => x.IsActive)
                    .Select(x => new SelectListItem
                    {
                        Value = x.Id.ToString(),
                        Text = x.FirstName + " " + x.LastName
                    })
                    .ToList()
            };

            ViewBag.ClearanceId = clearance.Id;

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(int id, CreateExitClearanceVM vm)
        {
            var clearance = _context.ExitClearances
                .FirstOrDefault(x => x.Id == id);

            if (clearance == null)
                return NotFound();

            clearance.EmployeeId = vm.EmployeeId;

            _context.SaveChanges();

            TempData["Success"] =
                "Exit Clearance updated successfully.";

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public IActionResult Delete(int id)
        {
            var clearance = _context.ExitClearances
                .Include(x => x.Employee)
                .FirstOrDefault(x => x.Id == id);

            if (clearance == null)
                return NotFound();

            return View(clearance);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteConfirmed(int id)
        {
            var clearance = _context.ExitClearances
                .FirstOrDefault(x => x.Id == id);

            if (clearance == null)
                return NotFound();

            _context.ExitClearanceEmployeeDetails
                .RemoveRange(
                    _context.ExitClearanceEmployeeDetails
                        .Where(x => x.ExitClearanceId == id));

            _context.FinanceClearances
                .RemoveRange(
                    _context.FinanceClearances
                        .Where(x => x.ExitClearanceId == id));

            _context.SystemsAdminClearances
                .RemoveRange(
                    _context.SystemsAdminClearances
                        .Where(x => x.ExitClearanceId == id));

            _context.DevelopmentClearances
                .RemoveRange(
                    _context.DevelopmentClearances
                        .Where(x => x.ExitClearanceId == id));

            _context.SupervisorApprovals
                .RemoveRange(
                    _context.SupervisorApprovals
                        .Where(x => x.ExitClearanceId == id));

            _context.HodApprovals
                .RemoveRange(
                    _context.HodApprovals
                        .Where(x => x.ExitClearanceId == id));

            _context.HrApprovals
                .RemoveRange(
                    _context.HrApprovals
                        .Where(x => x.ExitClearanceId == id));

            _context.ClearanceWorkflows
                .RemoveRange(
                    _context.ClearanceWorkflows
                        .Where(x => x.ExitClearanceId == id));

            _context.ExitClearances.Remove(clearance);

            _context.SaveChanges();

            TempData["Success"] =
                "Exit Clearance deleted successfully.";

            return RedirectToAction(nameof(Index));
        }



    }
}
