using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Models.Hr;
using Microsoft.EntityFrameworkCore;

namespace IT_Service_Management_System.Services.Hr
{
    /// <summary>
    /// Benefits: what each member costs, and how each benefit lands for tax.
    /// <para>
    /// The tax treatment is the whole point of computing this separately from the cost. A benefit in
    /// kind adds to taxable income; a contribution to an approved fund comes off before PAYE; a
    /// medical aid contribution attracts a credit against the tax itself rather than a deduction from
    /// income. Three different arithmetic paths, and getting them the wrong way round produces a
    /// payslip that looks right and is not.
    /// </para>
    /// <para>
    /// The service reports; it does not post to payroll. Benefits are agreed and change mid-month,
    /// and a run should pick them up deliberately rather than have them appear underneath it.
    /// </para>
    /// </summary>
    public class BenefitsService
    {
        private readonly ApplicationDbContext _db;
        private readonly StatutoryService _statutory;

        public BenefitsService(ApplicationDbContext db, StatutoryService statutory)
        {
            _db = db; _statutory = statutory;
        }

        /// <summary>What one enrolment costs and how it is treated, for a given month.</summary>
        public record MemberCost(
            int EnrolmentId,
            int EmployeeId,
            string EmployeeName,
            string PlanName,
            BenefitCategory Category,
            BenefitTaxTreatment TaxTreatment,
            string Currency,
            decimal EmployerContribution,
            decimal EmployeeContribution,
            int Dependants,
            decimal DependantCost)
    {
        public decimal Total => EmployerContribution + EmployeeContribution + DependantCost;
    }

        /// <summary>
        /// The cost of an employee's benefits as at a date, resolving percentage plans against the
        /// salary structure in force then. A benefit priced off a salary has to be priced off the
        /// salary that actually applied, not today's.
        /// </summary>
        public async Task<List<MemberCost>> EmployeeCostsAsync(int employeeId, DateTime? asAt = null)
        {
            var date = (asAt ?? DateTime.Today).Date;

            var enrolments = await _db.BenefitEnrolments.AsNoTracking()
                .Include(e => e.Plan).Include(e => e.Employee).Include(e => e.Dependants)
                .Where(e => e.EmployeeId == employeeId
                         && e.StartDate <= date
                         && (e.EndDate == null || e.EndDate >= date))
                .ToListAsync();

            if (enrolments.Count == 0) return new List<MemberCost>();

            var basic = await BasicSalaryAsync(employeeId, date);
            return enrolments.Select(e => Cost(e, basic)).ToList();
        }

        private async Task<decimal> BasicSalaryAsync(int employeeId, DateTime date)
        {
            return await _db.SalaryStructures.AsNoTracking()
                .Where(s => s.EmployeeId == employeeId
                         && s.EffectiveFrom <= date
                         && (s.EffectiveTo == null || s.EffectiveTo >= date))
                .OrderByDescending(s => s.EffectiveFrom)
                .Select(s => s.BasicSalary)
                .FirstOrDefaultAsync();
        }

        private static MemberCost Cost(BenefitEnrolment e, decimal basic)
        {
            var plan = e.Plan!;

            decimal employer, employee;

            if (plan.Basis == ContributionBasis.PercentOfBasic)
            {
                employer = Math.Round(basic * plan.EmployerRate / 100m, 2);
                employee = Math.Round(basic * plan.EmployeeRate / 100m, 2);
            }
            else
            {
                employer = plan.EmployerAmount;
                employee = plan.EmployeeAmount;
            }

            // A member's own terms override the plan's, where they were agreed differently.
            employer = e.EmployerAmountOverride ?? employer;
            employee = e.EmployeeAmountOverride ?? employee;

            var dependants = e.Dependants.Count(d => d.IsActive);

            return new MemberCost(
                e.Id, e.EmployeeId,
                e.Employee?.DisplayName ?? "",
                plan.Name, plan.Category, plan.TaxTreatment, plan.Currency,
                employer, employee, dependants,
                Math.Round(dependants * plan.CostPerDependant, 2));
        }

