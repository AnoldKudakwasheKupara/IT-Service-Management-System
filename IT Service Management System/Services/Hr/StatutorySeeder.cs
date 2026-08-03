using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Helpers.Hr;
using IT_Service_Management_System.Models.Hr;
using Microsoft.EntityFrameworkCore;

namespace IT_Service_Management_System.Services.Hr
{
    /// <summary>
    /// Seeds the Zimbabwean statutory parameters and public holidays so the HR modules have
    /// something correct to start from.
    /// <para>
    /// <b>These are starting values, not gospel.</b> The leave, notice and retrenchment figures
    /// come from the Labour Act [Chapter 28:01] and are stable. The payroll figures — PAYE bands,
    /// the NSSA insurable-earnings ceiling, levy rates — change with each Finance Act and gazette
    /// notice, and the ceiling in particular is re-set whenever the currency moves. Every one of
    /// them carries the authority it came from so it can be checked, and all are editable in the
    /// application. Confirm them against the current instruments before running a live payroll.
    /// </para>
    /// <para>
    /// Seeding is additive and idempotent: a parameter that already exists is never overwritten,
    /// so a value corrected by the payroll administrator survives every restart.
    /// </para>
    /// </summary>
    public class StatutorySeeder
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<StatutorySeeder> _log;

        public StatutorySeeder(ApplicationDbContext db, ILogger<StatutorySeeder> log)
        {
            _db = db; _log = log;
        }

        public async Task SeedAsync(CancellationToken ct = default)
        {
            var added = await SeedParametersAsync(ct);
            var holidays = await SeedHolidaysAsync(ct);

            if (added > 0 || holidays > 0)
                _log.LogInformation(
                    "Zimbabwe statutory seed: {Parameters} parameter(s) and {Holidays} public holiday(s) added.",
                    added, holidays);
        }

        // ── Parameters ───────────────────────────────────────────────────────────

        private async Task<int> SeedParametersAsync(CancellationToken ct)
        {
            var existing = await _db.StatutoryParameters.Select(p => p.Key).ToListAsync(ct);
            var defaults = Defaults();
            var missing = defaults.Where(d => !existing.Contains(d.Key)).ToList();

            if (missing.Count == 0) return 0;

            _db.StatutoryParameters.AddRange(missing);
            await _db.SaveChangesAsync(ct);
            return missing.Count;
        }

