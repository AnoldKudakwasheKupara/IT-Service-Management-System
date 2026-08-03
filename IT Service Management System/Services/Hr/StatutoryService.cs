using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Models.Hr;
using Microsoft.EntityFrameworkCore;

namespace IT_Service_Management_System.Services.Hr
{
    /// <summary>
    /// Resolves statutory parameters and PAYE bands as at a given date.
    /// <para>
    /// Every lookup takes a date rather than reading "the current value", because payroll is
    /// frequently rerun for a past period — a correction, a back-dated increase, an audit query —
    /// and it must produce the figures that applied <em>then</em>, not the ones that apply now.
    /// </para>
    /// </summary>
    public class StatutoryService
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<StatutoryService> _log;

        public StatutoryService(ApplicationDbContext db, ILogger<StatutoryService> log)
        {
            _db = db; _log = log;
        }

        /// <summary>
        /// The value of a parameter as at a date. Returns <paramref name="fallback"/> when nothing
        /// is configured, and logs it — a missing statutory rate is a configuration problem someone
        /// needs to see, not something to fail a payroll run over.
        /// </summary>
        public async Task<decimal> ValueAsync(string key, DateTime asAt, decimal fallback = 0m)
        {
            var value = await _db.StatutoryParameters.AsNoTracking()
                .Where(p => p.Key == key
                         && p.EffectiveFrom <= asAt
                         && (p.EffectiveTo == null || p.EffectiveTo >= asAt))
                .OrderByDescending(p => p.EffectiveFrom)
                .Select(p => (decimal?)p.Value)
                .FirstOrDefaultAsync();

            if (value.HasValue) return value.Value;

            _log.LogWarning("Statutory parameter {Key} has no value effective at {AsAt:d}; using {Fallback}.",
                key, asAt, fallback);
            return fallback;
        }

        /// <summary>Load several parameters in one round trip, for a payroll run over many employees.</summary>
        public async Task<Dictionary<string, decimal>> ValuesAsync(IEnumerable<string> keys, DateTime asAt)
        {
            var wanted = keys.Distinct().ToList();

            var rows = await _db.StatutoryParameters.AsNoTracking()
                .Where(p => wanted.Contains(p.Key)
                         && p.EffectiveFrom <= asAt
                         && (p.EffectiveTo == null || p.EffectiveTo >= asAt))
                .OrderByDescending(p => p.EffectiveFrom)
                .Select(p => new { p.Key, p.Value })
                .ToListAsync();

            // Most recent effective row wins where several overlap.
            return rows.GroupBy(r => r.Key).ToDictionary(g => g.Key, g => g.First().Value);
        }

        /// <summary>A rate stored as a percentage, returned as a fraction — 4.5 becomes 0.045.</summary>
        public async Task<decimal> RateAsync(string key, DateTime asAt, decimal fallbackPercent = 0m) =>
            await ValueAsync(key, asAt, fallbackPercent) / 100m;

        // ── PAYE ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// PAYE on taxable income for a period, plus the AIDS levy which is charged on the tax
        /// payable rather than on income.
        /// </summary>
        public async Task<PayeResult> CalculatePayeAsync(decimal taxableIncome, string currency,
            PayPeriod period, DateTime asAt)
        {
            if (taxableIncome <= 0) return new PayeResult();

            var bands = await _db.PayeTaxBands.AsNoTracking()
                .Where(b => b.Currency == currency && b.Period == period
                         && b.EffectiveFrom <= asAt
                         && (b.EffectiveTo == null || b.EffectiveTo >= asAt))
                .OrderBy(b => b.FromAmount)
                .ToListAsync();

            if (bands.Count == 0)
            {
                _log.LogWarning("No PAYE bands configured for {Currency} {Period} at {AsAt:d}; PAYE not deducted.",
                    currency, period, asAt);
                return new PayeResult { BandsMissing = true };
            }

            // ZIMRA publishes the tables as "multiply by the rate, then subtract the deduction",
            // so the matched band gives the answer directly rather than needing a cumulative walk.
            var band = bands.LastOrDefault(b => taxableIncome > b.FromAmount
                                             && (b.ToAmount == null || taxableIncome <= b.ToAmount))
                       ?? bands.Last();

            var tax = Math.Max(0, taxableIncome * band.Rate / 100m - band.Deduction);
            var aidsLevyRate = await RateAsync(StatutoryKeys.AidsLevyRate, asAt, 3m);
            var aidsLevy = Math.Round(tax * aidsLevyRate, 2);

            return new PayeResult
            {
                Tax = Math.Round(tax, 2),
                AidsLevy = aidsLevy,
                MarginalRate = band.Rate,
                BandFrom = band.FromAmount,
                BandTo = band.ToAmount
            };
        }

