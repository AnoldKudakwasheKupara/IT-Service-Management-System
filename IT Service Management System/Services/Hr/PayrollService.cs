using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Models.Hr;
using Microsoft.EntityFrameworkCore;

namespace IT_Service_Management_System.Services.Hr
{
    /// <summary>
    /// Calculates payroll against the Zimbabwean statutory position.
    /// <para>
    /// The order matters and is not arbitrary. Gross is assembled first; NSSA is deducted next
    /// because pension contributions are allowable against taxable income; PAYE is then computed
    /// on what remains; and the AIDS levy is charged on the PAYE payable rather than on income.
    /// Getting that sequence wrong overstates tax on every payslip.
    /// </para>
    /// <para>
    /// Every rate is read as at the run's <see cref="PayrollRun.StatutoryAsAt"/> date, so rerunning
    /// a past month applies the tables that were in force then rather than today's.
    /// </para>
    /// </summary>
    public class PayrollService
    {
        private readonly ApplicationDbContext _db;
        private readonly StatutoryService _statutory;
        private readonly ILogger<PayrollService> _log;

        public PayrollService(ApplicationDbContext db, StatutoryService statutory, ILogger<PayrollService> log)
        {
            _db = db; _statutory = statutory; _log = log;
        }

        /// <summary>
        /// Calculate every payslip in a run, replacing anything already there. Refuses once the run
        /// is approved — a payslip that can change after issue is worthless as evidence.
        /// </summary>
        public async Task<PayrollResult> CalculateAsync(int runId, CancellationToken ct = default)
        {
            var run = await _db.PayrollRuns.FirstOrDefaultAsync(r => r.Id == runId, ct);
            if (run == null) return new PayrollResult { Error = "Payroll run not found." };

            if (run.IsLocked)
                return new PayrollResult { Error = $"{run.Reference} is {run.Status} and can no longer be recalculated." };

            var asAt = run.StatutoryAsAt;
            var periodStart = new DateTime(run.PeriodYear, run.PeriodMonth, 1);
            var periodEnd = periodStart.AddMonths(1).AddDays(-1);

            // Warn rather than fail when the tables are missing: a run that silently deducts
            // nothing is far more dangerous than one that says why it could not.
            var result = new PayrollResult();
            var ceiling = await _statutory.ValueAsync(StatutoryKeys.NssaInsurableEarningsCeiling, asAt);
            if (ceiling <= 0)
                result.Warnings.Add("No NSSA insurable-earnings ceiling is configured, so contributions "
                                  + "are being calculated on uncapped earnings. Set it before paying.");

            var bandsExist = await _db.PayeTaxBands.AnyAsync(b =>
                b.Currency == run.Currency && b.EffectiveFrom <= asAt
                && (b.EffectiveTo == null || b.EffectiveTo >= asAt), ct);
            if (!bandsExist)
                result.Warnings.Add($"No PAYE bands are configured for {run.Currency} at {asAt:d MMM yyyy}, "
                                  + "so no tax is being deducted. Load the current tables before paying.");

            // Clear any previous calculation for this run.
            var existing = await _db.Payslips.Where(p => p.PayrollRunId == runId).Select(p => p.Id).ToListAsync(ct);
            if (existing.Count > 0)
            {
                _db.PayslipLines.RemoveRange(_db.PayslipLines.Where(l => existing.Contains(l.PayslipId)));
                _db.Payslips.RemoveRange(_db.Payslips.Where(p => p.PayrollRunId == runId));
                await _db.SaveChangesAsync(ct);
            }

            // Everybody on the payroll for this period, in this currency.
            var structures = await _db.SalaryStructures.AsNoTracking()
                .Include(s => s.Employee)
                .Where(s => s.Currency == run.Currency
                         && s.EffectiveFrom <= periodEnd
                         && (s.EffectiveTo == null || s.EffectiveTo >= periodStart))
                .ToListAsync(ct);

            // A back-dated increase can leave two structures overlapping; the later one wins.
            var current = structures
                .Where(s => s.Employee != null && s.Employee.IsCurrentEmployee)
                .GroupBy(s => s.EmployeeId)
                .Select(g => g.OrderByDescending(s => s.EffectiveFrom).First())
                .ToList();

            if (current.Count == 0)
            {
                result.Error = $"Nobody has a {run.Currency} salary structure effective in {run.PeriodName}.";
                return result;
            }

            var levyRates = await _statutory.ValuesAsync(
                new[] { StatutoryKeys.ZimdefRate, StatutoryKeys.StandardsDevelopmentLevyRate }, asAt);

            foreach (var structure in current)
            {
                var payslip = await BuildPayslipAsync(run, structure, periodStart, periodEnd, asAt, levyRates, ct);
                _db.Payslips.Add(payslip);
                result.Payslips++;
            }

            await _db.SaveChangesAsync(ct);
            await RollUpTotalsAsync(run, ct);

            run.Status = PayrollRunStatus.Calculated;
            await _db.SaveChangesAsync(ct);

            _log.LogInformation("Payroll {Reference} calculated: {Count} payslip(s).", run.Reference, result.Payslips);
            return result;
        }