        private static List<StatutoryParameter> Defaults()
        {
            // Dated at the start of the current year so a mid-year install does not look
            // retrospectively authoritative for periods nobody has checked.
            var from = new DateTime(DateTime.Today.Year, 1, 1);

            StatutoryParameter P(string key, string name, decimal value, StatutoryValueKind kind,
                string authority, string? notes = null, string? currency = null) =>
                new()
                {
                    Key = key,
                    Name = name,
                    Value = value,
                    Kind = kind,
                    Currency = currency,
                    EffectiveFrom = from,
                    Authority = authority,
                    Notes = notes
                };

            return new List<StatutoryParameter>
            {
                // ── Leave: Labour Act [Chapter 28:01]. Stable, and the ones to trust most. ──
                P(StatutoryKeys.VacationLeaveAccrualPerMonth, "Vacation leave accrued per month", 2.5m,
                    StatutoryValueKind.Days, "Labour Act [Chapter 28:01] s.14A",
                    "One twelfth of 30 days per month of service — 30 days a year. A statutory minimum; "
                    + "a contract or NEC agreement may give more."),

                P(StatutoryKeys.SickLeaveFullPayDays, "Sick leave on full pay", 90m,
                    StatutoryValueKind.Days, "Labour Act [Chapter 28:01] s.14",
                    "Ninety days on full pay in any twelve-month period, on production of a medical certificate."),

                P(StatutoryKeys.SickLeaveHalfPayDays, "Sick leave on half pay", 90m,
                    StatutoryValueKind.Days, "Labour Act [Chapter 28:01] s.14",
                    "A further ninety days at half pay once the full-pay entitlement is exhausted. "
                    + "After both, the employer may begin the incapacity process."),

                P(StatutoryKeys.MaternityLeaveDays, "Maternity leave", 98m,
                    StatutoryValueKind.Days, "Labour Act [Chapter 28:01] s.18",
                    "Ninety-eight days on full pay."),

                P(StatutoryKeys.MaternityQualifyingMonths, "Maternity qualifying service (months)", 12m,
                    StatutoryValueKind.Days, "Labour Act [Chapter 28:01] s.18",
                    "Minimum service before maternity leave may be taken. Confirm against the Act as "
                    + "amended — the qualifying conditions have been revised."),

                P(StatutoryKeys.PaternityLeaveDays, "Paternity leave", 14m,
                    StatutoryValueKind.Days, "Labour Amendment Act, 2023",
                    "Introduced by the 2023 amendment. Verify the entitlement and its conditions "
                    + "against the Act as amended before relying on this figure."),

                P(StatutoryKeys.SpecialLeaveDays, "Special leave on full pay", 12m,
                    StatutoryValueKind.Days, "Labour Act [Chapter 28:01] s.14B",
                    "Up to twelve days a year — bereavement, detention, or sitting an approved examination."),

                P(StatutoryKeys.MaxLeaveCarryOverDays, "Maximum leave carried over", 30m,
                    StatutoryValueKind.Days, "Employer policy",
                    "Not statutory. Set to whatever the contract or NEC agreement allows."),

                // ── Termination ──
                P(StatutoryKeys.RetrenchmentMonthsPerTwoYears, "Retrenchment months per two years of service", 1m,
                    StatutoryValueKind.Multiplier, "Labour Act [Chapter 28:01] s.12C",
                    "Minimum retrenchment package: one month's salary for every two years of service."),

                // ── NSSA. Rates are stable; the ceiling is not. ──
                P(StatutoryKeys.NssaPobsEmployeeRate, "NSSA pension — employee share", 4.5m,
                    StatutoryValueKind.Percentage, "National Social Security Authority Act [Chapter 17:04]",
                    "Of insurable earnings, up to the gazetted ceiling."),

                P(StatutoryKeys.NssaPobsEmployerRate, "NSSA pension — employer share", 4.5m,
                    StatutoryValueKind.Percentage, "National Social Security Authority Act [Chapter 17:04]",
                    "Of insurable earnings, up to the gazetted ceiling."),

                P(StatutoryKeys.NssaInsurableEarningsCeiling, "NSSA insurable earnings ceiling", 0m,
                    StatutoryValueKind.Amount, "NSSA gazette notice",
                    "MUST BE SET before running payroll. Re-gazetted whenever the currency moves; "
                    + "left at zero here because seeding a stale ceiling would silently under-deduct. "
                    + "Zero means no cap is applied.", "USD"),

                P(StatutoryKeys.NssaApwcsEmployerRate, "NSSA accident prevention — employer", 1m,
                    StatutoryValueKind.Percentage, "Accident Prevention and Workers Compensation Scheme",
                    "Employer only, and rated by industry risk class — confirm the rate assessed for "
                    + "this employer rather than using this placeholder."),

                // ── Tax and levies. Change with every Finance Act. ──
                P(StatutoryKeys.AidsLevyRate, "AIDS levy", 3m,
                    StatutoryValueKind.Percentage, "Income Tax Act [Chapter 23:06]",
                    "Charged on the PAYE payable, not on gross pay."),

                P(StatutoryKeys.ZimdefRate, "ZIMDEF manpower development levy", 1m,
                    StatutoryValueKind.Percentage, "Manpower Planning and Development Act [Chapter 28:02]",
                    "Employer levy on the gross wage bill."),

                P(StatutoryKeys.StandardsDevelopmentLevyRate, "Standards development levy", 0.5m,
                    StatutoryValueKind.Percentage, "Standards Development Fund",
                    "Employer levy on gross wages."),

                // ── Working time. Set by NEC agreement, not by the Act. ──
                P(StatutoryKeys.StandardHoursPerWeek, "Standard hours per week", 44m,
                    StatutoryValueKind.Days, "NEC collective bargaining agreement",
                    "Not fixed by the Labour Act — set by the National Employment Council for the "
                    + "sector. Adjust to the agreement that binds this employer."),

                P(StatutoryKeys.StandardHoursPerDay, "Standard hours per day", 8m,
                    StatutoryValueKind.Days, "NEC collective bargaining agreement"),

                P(StatutoryKeys.OvertimeMultiplier, "Overtime rate", 1.5m,
                    StatutoryValueKind.Multiplier, "NEC collective bargaining agreement",
                    "Typical rate. Confirm against the applicable agreement."),

                P(StatutoryKeys.RestDayMultiplier, "Rest-day rate", 2m,
                    StatutoryValueKind.Multiplier, "NEC collective bargaining agreement"),

                P(StatutoryKeys.PublicHolidayMultiplier, "Public holiday rate", 2m,
                    StatutoryValueKind.Multiplier, "NEC collective bargaining agreement")
            };
        }

        // ── Public holidays ──────────────────────────────────────────────────────

        /// <summary>
        /// Seed this year's and next year's holidays. Two years so leave requested in December for
        /// January is costed correctly, which a single-year seed gets wrong every December.
        /// </summary>
        private async Task<int> SeedHolidaysAsync(CancellationToken ct)
        {
            var years = new[] { DateTime.Today.Year, DateTime.Today.Year + 1 };
            var added = 0;

            foreach (var year in years)
            {
                var from = new DateTime(year, 1, 1);
                var to = new DateTime(year, 12, 31);

                var existing = await _db.PublicHolidays
                    .Where(h => h.Date >= from && h.Date <= to)
                    .Select(h => h.Date)
                    .ToListAsync(ct);

                var missing = ZimbabweLabourLaw.StandardHolidays(year)
                    .Where(h => !existing.Contains(h.Date))
                    .ToList();

                if (missing.Count == 0) continue;

                _db.PublicHolidays.AddRange(missing);
                added += missing.Count;
            }

            if (added > 0) await _db.SaveChangesAsync(ct);
            return added;
        }
    }
}