        /// <summary>
        /// How an employee's benefits affect their pay for a month, split by tax treatment.
        /// <para>
        /// This is what payroll needs and is deliberately returned rather than written: taxable
        /// benefits to add to gross, approved-fund contributions to deduct before PAYE, medical aid
        /// contributions on which a credit is claimed, and the employer's own cost, which affects
        /// what the benefit costs the business but not what the employee is taxed on.
        /// </para>
        /// </summary>
        public record PayrollEffect(
            decimal TaxableBenefitValue,
            decimal DeductibleContributions,
            decimal MedicalAidContributions,
            decimal MedicalAidCredit,
            decimal OtherEmployeeDeductions,
            decimal EmployerCost,
            string Currency,
            List<string> Notes);

        public async Task<PayrollEffect> PayrollEffectAsync(int employeeId, DateTime asAt)
        {
            var costs = await EmployeeCostsAsync(employeeId, asAt);
            var notes = new List<string>();

            decimal taxable = 0, deductible = 0, medical = 0, other = 0, employerCost = 0;
            var currency = costs.FirstOrDefault()?.Currency ?? "USD";

            foreach (var c in costs)
            {
                employerCost += c.EmployerContribution;

                switch (c.TaxTreatment)
                {
                    case BenefitTaxTreatment.TaxableBenefit:
                        // The employer's spend is the value in the employee's hands.
                        taxable += c.EmployerContribution + c.DependantCost;
                        other += c.EmployeeContribution;
                        break;

                    case BenefitTaxTreatment.DeductibleContribution:
                        deductible += c.EmployeeContribution;
                        break;

                    case BenefitTaxTreatment.MedicalAidCredit:
                        medical += c.EmployeeContribution + c.DependantCost;
                        break;

                    default:
                        other += c.EmployeeContribution;
                        break;
                }

                if (costs.Count(x => x.Currency != currency) > 0)
                    notes.Add($"{c.PlanName} is priced in {c.Currency}, which is not the currency of the "
                            + "other benefits. Convert before using these figures in a run.");
            }

            // The medical aid credit is a proportion of the contribution, set as a statutory
            // parameter so it can be corrected without a deployment.
            var creditRate = await _statutory.RateAsync("Tax.MedicalAidCreditRate", asAt);
            var credit = Math.Round(medical * creditRate, 2);

            if (medical > 0 && creditRate == 0)
                notes.Add("Medical aid contributions are recorded, but the medical aid tax credit rate "
                        + "is not configured. Until it is, no credit will be applied and PAYE will be "
                        + "overstated. Set Tax.MedicalAidCreditRate.");

            if (deductible > 0)
                notes.Add("Contributions to an approved fund are deductible within the statutory limit. "
                        + "Check the aggregate against that limit before the run — the module does not "
                        + "cap it, because the limit applies across all of an employee's funds, not "
                        + "just the ones recorded here.");

            return new PayrollEffect(
                Math.Round(taxable, 2), Math.Round(deductible, 2), Math.Round(medical, 2),
                credit, Math.Round(other, 2), Math.Round(employerCost, 2), currency,
                notes.Distinct().ToList());
        }

        public record PlanSummary(BenefitPlan Plan, int Members, int Dependants, decimal MonthlyEmployerCost);