        public class PayrollResult
        {
            public int Payslips { get; set; }
            public List<string> Warnings { get; } = new();
            public string? Error { get; set; }
            public bool Succeeded => Error == null;
        }

        // ── One payslip ──────────────────────────────────────────────────────────

        private async Task<Payslip> BuildPayslipAsync(PayrollRun run, SalaryStructure structure,
            DateTime periodStart, DateTime periodEnd, DateTime asAt,
            Dictionary<string, decimal> levyRates, CancellationToken ct)
        {
            var payslip = new Payslip
            {
                PayrollRunId = run.Id,
                EmployeeId = structure.EmployeeId,
                Currency = run.Currency,
                BasicSalary = structure.BasicSalary,
                CalculatedAt = DateTime.Now
            };

            var lines = new List<PayslipLine>();
            var order = 0;

            void Line(string description, PayslipLineKind kind, decimal amount, string? basis = null)
            {
                if (amount == 0) return;
                lines.Add(new PayslipLine
                {
                    Description = description, Kind = kind, Amount = amount,
                    Basis = basis, DisplayOrder = order++
                });
            }

            Line("Basic salary", PayslipLineKind.Earning, structure.BasicSalary);

            // ── Variable components ──
            var components = await _db.PayComponents.AsNoTracking()
                .Where(c => c.EmployeeId == structure.EmployeeId && c.IsActive
                         && c.EffectiveFrom <= periodEnd
                         && (c.EffectiveTo == null || c.EffectiveTo >= periodStart))
                .ToListAsync(ct);

            decimal taxableEarnings = structure.BasicSalary;
            decimal pensionableEarnings = structure.BasicSalary;

            foreach (var component in components)
            {
                // A one-off only applies in the period it falls in.
                if (!component.IsRecurring && (component.EffectiveFrom < periodStart || component.EffectiveFrom > periodEnd))
                    continue;

                var amount = component.PercentageOfBasic.HasValue
                    ? Math.Round(structure.BasicSalary * component.PercentageOfBasic.Value / 100m, 2)
                    : component.Amount;
                if (amount == 0) continue;

                switch (component.Type)
                {
                    case PayComponentType.Allowance:
                        payslip.Allowances += amount;
                        Line(component.Name, PayslipLineKind.Earning, amount);
                        if (component.IsTaxable) taxableEarnings += amount;
                        if (component.IsPensionable) pensionableEarnings += amount;
                        break;

                    case PayComponentType.Earning:
                        // Overtime is separated out because it is the line most often queried.
                        if (component.Name.Contains("overtime", StringComparison.OrdinalIgnoreCase))
                            payslip.Overtime += amount;
                        else
                            payslip.OtherEarnings += amount;
                        Line(component.Name, PayslipLineKind.Earning, amount);
                        if (component.IsTaxable) taxableEarnings += amount;
                        if (component.IsPensionable) pensionableEarnings += amount;
                        break;

                    case PayComponentType.Reimbursement:
                        // A reimbursement of actual expense is not income — it is never taxed and
                        // never counts towards NSSA, whatever the flags happen to say.
                        payslip.Reimbursements += amount;
                        Line(component.Name, PayslipLineKind.Earning, amount, "Reimbursement — not taxable income");
                        break;

                    case PayComponentType.LoanRepayment:
                        payslip.LoanRepayments += amount;
                        break;

                    case PayComponentType.Garnishee:
                    case PayComponentType.Deduction:
                        payslip.OtherDeductions += amount;
                        break;
                }
            }

            payslip.Gross = structure.BasicSalary + payslip.Allowances + payslip.Overtime
                          + payslip.OtherEarnings + payslip.Reimbursements;

            // ── Unpaid and half-pay absence ──
            var absence = await AbsenceDeductionAsync(structure, periodStart, periodEnd, ct);
            payslip.UnpaidLeaveDays = absence.UnpaidDays;
            payslip.UnpaidLeaveDeduction = absence.UnpaidAmount;
            payslip.HalfPayLeaveDays = absence.HalfPayDays;
            payslip.HalfPayLeaveDeduction = absence.HalfPayAmount;

            if (absence.UnpaidAmount > 0)
            {
                Line($"Unpaid leave — {absence.UnpaidDays:0.##} day(s)", PayslipLineKind.Deduction,
                    -absence.UnpaidAmount);
                taxableEarnings -= absence.UnpaidAmount;
                pensionableEarnings -= absence.UnpaidAmount;
                payslip.Gross -= absence.UnpaidAmount;
            }

            if (absence.HalfPayAmount > 0)
            {
                Line($"Sick leave at half pay — {absence.HalfPayDays:0.##} day(s)", PayslipLineKind.Deduction,
                    -absence.HalfPayAmount, "Labour Act [Chapter 28:01] s.14");
                taxableEarnings -= absence.HalfPayAmount;
                pensionableEarnings -= absence.HalfPayAmount;
                payslip.Gross -= absence.HalfPayAmount;
            }

            // ── NSSA. Deducted before PAYE, since the pension contribution is allowable. ──
            var nssa = await _statutory.CalculateNssaAsync(pensionableEarnings, asAt);
            payslip.NssaEmployee = nssa.EmployeeContribution;
            payslip.NssaEmployer = nssa.EmployerContribution;
            payslip.NssaAccidentPrevention = nssa.EmployerAccidentPrevention;
            payslip.NssaInsurableEarnings = nssa.InsurableEarnings;

            Line("NSSA pension (employee)", PayslipLineKind.StatutoryDeduction, -nssa.EmployeeContribution,
                "National Social Security Authority Act [Chapter 17:04]");

            // ── Other allowable deductions ──
            payslip.PensionContribution = structure.PensionContribution;
            payslip.MedicalAid = structure.MedicalAidContribution;

            Line("Occupational pension", PayslipLineKind.Deduction, -structure.PensionContribution);
            Line("Medical aid", PayslipLineKind.Deduction, -structure.MedicalAidContribution);

            // ── PAYE on what remains ──
            payslip.TaxableIncome = Math.Max(0,
                taxableEarnings - nssa.EmployeeContribution - structure.PensionContribution);

            var paye = await _statutory.CalculatePayeAsync(
                payslip.TaxableIncome, run.Currency, PayPeriod.Monthly, asAt);

            payslip.Paye = paye.Tax;
            payslip.AidsLevy = paye.AidsLevy;
            payslip.MarginalTaxRate = paye.MarginalRate;

            Line("PAYE", PayslipLineKind.StatutoryDeduction, -paye.Tax,
                $"Income Tax Act [Chapter 23:06] — marginal rate {paye.MarginalRate:0.##}%");
            Line("AIDS levy", PayslipLineKind.StatutoryDeduction, -paye.AidsLevy,
                "Charged on PAYE payable, not on gross pay");

            Line("Loan repayment", PayslipLineKind.Deduction, -payslip.LoanRepayments);
            Line("Other deductions", PayslipLineKind.Deduction, -payslip.OtherDeductions);

            // ── Employer-borne costs, shown for information ──
            payslip.Zimdef = Math.Round(payslip.Gross * levyRates.GetValueOrDefault(StatutoryKeys.ZimdefRate) / 100m, 2);
            payslip.StandardsLevy = Math.Round(
                payslip.Gross * levyRates.GetValueOrDefault(StatutoryKeys.StandardsDevelopmentLevyRate) / 100m, 2);

            Line("NSSA pension (employer)", PayslipLineKind.EmployerContribution, nssa.EmployerContribution);
            Line("NSSA accident prevention (employer)", PayslipLineKind.EmployerContribution,
                nssa.EmployerAccidentPrevention, "Assessed on the full wage, not the capped figure");
            Line("ZIMDEF levy (employer)", PayslipLineKind.EmployerContribution, payslip.Zimdef,
                "Manpower Planning and Development Act [Chapter 28:02]");
            Line("Standards development levy (employer)", PayslipLineKind.EmployerContribution, payslip.StandardsLevy);

            payslip.TotalDeductions = payslip.Paye + payslip.AidsLevy + payslip.NssaEmployee
                                    + payslip.PensionContribution + payslip.MedicalAid
                                    + payslip.LoanRepayments + payslip.OtherDeductions;

            payslip.Net = payslip.Gross - payslip.TotalDeductions;
            payslip.Lines = lines;

            return payslip;
        }

