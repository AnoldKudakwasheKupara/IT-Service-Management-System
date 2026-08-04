using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Models.Hr;
using Microsoft.EntityFrameworkCore;

namespace IT_Service_Management_System.Services.Hr
{
    /// <summary>
    /// Onboarding: building a joiner's programme from a template, and reporting what is outstanding.
    /// <para>
    /// The statutory steps are the point. An employer must register a new employee with NSSA, give
    /// written particulars of employment, give the code of conduct the employee will be disciplined
    /// under, and induct them on safety. Those are the steps most easily lost in a busy first week,
    /// and the ones with consequences, so they are seeded, marked, and reported separately.
    /// </para>
    /// </summary>
    public class OnboardingService
    {
        private readonly ApplicationDbContext _db;

        public OnboardingService(ApplicationDbContext db) { _db = db; }

        public record StartResult(bool Succeeded, string Message, int? ProgrammeId = null);

        /// <summary>
        /// Build a programme for an employee from a template.
        /// <para>
        /// Steps are copied, not referenced. Editing a template later must not rewrite what somebody
        /// was actually asked to do six months ago.
        /// </para>
        /// </summary>
        public async Task<StartResult> StartAsync(int employeeId, int? templateId, DateTime? startDate, int? buddyId)
        {
            var employee = await _db.Employees.FirstOrDefaultAsync(e => e.Id == employeeId);
            if (employee == null) return new StartResult(false, "Employee not found.");

            if (await _db.OnboardingProgrammes.AnyAsync(p => p.EmployeeId == employeeId
                                                          && p.Status != OnboardingStatus.Abandoned))
                return new StartResult(false, $"{employee.DisplayName} already has an onboarding programme.");

            var template = templateId.HasValue
                ? await _db.OnboardingTemplates.Include(t => t.Tasks)
                    .FirstOrDefaultAsync(t => t.Id == templateId.Value)
                : await PickTemplateAsync(employee.EmploymentType);

            if (template == null)
                return new StartResult(false, "No onboarding template is available. Create one first.");

            var start = (startDate ?? employee.HireDate ?? DateTime.Today).Date;

            var programme = new OnboardingProgramme
            {
                EmployeeId = employeeId,
                TemplateId = template.Id,
                StartDate = start,
                BuddyId = buddyId,
                Status = OnboardingStatus.NotStarted
            };

            foreach (var t in template.Tasks.OrderBy(t => t.DisplayOrder))
            {
                programme.Tasks.Add(new OnboardingTask
                {
                    Title = t.Title,
                    Detail = t.Detail,
                    Category = t.Category,
                    Owner = t.Owner,
                    DueDate = start.AddDays(t.DueDayOffset),
                    IsStatutory = t.IsStatutory,
                    Authority = t.Authority,
                    DisplayOrder = t.DisplayOrder
                });
            }

            _db.OnboardingProgrammes.Add(programme);
            await _db.SaveChangesAsync();

            var statutory = programme.Tasks.Count(t => t.IsStatutory);
            return new StartResult(true,
                $"Onboarding started for {employee.DisplayName} — {programme.Tasks.Count} step(s), "
                + $"{statutory} of them required by law.", programme.Id);
        }

        /// <summary>
        /// The template that fits this employment type, falling back to the default. A contractor's
        /// induction is not a permanent employee's, so the match is tried before the fallback.
        /// </summary>
        private async Task<OnboardingTemplate?> PickTemplateAsync(EmploymentType type)
        {
            return await _db.OnboardingTemplates.Include(t => t.Tasks)
                       .FirstOrDefaultAsync(t => t.IsActive && t.AppliesTo == type)
                ?? await _db.OnboardingTemplates.Include(t => t.Tasks)
                       .FirstOrDefaultAsync(t => t.IsActive && t.IsDefault)
                ?? await _db.OnboardingTemplates.Include(t => t.Tasks)
                       .FirstOrDefaultAsync(t => t.IsActive && t.AppliesTo == null);
        }

