using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Helpers.Hr;
using IT_Service_Management_System.Models.Hr;
using Microsoft.EntityFrameworkCore;

namespace IT_Service_Management_System.Services.Hr
{
    /// <summary>
    /// Disciplinary process support: what penalty the code points to given what has gone before,
    /// what a dismissal would cost, and the procedural checks a fair process needs.
    /// <para>
    /// The service advises; it never decides. Every suggestion is returned with the reasoning
    /// behind it so the chairperson can disagree — an employer that cannot explain why a penalty
    /// was chosen has a problem whichever way the decision went.
    /// </para>
    /// </summary>
    public class DisciplinaryService
    {
        private readonly ApplicationDbContext _db;
        private readonly StatutoryService _statutory;

        public DisciplinaryService(ApplicationDbContext db, StatutoryService statutory)
        {
            _db = db; _statutory = statutory;
        }

        /// <summary>
        /// Live warnings for an employee — those not yet expired. A spent warning cannot be counted
        /// towards progression, which is the most common error in a progressive-discipline record.
        /// </summary>
        public async Task<List<DisciplinaryCase>> LiveWarningsAsync(int employeeId, int? excludeCaseId = null)
        {
            var cases = await _db.DisciplinaryCases.AsNoTracking()
                .Include(c => c.Offence)
                .Where(c => c.EmployeeId == employeeId
                         && c.Id != (excludeCaseId ?? 0)
                         && c.Finding == DisciplinaryFinding.Proven
                         && c.WarningExpiryDate != null
                         && c.WarningExpiryDate >= DateTime.Today)
                .OrderByDescending(c => c.PenaltyDate)
                .ToListAsync();

            return cases.Where(c => c.IsWarningLive).ToList();
        }

        /// <summary>
        /// What the code points to for this offence, given the employee's live warnings. Returns
        /// the reasoning as well as the penalty, because the reasoning is what has to be recorded.
        /// </summary>
        public async Task<PenaltyGuidance> SuggestPenaltyAsync(int employeeId, int offenceId, int? excludeCaseId = null)
        {
            var offence = await _db.DisciplinaryOffences.AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == offenceId);
            if (offence == null)
                return new PenaltyGuidance(DisciplinaryPenalty.None, "No offence selected.", new List<string>());

            var live = await LiveWarningsAsync(employeeId, excludeCaseId);
            var reasoning = new List<string>();

            reasoning.Add($"{offence.Name} is classified as {offence.Seriousness.ToString().ToLowerInvariant()} "
                        + $"under {offence.Authority ?? "the applicable code of conduct"}.");

            // Gross misconduct that the code allows dismissal for on a first occasion.
            if (offence.DismissableFirstOffence)
            {
                reasoning.Add("The code allows dismissal for this offence even on a first occasion, "
                            + "so previous record is not what decides it — the seriousness of the act is.");
                reasoning.Add("Mitigation must still be weighed before dismissing. Length of service, "
                            + "a clean record and the circumstances of the incident all count.");
                return new PenaltyGuidance(DisciplinaryPenalty.SummaryDismissal,
                    "Dismissal is available on a first offence for this charge.", reasoning);
            }

            if (live.Count == 0)
            {
                reasoning.Add("The employee has no live warnings, so this is treated as a first offence.");
                return new PenaltyGuidance(offence.DefaultFirstPenalty,
                    "First offence, no live warnings on record.", reasoning);
            }

            reasoning.Add($"The employee has {live.Count} live warning(s): "
                        + string.Join("; ", live.Select(w =>
                            $"{Describe(w.Penalty)} for {w.Offence?.Name ?? w.Title} on {w.PenaltyDate:d MMM yyyy}, "
                            + $"expiring {w.WarningExpiryDate:d MMM yyyy}")) + ".");

            // Progression steps up from the most severe warning still live.
            var highest = live.Max(w => w.Penalty);
            var next = highest switch
            {
                DisciplinaryPenalty.VerbalWarning => DisciplinaryPenalty.WrittenWarning,
                DisciplinaryPenalty.WrittenWarning => DisciplinaryPenalty.FinalWritten,
                DisciplinaryPenalty.FinalWritten => DisciplinaryPenalty.DismissalOnNotice,
                _ => DisciplinaryPenalty.FinalWritten
            };

            reasoning.Add($"Progressive discipline from the most severe live warning "
                        + $"({Describe(highest)}) points to {Describe(next)}.");

            if (next is DisciplinaryPenalty.DismissalOnNotice)
                reasoning.Add("Dismissal after a final written warning still requires the current "
                            + "allegation to be proven on its own facts. A live warning is context, "
                            + "not proof of the new charge.");

            return new PenaltyGuidance(next,
                $"Progression from {live.Count} live warning(s).", reasoning);
        }

        public record PenaltyGuidance(DisciplinaryPenalty Suggested, string Summary, List<string> Reasoning);