        /// <summary>
        /// What unpaid and half-pay absence in the period costs. Half-pay sick leave costs half a
        /// day's pay for each day taken; unpaid leave costs the whole day.
        /// </summary>
        private async Task<(decimal UnpaidDays, decimal UnpaidAmount, decimal HalfPayDays, decimal HalfPayAmount)>
            AbsenceDeductionAsync(SalaryStructure structure, DateTime periodStart, DateTime periodEnd, CancellationToken ct)
        {
            var leave = await _db.LeaveRequests.AsNoTracking()
                .Where(r => r.EmployeeId == structure.EmployeeId
                         && (r.Status == LeaveRequestStatus.Approved || r.Status == LeaveRequestStatus.Taken)
                         && r.StartDate <= periodEnd && r.EndDate >= periodStart
                         && (r.UnpaidDays > 0 || r.HalfPayDays > 0))
                .Select(r => new { r.UnpaidDays, r.HalfPayDays })
                .ToListAsync(ct);

            if (leave.Count == 0) return (0, 0, 0, 0);

            var unpaidDays = leave.Sum(l => l.UnpaidDays);
            var halfPayDays = leave.Sum(l => l.HalfPayDays);

            // 22 working days a month is the conventional divisor for a daily rate.
            var dailyRate = structure.BasicSalary / 22m;

            return (
                unpaidDays, Math.Round(unpaidDays * dailyRate, 2),
                halfPayDays, Math.Round(halfPayDays * dailyRate * 0.5m, 2));
        }

