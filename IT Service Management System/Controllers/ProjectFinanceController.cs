using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Filters;
using IT_Service_Management_System.Helpers;
using IT_Service_Management_System.Helpers.Pm;
using IT_Service_Management_System.Models.Pm;
using IT_Service_Management_System.Services.Pm;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IT_Service_Management_System.Controllers
{
    /// <summary>
    /// Project money: the budget breakdown with forecast and variance, expense claims and their
    /// approval workflow, the procurement chain from request through to payment, and the assets
    /// issued to a project.
    /// </summary>
    [RoleAuthorize("Admin", "SystemsAdmin", "ProjectManager", "GeneralManager", "TeamLead",
                   "DepartmentManager", "Finance", "Procurement", "Auditor", "Employee")]
    public class ProjectFinanceController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly ProjectMetricsService _metrics;
        private readonly ProjectActivityService _activity;
        private readonly ProjectApprovalService _approvals;
        private readonly ProjectIntelligenceService _intelligence;
        private readonly PmFileService _files;

        public ProjectFinanceController(ApplicationDbContext db, ProjectMetricsService metrics,
            ProjectActivityService activity, ProjectApprovalService approvals,
            ProjectIntelligenceService intelligence, PmFileService files)
        {
            _db = db; _metrics = metrics; _activity = activity;
            _approvals = approvals; _intelligence = intelligence; _files = files;
        }

        private int Uid => HttpContext.Session.GetInt32("UserId") ?? 0;
        private string? Role => HttpContext.Session.GetString("UserRole");
        private IActionResult AccessDenied() => RedirectToAction("AccessDenied", "Home");

        // ════════════════════════════════════════════════════════════════════════
        //  Budget
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>Budget vs actual by cost category, with the forecast outturn.</summary>
        public async Task<IActionResult> Budget(int projectId)
        {
            var ctx = await LoadContextAsync(projectId);
            if (ctx == null) return NotFound();
            if (!ctx.Value.CanView) return AccessDenied();

            var project = ctx.Value.Project;
            var lines = await _db.BudgetLines.AsNoTracking()
                .Include(l => l.Phase)
                .Where(l => l.ProjectId == projectId)
                .OrderBy(l => l.Category).ThenBy(l => l.Name).ToListAsync();

            // Approved expenses attributed to each line, so the page reconciles with the claims.
            var expensesByLine = await _db.ProjectExpenses.AsNoTracking()
                .Where(e => e.ProjectId == projectId && e.BudgetLineId != null
                            && (e.Status == ExpenseStatus.Approved || e.Status == ExpenseStatus.Reimbursed))
                .GroupBy(e => e.BudgetLineId!.Value)
                .Select(g => new { g.Key, Total = g.Sum(x => x.Amount) })
                .ToDictionaryAsync(x => x.Key, x => x.Total);

            var labourCost = await _db.TimeEntries
                .Where(t => t.ProjectId == projectId && t.Status == TimeEntryStatus.Approved)
                .SumAsync(t => (decimal?)((t.Hours - t.BreakHours) * t.CostRate)) ?? 0m;

            ViewBag.Lines = lines;
            ViewBag.ExpensesByLine = expensesByLine;
            ViewBag.LabourCost = labourCost;
            ViewBag.TotalPlanned = lines.Sum(l => l.PlannedAmount);
            ViewBag.TotalActual = await _metrics.ActualSpendAsync(projectId);
            ViewBag.TotalCommitted = await _metrics.CommittedSpendAsync(projectId);
            ViewBag.TotalForecast = lines.Sum(l => l.ForecastAmount > 0 ? l.ForecastAmount : l.PlannedAmount);
            ViewBag.Unallocated = project.TotalBudget - lines.Sum(l => l.PlannedAmount);
            ViewBag.Forecast = await _intelligence.ForecastBudgetAsync(projectId);
            ViewBag.CanManageMoney = ctx.Value.CanEdit || PmAccess.CanApproveSpend(Role);

            ViewBag.Phases = await _db.ProjectPhases.AsNoTracking()
                .Where(p => p.ProjectId == projectId).OrderBy(p => p.Sequence)
                .Select(p => new { p.Id, p.Name }).ToListAsync();

            return View(project);
        }

        [HttpPost]
        public async Task<IActionResult> SaveBudgetLine(BudgetLine input)
        {
            var ctx = await LoadContextAsync(input.ProjectId);
            if (ctx == null) return NotFound();
            if (!ctx.Value.CanEdit && !PmAccess.CanApproveSpend(Role)) return AccessDenied();

            if (string.IsNullOrWhiteSpace(input.Name))
            {
                TempData["Error"] = "A budget line needs a name.";
                return RedirectToAction(nameof(Budget), new { projectId = input.ProjectId });
            }

            if (input.Id == 0)
            {
                input.ForecastAmount = input.ForecastAmount > 0 ? input.ForecastAmount : input.PlannedAmount;
                _db.BudgetLines.Add(input);
                _activity.Log(input.ProjectId, nameof(BudgetLine), null, "Created", $"{input.Name} · {input.PlannedAmount:N2}");
            }
            else
            {
                var line = await _db.BudgetLines.FirstOrDefaultAsync(l => l.Id == input.Id && l.ProjectId == input.ProjectId);
                if (line == null) return NotFound();

                _activity.LogChange(input.ProjectId, nameof(BudgetLine), line.Id, "Planned amount", line.PlannedAmount, input.PlannedAmount);

                line.Name = input.Name;
                line.Category = input.Category;
                line.PhaseId = input.PhaseId;
                line.PlannedAmount = input.PlannedAmount;
                line.ActualAmount = input.ActualAmount;
                line.ForecastAmount = input.ForecastAmount;
                line.CommittedAmount = input.CommittedAmount;
                line.Notes = input.Notes;
            }

            await _db.SaveChangesAsync();
            await _metrics.RefreshProjectAsync(input.ProjectId);
            await WarnIfOverBudgetAsync(input.ProjectId);

            TempData["Success"] = "Budget line saved.";
            return RedirectToAction(nameof(Budget), new { projectId = input.ProjectId });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteBudgetLine(int projectId, int lineId)
        {
            var ctx = await LoadContextAsync(projectId);
            if (ctx == null) return NotFound();
            if (!ctx.Value.CanEdit && !PmAccess.CanApproveSpend(Role)) return AccessDenied();

            var line = await _db.BudgetLines.FirstOrDefaultAsync(l => l.Id == lineId && l.ProjectId == projectId);
            if (line != null)
            {
                // Expenses survive; they simply become unattributed spend on the project.
                foreach (var expense in await _db.ProjectExpenses.Where(e => e.BudgetLineId == lineId).ToListAsync())
                    expense.BudgetLineId = null;

                _db.BudgetLines.Remove(line);
                _activity.Log(projectId, nameof(BudgetLine), lineId, "Deleted", line.Name);
                await _db.SaveChangesAsync();
                await _metrics.RefreshProjectAsync(projectId);
            }

            TempData["Success"] = "Budget line removed.";
            return RedirectToAction(nameof(Budget), new { projectId });
        }

        /// <summary>Raise a warning notification once spend crosses the approved budget.</summary>
        private async Task WarnIfOverBudgetAsync(int projectId)
        {
            var project = await _db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null || project.TotalBudget <= 0) return;

            var spent = await _metrics.ActualSpendAsync(projectId);
            if (spent <= project.TotalBudget) return;

            _activity.NotifyMany(await _activity.ProjectAudienceAsync(projectId), PmNotificationType.BudgetExceeded,
                $"{project.Reference} is over budget",
                $"Spend of {spent:N2} exceeds the approved budget of {project.TotalBudget:N2}.",
                Url.Action(nameof(Budget), new { projectId }), projectId);
            await _db.SaveChangesAsync();
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Expenses
        // ════════════════════════════════════════════════════════════════════════

        public async Task<IActionResult> Expenses(int projectId, ExpenseStatus? status, CostCategory? category)
        {
            var ctx = await LoadContextAsync(projectId);
            if (ctx == null) return NotFound();
            if (!ctx.Value.CanView) return AccessDenied();

            IQueryable<ProjectExpense> query = _db.ProjectExpenses.AsNoTracking()
                .Include(e => e.SubmittedBy).Include(e => e.ApprovedBy).Include(e => e.BudgetLine)
                .Where(e => e.ProjectId == projectId);

            if (status.HasValue) query = query.Where(e => e.Status == status.Value);
            if (category.HasValue) query = query.Where(e => e.Category == category.Value);

            var expenses = await query.OrderByDescending(e => e.ExpenseDate).ToListAsync();

            ViewBag.Status = status; ViewBag.Category = category;
            ViewBag.TotalApproved = expenses.Where(e => e.Status is ExpenseStatus.Approved or ExpenseStatus.Reimbursed).Sum(e => e.Amount);
            ViewBag.TotalPending = expenses.Where(e => e.Status == ExpenseStatus.Submitted).Sum(e => e.Amount);
            ViewBag.PendingCount = expenses.Count(e => e.Status == ExpenseStatus.Submitted);
            ViewBag.CanApproveSpend = PmAccess.CanApproveSpend(Role) || ctx.Value.CanEdit;
            ViewBag.BudgetLines = await _db.BudgetLines.AsNoTracking()
                .Where(l => l.ProjectId == projectId).OrderBy(l => l.Name)
                .Select(l => new { l.Id, l.Name }).ToListAsync();
            ViewBag.Tasks = await _db.ProjectTasks.AsNoTracking()
                .Where(t => t.ProjectId == projectId).OrderBy(t => t.Name)
                .Select(t => new { t.Id, t.Name }).ToListAsync();

            return View(expenses);
        }

        [HttpPost]
        [RequestSizeLimit(30_000_000)]
        public async Task<IActionResult> SaveExpense(ProjectExpense input, IFormFile? receipt)
        {
            var ctx = await LoadContextAsync(input.ProjectId);
            if (ctx == null) return NotFound();
            if (!ctx.Value.CanContribute) return AccessDenied();

            if (string.IsNullOrWhiteSpace(input.Description) || input.Amount <= 0)
            {
                TempData["Error"] = "An expense needs a description and an amount above zero.";
                return RedirectToAction(nameof(Expenses), new { projectId = input.ProjectId });
            }
            if (input.ExpenseDate > DateTime.Today)
            {
                TempData["Error"] = "An expense cannot be dated in the future.";
                return RedirectToAction(nameof(Expenses), new { projectId = input.ProjectId });
            }

            if (input.BudgetLineId == 0) input.BudgetLineId = null;
            if (input.TaskId == 0) input.TaskId = null;

            ProjectExpense expense;
            if (input.Id == 0)
            {
                input.SubmittedById = Uid;
                input.CreatedAt = DateTime.Now;
                input.Status = ExpenseStatus.Draft;
                input.Currency = ctx.Value.Project.Currency;
                _db.ProjectExpenses.Add(input);
                expense = input;
            }
            else
            {
                var existing = await _db.ProjectExpenses.FirstOrDefaultAsync(e => e.Id == input.Id && e.ProjectId == input.ProjectId);
                if (existing == null) return NotFound();

                // Once money has been approved the claim is a financial record, not a draft.
                if (existing.Status is ExpenseStatus.Approved or ExpenseStatus.Reimbursed)
                {
                    TempData["Error"] = "An approved expense can no longer be edited.";
                    return RedirectToAction(nameof(Expenses), new { projectId = input.ProjectId });
                }
                if (existing.SubmittedById != Uid && !Roles.IsFullAccess(Role))
                {
                    TempData["Error"] = "You can only edit expense claims you raised.";
                    return RedirectToAction(nameof(Expenses), new { projectId = input.ProjectId });
                }

                existing.Description = input.Description;
                existing.Category = input.Category;
                existing.Amount = input.Amount;
                existing.ExpenseDate = input.ExpenseDate;
                existing.Supplier = input.Supplier;
                existing.ReceiptReference = input.ReceiptReference;
                existing.BudgetLineId = input.BudgetLineId;
                existing.TaskId = input.TaskId;
                expense = existing;
            }

            await _db.SaveChangesAsync();

            if (receipt != null)
            {
                var saved = await _files.SaveAsync(receipt, "expenses", expense.Id);
                if (saved != null)
                {
                    _files.Delete(expense.ReceiptPath);
                    expense.ReceiptPath = saved.RelativePath;
                    await _db.SaveChangesAsync();
                }
                else TempData["Error"] = _files.LastError;
            }

            _activity.Log(input.ProjectId, nameof(ProjectExpense), expense.Id,
                input.Id == 0 ? "Created" : "Updated", $"{expense.Description} · {expense.Amount:N2}");
            await _db.SaveChangesAsync();

            TempData["Success"] ??= "Expense saved.";
            return RedirectToAction(nameof(Expenses), new { projectId = input.ProjectId });
        }

        [HttpPost]
        public async Task<IActionResult> SubmitExpense(int projectId, int expenseId)
        {
            var ctx = await LoadContextAsync(projectId);
            if (ctx == null) return NotFound();
            if (!ctx.Value.CanContribute) return AccessDenied();

            var expense = await _db.ProjectExpenses.FirstOrDefaultAsync(e => e.Id == expenseId && e.ProjectId == projectId);
            if (expense == null) return NotFound();
            if (expense.Status != ExpenseStatus.Draft)
            {
                TempData["Error"] = "Only a draft expense can be submitted.";
                return RedirectToAction(nameof(Expenses), new { projectId });
            }

            var steps = await _approvals.RequestAsync(ApprovalSubject.Expense, expenseId,
                $"{expense.Reference} — {expense.Description}", projectId, Uid, expense.Amount);
            if (steps == 0)
            {
                TempData["Error"] = "No approver could be found for this claim.";
                return RedirectToAction(nameof(Expenses), new { projectId });
            }

            expense.Status = ExpenseStatus.Submitted;
            await _db.SaveChangesAsync();

            TempData["Success"] = $"Expense submitted — {steps} approval step(s) raised.";
            return RedirectToAction(nameof(Expenses), new { projectId });
        }

        /// <summary>Mark an approved claim as paid back to the employee.</summary>
        [HttpPost]
        public async Task<IActionResult> MarkReimbursed(int projectId, int expenseId)
        {
            if (!PmAccess.CanApproveSpend(Role)) return AccessDenied();

            var expense = await _db.ProjectExpenses.FirstOrDefaultAsync(e => e.Id == expenseId && e.ProjectId == projectId);
            if (expense == null) return NotFound();
            if (expense.Status != ExpenseStatus.Approved)
            {
                TempData["Error"] = "Only an approved expense can be marked reimbursed.";
                return RedirectToAction(nameof(Expenses), new { projectId });
            }

            expense.Status = ExpenseStatus.Reimbursed;
            expense.ReimbursedAt = DateTime.Now;
            _activity.Log(projectId, nameof(ProjectExpense), expenseId, "Reimbursed", $"{expense.Amount:N2}");
            _activity.Notify(expense.SubmittedById, PmNotificationType.ApprovalDecided,
                "Expense reimbursed", $"{expense.Reference} · {expense.Amount:N2}",
                Url.Action(nameof(Expenses), new { projectId }), projectId);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Expense marked reimbursed.";
            return RedirectToAction(nameof(Expenses), new { projectId });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteExpense(int projectId, int expenseId)
        {
            var ctx = await LoadContextAsync(projectId);
            if (ctx == null) return NotFound();

            var expense = await _db.ProjectExpenses.FirstOrDefaultAsync(e => e.Id == expenseId && e.ProjectId == projectId);
            if (expense == null) return NotFound();

            if (expense.SubmittedById != Uid && !ctx.Value.CanEdit && !Roles.IsFullAccess(Role)) return AccessDenied();
            if (expense.Status is ExpenseStatus.Approved or ExpenseStatus.Reimbursed && !Roles.IsFullAccess(Role))
            {
                TempData["Error"] = "An approved expense is a financial record and cannot be deleted.";
                return RedirectToAction(nameof(Expenses), new { projectId });
            }

            _files.Delete(expense.ReceiptPath);
            _db.ProjectExpenses.Remove(expense);
            _activity.Log(projectId, nameof(ProjectExpense), expenseId, "Deleted", expense.Description);
            await _db.SaveChangesAsync();
            await _metrics.RefreshProjectAsync(projectId);

            TempData["Success"] = "Expense removed.";
            return RedirectToAction(nameof(Expenses), new { projectId });
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Procurement
        // ════════════════════════════════════════════════════════════════════════

        public async Task<IActionResult> Procurement(int projectId, ProcurementStatus? status)
        {
            var ctx = await LoadContextAsync(projectId);
            if (ctx == null) return NotFound();
            if (!ctx.Value.CanView) return AccessDenied();

            var requests = await _db.ProcurementRequests.AsNoTracking()
                .Include(p => p.RequestedBy).Include(p => p.ApprovedBy).Include(p => p.BudgetLine)
                .Where(p => p.ProjectId == projectId && (status == null || p.Status == status))
                .OrderByDescending(p => p.CreatedAt).ToListAsync();

            var ids = requests.Select(r => r.Id).ToList();
            ViewBag.QuoteCounts = await _db.ProcurementQuotes.AsNoTracking()
                .Where(q => ids.Contains(q.ProcurementRequestId))
                .GroupBy(q => q.ProcurementRequestId)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count);

            ViewBag.Status = status;
            ViewBag.TotalOrdered = requests.Sum(r => r.OrderedAmount);
            ViewBag.TotalPaid = requests.Sum(r => r.PaidAmount);
            ViewBag.TotalOutstanding = requests.Sum(r => r.OutstandingAmount);
            ViewBag.CanProcure = Roles.PmProcurement.Contains(Role) || ctx.Value.CanEdit;
            ViewBag.BudgetLines = await _db.BudgetLines.AsNoTracking()
                .Where(l => l.ProjectId == projectId).OrderBy(l => l.Name)
                .Select(l => new { l.Id, l.Name }).ToListAsync();
            ViewBag.Suppliers = await _db.Suppliers.AsNoTracking()
                .OrderBy(s => s.Name).Select(s => new { s.Id, s.Name }).ToListAsync();

            return View(requests);
        }

        [HttpPost]
        public async Task<IActionResult> SaveProcurement(ProcurementRequest input)
        {
            var ctx = await LoadContextAsync(input.ProjectId);
            if (ctx == null) return NotFound();
            if (!ctx.Value.CanContribute) return AccessDenied();

            if (string.IsNullOrWhiteSpace(input.Title))
            {
                TempData["Error"] = "A purchase request needs a title.";
                return RedirectToAction(nameof(Procurement), new { projectId = input.ProjectId });
            }
            if (input.BudgetLineId == 0) input.BudgetLineId = null;

            if (input.Id == 0)
            {
                input.RequestedById = Uid;
                input.CreatedAt = DateTime.Now;
                input.Status = ProcurementStatus.Draft;
                input.Currency = ctx.Value.Project.Currency;
                _db.ProcurementRequests.Add(input);
                await _db.SaveChangesAsync();
                _activity.Log(input.ProjectId, nameof(ProcurementRequest), input.Id, "Created", input.Title);
            }
            else
            {
                var request = await _db.ProcurementRequests.FirstOrDefaultAsync(p => p.Id == input.Id && p.ProjectId == input.ProjectId);
                if (request == null) return NotFound();

                request.Title = input.Title;
                request.Description = input.Description;
                request.Category = input.Category;
                request.EstimatedCost = input.EstimatedCost;
                request.BudgetLineId = input.BudgetLineId;
                request.RequiredByDate = input.RequiredByDate;
                _activity.Log(input.ProjectId, nameof(ProcurementRequest), request.Id, "Updated", request.Title);
            }

            await _db.SaveChangesAsync();
            TempData["Success"] = "Purchase request saved.";
            return RedirectToAction(nameof(Procurement), new { projectId = input.ProjectId });
        }

        [HttpPost]
        public async Task<IActionResult> SubmitProcurement(int projectId, int requestId)
        {
            var ctx = await LoadContextAsync(projectId);
            if (ctx == null) return NotFound();
            if (!ctx.Value.CanContribute) return AccessDenied();

            var request = await _db.ProcurementRequests.FirstOrDefaultAsync(p => p.Id == requestId && p.ProjectId == projectId);
            if (request == null) return NotFound();

            var steps = await _approvals.RequestAsync(ApprovalSubject.Purchase, requestId,
                $"{request.Reference} — {request.Title}", projectId, Uid, request.EstimatedCost);
            if (steps == 0)
            {
                TempData["Error"] = "No approver could be found for this purchase request.";
                return RedirectToAction(nameof(Procurement), new { projectId });
            }

            request.Status = ProcurementStatus.Submitted;
            await _db.SaveChangesAsync();

            TempData["Success"] = $"Purchase request submitted — {steps} approval step(s) raised.";
            return RedirectToAction(nameof(Procurement), new { projectId });
        }

        /// <summary>Record a supplier quotation against a request, for side-by-side comparison.</summary>
        [HttpPost]
        public async Task<IActionResult> AddQuote(int projectId, ProcurementQuote input)
        {
            var ctx = await LoadContextAsync(projectId);
            if (ctx == null) return NotFound();
            if (!Roles.PmProcurement.Contains(Role) && !ctx.Value.CanEdit) return AccessDenied();

            if (string.IsNullOrWhiteSpace(input.SupplierName) || input.QuotedAmount <= 0)
            {
                TempData["Error"] = "A quotation needs a supplier and an amount.";
                return RedirectToAction(nameof(ProcurementDetails), new { id = input.ProcurementRequestId });
            }

            input.ReceivedDate ??= DateTime.Today;
            _db.ProcurementQuotes.Add(input);

            var request = await _db.ProcurementRequests.FirstOrDefaultAsync(p => p.Id == input.ProcurementRequestId);
            if (request != null && request.Status == ProcurementStatus.Approved)
                request.Status = ProcurementStatus.RfqIssued;

            await _db.SaveChangesAsync();

            TempData["Success"] = "Quotation recorded.";
            return RedirectToAction(nameof(ProcurementDetails), new { id = input.ProcurementRequestId });
        }

        public async Task<IActionResult> ProcurementDetails(int id)
        {
            var request = await _db.ProcurementRequests.AsNoTracking()
                .Include(p => p.Project).Include(p => p.RequestedBy).Include(p => p.ApprovedBy).Include(p => p.BudgetLine)
                .FirstOrDefaultAsync(p => p.Id == id);
            if (request == null) return NotFound();

            var ctx = await LoadContextAsync(request.ProjectId);
            if (ctx == null || !ctx.Value.CanView) return AccessDenied();

            ViewBag.Quotes = await _db.ProcurementQuotes.AsNoTracking()
                .Where(q => q.ProcurementRequestId == id)
                .OrderByDescending(q => q.Score).ThenBy(q => q.QuotedAmount).ToListAsync();
            ViewBag.CanProcure = Roles.PmProcurement.Contains(Role) || ctx.Value.CanEdit;
            ViewBag.Suppliers = await _db.Suppliers.AsNoTracking()
                .OrderBy(s => s.Name).Select(s => new { s.Id, s.Name }).ToListAsync();

            return View(request);
        }

        /// <summary>Advance a purchase along the chain: select supplier → order → receive → invoice → pay.</summary>
        [HttpPost]
        public async Task<IActionResult> AdvanceProcurement(int id, ProcurementStatus status,
            int? selectedQuoteId, decimal? amount, string? reference)
        {
            var request = await _db.ProcurementRequests.FirstOrDefaultAsync(p => p.Id == id);
            if (request == null) return NotFound();

            var ctx = await LoadContextAsync(request.ProjectId);
            if (ctx == null) return NotFound();
            if (!Roles.PmProcurement.Contains(Role) && !ctx.Value.CanEdit) return AccessDenied();

            if (request.Status == ProcurementStatus.Draft || request.Status == ProcurementStatus.Submitted)
            {
                TempData["Error"] = "This purchase request must be approved before it can proceed.";
                return RedirectToAction(nameof(ProcurementDetails), new { id });
            }

            switch (status)
            {
                case ProcurementStatus.SupplierSelected when selectedQuoteId is int quoteId:
                    var quotes = await _db.ProcurementQuotes.Where(q => q.ProcurementRequestId == id).ToListAsync();
                    foreach (var quote in quotes) quote.IsSelected = quote.Id == quoteId;
                    var chosen = quotes.FirstOrDefault(q => q.Id == quoteId);
                    if (chosen != null)
                    {
                        request.SelectedSupplierId = chosen.SupplierId;
                        request.SelectedSupplierName = chosen.SupplierName;
                        request.SupplierSelectionRationale = reference;
                    }
                    break;

                case ProcurementStatus.Ordered:
                    request.OrderedAmount = amount ?? request.EstimatedCost;
                    request.PurchaseOrderNumber = reference;
                    request.OrderedDate = DateTime.Today;
                    // Committing money reserves it on the budget line so the forecast stays honest.
                    if (request.BudgetLineId is int lineId)
                    {
                        var line = await _db.BudgetLines.FirstOrDefaultAsync(l => l.Id == lineId);
                        if (line != null) line.CommittedAmount += request.OrderedAmount;
                    }
                    break;

                case ProcurementStatus.GoodsReceived:
                    request.GoodsReceivedNoteNumber = reference;
                    request.ReceivedDate = DateTime.Today;
                    break;

                case ProcurementStatus.Invoiced:
                    request.InvoicedAmount = amount ?? request.OrderedAmount;
                    request.InvoiceNumber = reference;
                    request.InvoiceDate = DateTime.Today;
                    break;

                case ProcurementStatus.Paid:
                    request.PaidAmount = amount ?? request.InvoicedAmount;
                    request.PaidDate = DateTime.Today;
                    // Payment converts the commitment into actual spend.
                    if (request.BudgetLineId is int paidLineId)
                    {
                        var line = await _db.BudgetLines.FirstOrDefaultAsync(l => l.Id == paidLineId);
                        if (line != null)
                        {
                            line.CommittedAmount = Math.Max(0, line.CommittedAmount - request.OrderedAmount);
                            line.ActualAmount += request.PaidAmount;
                        }
                    }
                    break;
            }

            _activity.LogChange(request.ProjectId, nameof(ProcurementRequest), id, "Status", request.Status, status);
            request.Status = status;
            await _db.SaveChangesAsync();
            await _metrics.RefreshProjectAsync(request.ProjectId);
            await WarnIfOverBudgetAsync(request.ProjectId);

            TempData["Success"] = $"Purchase moved to {status}.";
            return RedirectToAction(nameof(ProcurementDetails), new { id });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteProcurement(int projectId, int requestId)
        {
            var ctx = await LoadContextAsync(projectId);
            if (ctx == null) return NotFound();
            if (!ctx.Value.CanEdit && !Roles.IsFullAccess(Role)) return AccessDenied();

            var request = await _db.ProcurementRequests.FirstOrDefaultAsync(p => p.Id == requestId && p.ProjectId == projectId);
            if (request == null) return NotFound();

            if (request.Status is ProcurementStatus.Paid or ProcurementStatus.Invoiced && !Roles.IsFullAccess(Role))
            {
                TempData["Error"] = "An invoiced or paid purchase is a financial record and cannot be deleted.";
                return RedirectToAction(nameof(Procurement), new { projectId });
            }

            _db.ProcurementRequests.Remove(request);
            _activity.Log(projectId, nameof(ProcurementRequest), requestId, "Deleted", request.Title);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Purchase request removed.";
            return RedirectToAction(nameof(Procurement), new { projectId });
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Assets on the project
        // ════════════════════════════════════════════════════════════════════════

        public async Task<IActionResult> Assets(int projectId, bool outstandingOnly = false)
        {
            var ctx = await LoadContextAsync(projectId);
            if (ctx == null) return NotFound();
            if (!ctx.Value.CanView) return AccessDenied();

            IQueryable<ProjectAsset> query = _db.ProjectAssets.AsNoTracking()
                .Include(a => a.IssuedTo).Include(a => a.Asset)
                .Where(a => a.ProjectId == projectId);
            if (outstandingOnly) query = query.Where(a => a.ReturnedDate == null);

            var assets = await query.OrderByDescending(a => a.IssuedDate).ToListAsync();

            ViewBag.OutstandingOnly = outstandingOnly;
            ViewBag.Outstanding = assets.Count(a => a.IsOutstanding);
            ViewBag.Overdue = assets.Count(a => a.IsOutstanding && a.DueBackDate < DateTime.Today);
            ViewBag.Users = await _db.Users.AsNoTracking()
                .Where(u => u.IsActive).OrderBy(u => u.FirstName)
                .Select(u => new { u.Id, Name = u.FirstName + " " + u.LastName }).ToListAsync();
            // Pull from the organisation-wide asset register so an issued item can be linked to it.
            ViewBag.RegisteredAssets = await _db.Assets.AsNoTracking()
                .OrderBy(a => a.ItemName).Take(500)
                .Select(a => new { a.Id, Name = a.ItemName + (a.AssetTag == null ? "" : " · " + a.AssetTag) })
                .ToListAsync();

            return View(assets);
        }

        [HttpPost]
        public async Task<IActionResult> SaveAsset(ProjectAsset input)
        {
            var ctx = await LoadContextAsync(input.ProjectId);
            if (ctx == null) return NotFound();
            if (!ctx.Value.CanContribute) return AccessDenied();

            if (string.IsNullOrWhiteSpace(input.Name))
            {
                TempData["Error"] = "An asset needs a name.";
                return RedirectToAction(nameof(Assets), new { projectId = input.ProjectId });
            }
            if (input.AssetId == 0) input.AssetId = null;

            if (input.Id == 0)
            {
                input.IssuedDate ??= DateTime.Today;
                _db.ProjectAssets.Add(input);
                _activity.Log(input.ProjectId, nameof(ProjectAsset), null, "Issued", input.Name);
            }
            else
            {
                var asset = await _db.ProjectAssets.FirstOrDefaultAsync(a => a.Id == input.Id && a.ProjectId == input.ProjectId);
                if (asset == null) return NotFound();

                asset.Name = input.Name;
                asset.AssetTag = input.AssetTag;
                asset.Kind = input.Kind;
                asset.AssetId = input.AssetId;
                asset.IssuedToId = input.IssuedToId;
                asset.IssuedDate = input.IssuedDate;
                asset.DueBackDate = input.DueBackDate;
                asset.ConditionOnIssue = input.ConditionOnIssue;
                asset.Notes = input.Notes;
            }

            await _db.SaveChangesAsync();
            TempData["Success"] = "Asset record saved.";
            return RedirectToAction(nameof(Assets), new { projectId = input.ProjectId });
        }

        [HttpPost]
        public async Task<IActionResult> ReturnAsset(int projectId, int assetId, string? condition)
        {
            var ctx = await LoadContextAsync(projectId);
            if (ctx == null) return NotFound();
            if (!ctx.Value.CanContribute) return AccessDenied();

            var asset = await _db.ProjectAssets.FirstOrDefaultAsync(a => a.Id == assetId && a.ProjectId == projectId);
            if (asset != null)
            {
                asset.ReturnedDate = DateTime.Today;
                asset.ConditionOnReturn = condition;
                _activity.Log(projectId, nameof(ProjectAsset), assetId, "Returned", $"{asset.Name} · {condition}");
                await _db.SaveChangesAsync();
            }

            TempData["Success"] = "Asset marked returned.";
            return RedirectToAction(nameof(Assets), new { projectId });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteAsset(int projectId, int assetId)
        {
            var ctx = await LoadContextAsync(projectId);
            if (ctx == null) return NotFound();
            if (!ctx.Value.CanEdit) return AccessDenied();

            var asset = await _db.ProjectAssets.FirstOrDefaultAsync(a => a.Id == assetId && a.ProjectId == projectId);
            if (asset != null) { _db.ProjectAssets.Remove(asset); await _db.SaveChangesAsync(); }

            return RedirectToAction(nameof(Assets), new { projectId });
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Helpers
        // ════════════════════════════════════════════════════════════════════════

        private async Task<(Project Project, bool CanView, bool CanEdit, bool CanContribute)?> LoadContextAsync(int projectId)
        {
            var (project, team) = await ProjectsController.LoadAsync(_db, projectId);
            if (project == null) return null;

            var teamIds = team.Select(t => t.UserId).ToList();
            var canView = PmAccess.CanView(project, Uid, Role, teamIds);
            var canEdit = PmAccess.CanEdit(project, Uid, Role);
            var canContribute = PmAccess.CanContribute(project, Uid, Role, teamIds);

            ViewBag.Project = project;
            ViewBag.Team = team;
            ViewBag.CanEdit = canEdit;
            ViewBag.CanContribute = canContribute;
            return (project, canView, canEdit, canContribute);
        }
    }
}