        /// <summary>What each plan costs the employer a month, and how many people are on it.</summary>
        public async Task<List<PlanSummary>> PlanSummariesAsync(DateTime? asAt = null)
        {
            var date = (asAt ?? DateTime.Today).Date;

            var plans = await _db.BenefitPlans.AsNoTracking()
                .OrderBy(p => p.Category).ThenBy(p => p.Name).ToListAsync();

            var enrolments = await _db.BenefitEnrolments.AsNoTracking()
                .Include(e => e.Dependants)
                .Where(e => e.StartDate <= date && (e.EndDate == null || e.EndDate >= date))
                .ToListAsync();

            var salaries = await _db.SalaryStructures.AsNoTracking()
                .Where(s => s.EffectiveFrom <= date && (s.EffectiveTo == null || s.EffectiveTo >= date))
                .GroupBy(s => s.EmployeeId)
                .Select(g => new { EmployeeId = g.Key, Basic = g.OrderByDescending(s => s.EffectiveFrom).First().BasicSalary })
                .ToDictionaryAsync(x => x.EmployeeId, x => x.Basic);

            var summaries = new List<PlanSummary>();

            foreach (var plan in plans)
            {
                var mine = enrolments.Where(e => e.PlanId == plan.Id).ToList();
                decimal cost = 0;

                foreach (var e in mine)
                {
                    var basic = salaries.GetValueOrDefault(e.EmployeeId);
                    var employer = e.EmployerAmountOverride
                        ?? (plan.Basis == ContributionBasis.PercentOfBasic
                            ? Math.Round(basic * plan.EmployerRate / 100m, 2)
                            : plan.EmployerAmount);

                    cost += employer + e.Dependants.Count(d => d.IsActive) * plan.CostPerDependant;
                }

                summaries.Add(new PlanSummary(plan, mine.Count,
                    mine.Sum(e => e.Dependants.Count(d => d.IsActive)), Math.Round(cost, 2)));
            }

            return summaries;
        }

        public record EnrolResult(bool Succeeded, string Message, int? EnrolmentId = null);

        /// <summary>
        /// Enrol an employee, checking the qualifying period and the employment type the plan is
        /// open to. Both are refused rather than warned about — enrolling someone who does not
        /// qualify creates an expectation that has to be taken away again.
        /// </summary>
        public async Task<EnrolResult> EnrolAsync(int employeeId, int planId, DateTime startDate,
            string? membershipNumber)
        {
            var plan = await _db.BenefitPlans.FirstOrDefaultAsync(p => p.Id == planId);
            if (plan == null) return new EnrolResult(false, "Plan not found.");

            var employee = await _db.Employees.FirstOrDefaultAsync(e => e.Id == employeeId);
            if (employee == null) return new EnrolResult(false, "Employee not found.");

            if (!plan.IsCurrent)
                return new EnrolResult(false, $"{plan.Name} is not currently in force.");

            if (plan.AvailableTo.HasValue && plan.AvailableTo != employee.EmploymentType)
                return new EnrolResult(false,
                    $"{plan.Name} is open to {plan.AvailableTo} employees only, and "
                    + $"{employee.DisplayName} is {employee.EmploymentType}.");

            if (plan.QualifyingMonths > 0)
            {
                var months = employee.HireDate.HasValue
                    ? (startDate - employee.HireDate.Value).TotalDays / 30.44
                    : 0;

                if (months < plan.QualifyingMonths)
                    return new EnrolResult(false,
                        $"{plan.Name} requires {plan.QualifyingMonths} month(s) of service. "
                        + $"{employee.DisplayName} will qualify from "
                        + $"{employee.HireDate?.AddMonths(plan.QualifyingMonths):d MMM yyyy}.");
            }

            var overlapping = await _db.BenefitEnrolments
                .AnyAsync(e => e.EmployeeId == employeeId && e.PlanId == planId
                            && (e.EndDate == null || e.EndDate >= startDate));

            if (overlapping)
                return new EnrolResult(false, $"{employee.DisplayName} is already on {plan.Name}.");

            var enrolment = new BenefitEnrolment
            {
                EmployeeId = employeeId,
                PlanId = planId,
                StartDate = startDate,
                MembershipNumber = membershipNumber
            };

            _db.BenefitEnrolments.Add(enrolment);
            await _db.SaveChangesAsync();

            return new EnrolResult(true,
                $"{employee.DisplayName} enrolled on {plan.Name} from {startDate:d MMM yyyy}.",
                enrolment.Id);
        }
    }
}