        // ── Run totals ───────────────────────────────────────────────────────────

        private async Task RollUpTotalsAsync(PayrollRun run, CancellationToken ct)
        {
            var totals = await _db.Payslips.AsNoTracking()
                .Where(p => p.PayrollRunId == run.Id)
                .GroupBy(p => 1)
                .Select(g => new
                {
                    Count = g.Count(),
                    Gross = g.Sum(p => p.Gross),
                    Paye = g.Sum(p => p.Paye),
                    Aids = g.Sum(p => p.AidsLevy),
                    NssaEmployee = g.Sum(p => p.NssaEmployee),
                    NssaEmployer = g.Sum(p => p.NssaEmployer + p.NssaAccidentPrevention),
                    Other = g.Sum(p => p.PensionContribution + p.MedicalAid + p.LoanRepayments + p.OtherDeductions),
                    Net = g.Sum(p => p.Net),
                    Zimdef = g.Sum(p => p.Zimdef),
                    Standards = g.Sum(p => p.StandardsLevy)
                })
                .FirstOrDefaultAsync(ct);

            run.EmployeeCount = totals?.Count ?? 0;
            run.TotalGross = totals?.Gross ?? 0;
            run.TotalPaye = totals?.Paye ?? 0;
            run.TotalAidsLevy = totals?.Aids ?? 0;
            run.TotalNssaEmployee = totals?.NssaEmployee ?? 0;
            run.TotalNssaEmployer = totals?.NssaEmployer ?? 0;
            run.TotalOtherDeductions = totals?.Other ?? 0;
            run.TotalNet = totals?.Net ?? 0;
            run.TotalZimdef = totals?.Zimdef ?? 0;
            run.TotalStandardsLevy = totals?.Standards ?? 0;
        }

        /// <summary>
        /// The monthly return figures — what has to be remitted, and to whom. PAYE and the AIDS
        /// levy go to ZIMRA, NSSA to the Authority, and the levies to their respective funds.
        /// </summary>
        public async Task<List<StatutoryReturn>> StatutoryReturnsAsync(int runId)
        {
            var run = await _db.PayrollRuns.AsNoTracking().FirstOrDefaultAsync(r => r.Id == runId);
            if (run == null) return new List<StatutoryReturn>();

            return new List<StatutoryReturn>
            {
                new("ZIMRA", "PAYE", run.TotalPaye, run.Currency,
                    "Income Tax Act [Chapter 23:06] — remitted with the monthly P2 return"),
                new("ZIMRA", "AIDS levy", run.TotalAidsLevy, run.Currency,
                    "Charged at the configured rate on PAYE payable"),
                new("NSSA", "Pension and other benefits — employee", run.TotalNssaEmployee, run.Currency,
                    "National Social Security Authority Act [Chapter 17:04]"),
                new("NSSA", "Pension, other benefits and accident prevention — employer",
                    run.TotalNssaEmployer, run.Currency,
                    "Employer share, plus the accident-prevention contribution"),
                new("ZIMDEF", "Manpower development levy", run.TotalZimdef, run.Currency,
                    "Manpower Planning and Development Act [Chapter 28:02] — employer"),
                new("Standards Development Fund", "Standards development levy",
                    run.TotalStandardsLevy, run.Currency, "Employer levy on gross wages")
            };
        }

        public record StatutoryReturn(string Payee, string Description, decimal Amount, string Currency, string Basis);
    }
}