        private static string Describe(DisciplinaryPenalty penalty) => penalty switch
        {
            DisciplinaryPenalty.VerbalWarning => "a verbal warning",
            DisciplinaryPenalty.WrittenWarning => "a written warning",
            DisciplinaryPenalty.FinalWritten => "a final written warning",
            DisciplinaryPenalty.SuspensionWithoutPay => "suspension without pay",
            DisciplinaryPenalty.Demotion => "demotion",
            DisciplinaryPenalty.DismissalOnNotice => "dismissal on notice",
            DisciplinaryPenalty.SummaryDismissal => "summary dismissal",
            _ => "no penalty"
        };

        /// <summary>
        /// What a dismissal on notice would cost — notice pay and any leave that must be paid out.
        /// <para>
        /// Summary dismissal for misconduct going to the root of the contract does not attract
        /// notice pay; dismissal on notice does. Accrued leave is paid out either way, because it
        /// is money already earned.
        /// </para>
        /// </summary>
        public async Task<TerminationCost> EstimateTerminationCostAsync(int employeeId, DisciplinaryPenalty penalty)
        {
            var employee = await _db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == employeeId);
            if (employee == null) return new TerminationCost();

            var salary = await _db.SalaryStructures.AsNoTracking()
                .Where(s => s.EmployeeId == employeeId
                         && s.EffectiveFrom <= DateTime.Today
                         && (s.EffectiveTo == null || s.EffectiveTo >= DateTime.Today))
                .OrderByDescending(s => s.EffectiveFrom)
                .FirstOrDefaultAsync();

            var monthly = salary?.BasicSalary ?? 0m;
            var currency = salary?.Currency ?? "USD";

            var cost = new TerminationCost { Currency = currency, MonthlySalary = monthly };

            // Notice is owed on a dismissal on notice, not on a summary dismissal.
            if (penalty == DisciplinaryPenalty.DismissalOnNotice)
            {
                var contractMonths = employee.HireDate.HasValue
                    ? (DateTime.Today - employee.HireDate.Value).TotalDays / 30.44
                    : 0;

                var notice = ZimbabweLabourLaw.MinimumNotice(employee.EmploymentType, contractMonths);
                cost.NoticePeriod = notice.ToString();
                cost.NoticeAuthority = notice.Authority;

                var months = notice.Unit switch
                {
                    ZimbabweLabourLaw.NoticeUnit.Months => notice.Length,
                    ZimbabweLabourLaw.NoticeUnit.Weeks => notice.Length / 4.33m,
                    _ => notice.Length / 30.44m
                };
                cost.NoticePay = Math.Round(monthly * (decimal)months, 2);
            }
            else if (penalty == DisciplinaryPenalty.SummaryDismissal)
            {
                cost.NoticePeriod = "None";
                cost.NoticeAuthority = "Summary dismissal — no notice is payable where the misconduct "
                                     + "goes to the root of the contract. If that threshold is not met, "
                                     + "notice pay becomes due.";
            }

            // Accrued leave is earned money and is paid out however employment ends.
            var year = DateTime.Today.Year;
            var balances = await _db.LeaveBalances.AsNoTracking()
                .Include(b => b.LeaveType)
                .Where(b => b.EmployeeId == employeeId && b.CycleYear == year
                         && b.LeaveType!.PaidOutOnTermination)
                .ToListAsync();

            var leaveDays = balances.Sum(b => b.Available);
            if (leaveDays > 0 && monthly > 0)
            {
                cost.LeaveDays = leaveDays;
                cost.LeavePayout = Math.Round(leaveDays * (monthly / 22m), 2);
            }

            return cost;
        }

        public class TerminationCost
        {
            public string Currency { get; set; } = "USD";
            public decimal MonthlySalary { get; set; }
            public string? NoticePeriod { get; set; }
            public string? NoticeAuthority { get; set; }
            public decimal NoticePay { get; set; }
            public decimal LeaveDays { get; set; }
            public decimal LeavePayout { get; set; }

            public decimal Total => NoticePay + LeavePayout;
        }

        /// <summary>
        /// Set the expiry on a warning from the offence's validity period, so a spent warning
        /// stops counting automatically rather than needing anyone to remember.
        /// </summary>
        public static DateTime? WarningExpiry(DisciplinaryPenalty penalty, DateTime penaltyDate, int validityMonths)
        {
            if (penalty is not (DisciplinaryPenalty.VerbalWarning
                or DisciplinaryPenalty.WrittenWarning
                or DisciplinaryPenalty.FinalWritten))
                return null;

            return penaltyDate.AddMonths(validityMonths > 0 ? validityMonths : 12);
        }

        /// <summary>Record a step on the case file, so the process can be reconstructed later.</summary>
        public void RecordEvent(DisciplinaryCase disciplinaryCase, string step, string? detail, int? userId)
        {
            _db.DisciplinaryEvents.Add(new DisciplinaryEvent
            {
                CaseId = disciplinaryCase.Id,
                Step = step,
                Detail = detail,
                RecordedById = userId,
                At = DateTime.Now
            });
        }
    }
}