        /// <summary>
        /// Tick a step off. Evidence is required on statutory steps: a tick proves nothing, whereas
        /// "registered, NSSA reference 123456" proves something.
        /// </summary>
        public async Task<(bool Succeeded, string Message)> CompleteAsync(int taskId, string? evidence, int userId)
        {
            var task = await _db.OnboardingTasks.Include(t => t.Programme).ThenInclude(p => p!.Tasks)
                .FirstOrDefaultAsync(t => t.Id == taskId);

            if (task == null) return (false, "Step not found.");

            if (task.IsStatutory && string.IsNullOrWhiteSpace(evidence))
                return (false, $"\"{task.Title}\" is required by "
                             + $"{task.Authority ?? "law"}. Record what was done or issued — a tick on "
                             + "its own is not evidence that it happened.");

            task.IsComplete = true;
            task.CompletedAt = DateTime.Now;
            task.CompletedById = userId;
            task.Evidence = evidence;

            var programme = task.Programme!;
            if (programme.Tasks.All(t => t.IsComplete))
            {
                programme.Status = OnboardingStatus.Complete;
                programme.CompletedAt = DateTime.Now;
            }
            else if (programme.Status == OnboardingStatus.NotStarted)
            {
                programme.Status = OnboardingStatus.InProgress;
            }

            await _db.SaveChangesAsync();

            return (true, programme.Status == OnboardingStatus.Complete
                ? "Step recorded. That completes the programme."
                : "Step recorded.");
        }

        public record OnboardingOverview(
            int InProgress,
            int StartingSoon,
            int OverdueStatutory,
            int OverdueOther);

        public async Task<OnboardingOverview> OverviewAsync()
        {
            var open = await _db.OnboardingProgrammes.AsNoTracking()
                .Include(p => p.Tasks)
                .Where(p => p.Status == OnboardingStatus.NotStarted || p.Status == OnboardingStatus.InProgress)
                .ToListAsync();

            var overdue = open.SelectMany(p => p.Tasks)
                .Where(t => !t.IsComplete && t.DueDate < DateTime.Today).ToList();

            return new OnboardingOverview(
                open.Count,
                open.Count(p => p.StartDate >= DateTime.Today && p.StartDate <= DateTime.Today.AddDays(14)),
                overdue.Count(t => t.IsStatutory),
                overdue.Count(t => !t.IsStatutory));
        }