        public record PayeResult
        {
            public decimal Tax { get; init; }
            public decimal AidsLevy { get; init; }
            public decimal MarginalRate { get; init; }
            public decimal BandFrom { get; init; }
            public decimal? BandTo { get; init; }
            public bool BandsMissing { get; init; }

            /// <summary>PAYE and the AIDS levy together — what actually leaves the employee's pay.</summary>
            public decimal Total => Tax + AidsLevy;
        }

        // ── NSSA ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// NSSA contributions on a month's earnings. POBS is split between employee and employer
        /// and is capped at the gazetted insurable-earnings ceiling; the accident-prevention
        /// contribution is the employer's alone and is uncapped.
        /// </summary>
        public async Task<NssaResult> CalculateNssaAsync(decimal grossEarnings, DateTime asAt)
        {
            var values = await ValuesAsync(new[]
            {
                StatutoryKeys.NssaPobsEmployeeRate,
                StatutoryKeys.NssaPobsEmployerRate,
                StatutoryKeys.NssaInsurableEarningsCeiling,
                StatutoryKeys.NssaApwcsEmployerRate
            }, asAt);

            var ceiling = values.GetValueOrDefault(StatutoryKeys.NssaInsurableEarningsCeiling);
            var insurable = ceiling > 0 ? Math.Min(grossEarnings, ceiling) : grossEarnings;

            var employeeRate = values.GetValueOrDefault(StatutoryKeys.NssaPobsEmployeeRate) / 100m;
            var employerRate = values.GetValueOrDefault(StatutoryKeys.NssaPobsEmployerRate) / 100m;
            var apwcsRate = values.GetValueOrDefault(StatutoryKeys.NssaApwcsEmployerRate) / 100m;

            return new NssaResult
            {
                InsurableEarnings = Math.Round(insurable, 2),
                Ceiling = ceiling,
                EmployeeContribution = Math.Round(insurable * employeeRate, 2),
                EmployerContribution = Math.Round(insurable * employerRate, 2),
                // Accident prevention is assessed on the actual wage bill, not the capped figure.
                EmployerAccidentPrevention = Math.Round(grossEarnings * apwcsRate, 2)
            };
        }

        public record NssaResult
        {
            public decimal InsurableEarnings { get; init; }
            public decimal Ceiling { get; init; }
            public decimal EmployeeContribution { get; init; }
            public decimal EmployerContribution { get; init; }
            public decimal EmployerAccidentPrevention { get; init; }

            public decimal TotalEmployerCost => EmployerContribution + EmployerAccidentPrevention;
        }

        // ── Employer levies ──────────────────────────────────────────────────────

        /// <summary>
        /// The levies charged on the employer's wage bill rather than deducted from the employee —
        /// manpower development and standards development.
        /// </summary>
        public async Task<(decimal Zimdef, decimal StandardsDevelopment)> CalculateEmployerLeviesAsync(
            decimal grossWageBill, DateTime asAt)
        {
            var values = await ValuesAsync(
                new[] { StatutoryKeys.ZimdefRate, StatutoryKeys.StandardsDevelopmentLevyRate }, asAt);

            return (
                Math.Round(grossWageBill * values.GetValueOrDefault(StatutoryKeys.ZimdefRate) / 100m, 2),
                Math.Round(grossWageBill * values.GetValueOrDefault(StatutoryKeys.StandardsDevelopmentLevyRate) / 100m, 2)
            );
        }

        // ── Public holidays ──────────────────────────────────────────────────────

        public async Task<HashSet<DateTime>> PublicHolidaysAsync(DateTime from, DateTime to) =>
            (await _db.PublicHolidays.AsNoTracking()
                .Where(h => h.Date >= from.Date && h.Date <= to.Date)
                .Select(h => h.Date)
                .ToListAsync())
            .Select(d => d.Date)
            .ToHashSet();

        /// <summary>
        /// Working days between two dates inclusive, excluding weekends and public holidays.
        /// Leave is deducted in working days, so this is what most of the leave module runs on.
        /// </summary>
        public async Task<int> WorkingDaysBetweenAsync(DateTime from, DateTime to)
        {
            if (to < from) return 0;

            var holidays = await PublicHolidaysAsync(from, to);
            var days = 0;

            for (var day = from.Date; day <= to.Date; day = day.AddDays(1))
            {
                if (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) continue;
                if (holidays.Contains(day)) continue;
                days++;
            }
            return days;
        }
    }
}