        /// <summary>
        /// Seed the default programme where none exists. Additive and idempotent — a template that
        /// has been edited is left alone.
        /// <para>
        /// Two figures are worth checking against current practice before this is relied on: the
        /// window for NSSA registration of a new employee, and whatever the employer's own safety
        /// induction requires under the Factories and Works Act. Both are recorded as steps with
        /// their authority named rather than buried as assumptions.
        /// </para>
        /// </summary>
        public async Task<int> SeedDefaultTemplateAsync(CancellationToken ct = default)
        {
            if (await _db.OnboardingTemplates.AnyAsync(ct)) return 0;

            var template = new OnboardingTemplate
            {
                Name = "Standard onboarding",
                Description = "The steps that apply to every new employee, including the ones required "
                            + "by law. Adjust to your own practice; do not remove the statutory steps.",
                IsDefault = true,
                IsActive = true
            };

            void Add(string title, OnboardingCategory category, OnboardingOwner owner, int offset,
                string? detail = null, bool statutory = false, string? authority = null)
            {
                template.Tasks.Add(new OnboardingTaskTemplate
                {
                    Title = title,
                    Detail = detail,
                    Category = category,
                    Owner = owner,
                    DueDayOffset = offset,
                    IsStatutory = statutory,
                    Authority = authority,
                    DisplayOrder = template.Tasks.Count + 1
                });
            }

            // ── Before day one ──
            Add("Contract of employment signed", OnboardingCategory.Contract, OnboardingOwner.Hr, -5,
                "Signed by both parties before the employee starts. A contract signed on the first "
                + "morning is a contract signed late.",
                statutory: true, authority: "Labour Act [Chapter 28:01]");

            Add("Written particulars of employment issued", OnboardingCategory.Contract, OnboardingOwner.Hr, -1,
                "Post, wage, hours, leave entitlement, notice period and the code of conduct that applies.",
                statutory: true, authority: "Labour Act [Chapter 28:01]");

            Add("Code of conduct issued and explained", OnboardingCategory.Contract, OnboardingOwner.Hr, 0,
                "The registered code, or the National Employment Code of Conduct in SI 15 of 2006 where "
                + "the employer has none. An employee cannot be disciplined under a code they were never given.",
                statutory: true, authority: "SI 15 of 2006 / registered code of conduct");

            // ── Statutory registrations ──
            Add("Registered with NSSA", OnboardingCategory.StatutoryRegistration, OnboardingOwner.Hr, 5,
                "Pension and Other Benefits Scheme, and the Accident Prevention and Workers' "
                + "Compensation Scheme. Record the NSSA number as evidence.",
                statutory: true, authority: "NSSA Act [Chapter 17:04]");

            Add("Tax details recorded for PAYE", OnboardingCategory.StatutoryRegistration, OnboardingOwner.Hr, 3,
                "Tax number and the details ZIMRA requires, so the first payslip deducts correctly.",
                statutory: true, authority: "Income Tax Act [Chapter 23:06]");

            // ── Safety ──
            Add("Health and safety induction", OnboardingCategory.Safety, OnboardingOwner.Safety, 1,
                "Hazards, emergency procedures, protective equipment, and who to report to. Record who "
                + "gave it and what was covered.",
                statutory: true, authority: "Factories and Works Act [Chapter 14:08]");

            // ── Payroll ──
            Add("Bank details captured", OnboardingCategory.Payroll, OnboardingOwner.Hr, 3);
            Add("Salary structure created", OnboardingCategory.Payroll, OnboardingOwner.Hr, 3,
                "Effective from the start date, so the first payroll run picks it up.");
            Add("Added to the payroll run", OnboardingCategory.Payroll, OnboardingOwner.Finance, 10);
            Add("Leave balances opened", OnboardingCategory.Payroll, OnboardingOwner.Hr, 3,
                "Vacation leave accrues from the start date. Opening the balance late means back-dating it.");

            // ── Induction ──
            Add("Next of kin and emergency contact recorded", OnboardingCategory.Administration, OnboardingOwner.Hr, 1);
            Add("Introduced to the team", OnboardingCategory.Induction, OnboardingOwner.LineManager, 0);
            Add("Buddy assigned", OnboardingCategory.Induction, OnboardingOwner.LineManager, 0);
            Add("Job description and objectives discussed", OnboardingCategory.Induction, OnboardingOwner.LineManager, 5,
                "What the job is, and what good looks like in the first three months.");
            Add("Probation review scheduled", OnboardingCategory.Induction, OnboardingOwner.LineManager, 7,
                "Booked now, for before the probation period ends. A probation that lapses unreviewed "
                + "confirms itself by default.");

            // ── Equipment and access ──
            Add("Workstation and equipment issued", OnboardingCategory.Equipment, OnboardingOwner.It, 0);
            Add("System accounts created", OnboardingCategory.SystemAccess, OnboardingOwner.It, 0,
                "Access appropriate to the role. Granting more than the job needs is harder to undo later.");
            Add("Access card and keys issued", OnboardingCategory.Equipment, OnboardingOwner.Hr, 0);

            // ── Training ──
            Add("Policies read and acknowledged", OnboardingCategory.Training, OnboardingOwner.Employee, 14);
            Add("Role-specific training arranged", OnboardingCategory.Training, OnboardingOwner.LineManager, 21);

            _db.OnboardingTemplates.Add(template);
            await _db.SaveChangesAsync(ct);

            return template.Tasks.Count;
        }
    }
}
