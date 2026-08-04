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
            var leaveTypes = await SeedLeaveTypesAsync(ct);
            var offences = await SeedDisciplinaryOffencesAsync(ct);

            if (added > 0 || holidays > 0 || leaveTypes > 0 || offences > 0)
                _log.LogInformation(
                    "Zimbabwe statutory seed: {Parameters} parameter(s), {Holidays} public holiday(s), "
                    + "{LeaveTypes} leave type(s) and {Offences} disciplinary offence(s) added.",
                    added, holidays, leaveTypes, offences);
        }

        // ── Disciplinary offences ────────────────────────────────────────────────

        /// <summary>
        /// The acts of misconduct in the National Employment Code of Conduct, Statutory Instrument
        /// 15 of 2006 — which applies wherever an employer has no registered code of its own.
        /// <para>
        /// The first five are the offences the model code treats as justifying dismissal even on a
        /// first occasion. The rest run through progressive discipline. An employer with a code
        /// registered through its NEC or works council should replace these with that code's list.
        /// </para>
        /// </summary>
        private async Task<int> SeedDisciplinaryOffencesAsync(CancellationToken ct)
        {
            var existing = await _db.DisciplinaryOffences.Select(o => o.Code).ToListAsync(ct);
            const string modelCode = "National Employment Code of Conduct, SI 15 of 2006";

            var defaults = new List<DisciplinaryOffence>
            {
                // ── Dismissable on a first offence under the model code ──
                new()
                {
                    Code = "THEFT", Name = "Theft or fraud", DisplayOrder = 1,
                    Seriousness = OffenceSeriousness.Gross, Authority = modelCode,
                    Description = "Any act of theft or fraud against the employer, a fellow employee, "
                                + "a client or a supplier.",
                    DismissableFirstOffence = true,
                    DefaultFirstPenalty = DisciplinaryPenalty.SummaryDismissal
                },
                new()
                {
                    Code = "WILFULDAMAGE", Name = "Wilful damage to property", DisplayOrder = 2,
                    Seriousness = OffenceSeriousness.Gross, Authority = modelCode,
                    Description = "Deliberate damage to the employer's property.",
                    DismissableFirstOffence = true,
                    DefaultFirstPenalty = DisciplinaryPenalty.SummaryDismissal
                },
                new()
                {
                    Code = "INSUBORDINATION", Name = "Gross insubordination", DisplayOrder = 3,
                    Seriousness = OffenceSeriousness.Gross, Authority = modelCode,
                    Description = "Wilful disobedience of a lawful and reasonable instruction.",
                    DismissableFirstOffence = true,
                    DefaultFirstPenalty = DisciplinaryPenalty.SummaryDismissal
                },
                new()
                {
                    Code = "AWOL5", Name = "Absence without leave for five or more days", DisplayOrder = 4,
                    Seriousness = OffenceSeriousness.Gross, Authority = modelCode,
                    Description = "Absence from work for a period of five or more continuous working "
                                + "days without leave or a reasonable excuse.",
                    DismissableFirstOffence = true,
                    DefaultFirstPenalty = DisciplinaryPenalty.SummaryDismissal
                },
                new()
                {
                    Code = "INCOMPETENCE", Name = "Gross incompetence or inefficiency", DisplayOrder = 5,
                    Seriousness = OffenceSeriousness.Gross, Authority = modelCode,
                    Description = "Habitual and substantial neglect of duties, or a lack of skill the "
                                + "employee expressly or impliedly warranted they possessed.",
                    DismissableFirstOffence = true,
                    DefaultFirstPenalty = DisciplinaryPenalty.SummaryDismissal
                },

                // ── Progressive discipline ──
                new()
                {
                    Code = "LATE", Name = "Persistent lateness", DisplayOrder = 10,
                    Seriousness = OffenceSeriousness.Minor, Authority = modelCode,
                    Description = "Repeated failure to report for duty at the appointed time.",
                    DefaultFirstPenalty = DisciplinaryPenalty.VerbalWarning,
                    WarningValidityMonths = 6
                },
                new()
                {
                    Code = "AWOL", Name = "Absence without leave", DisplayOrder = 11,
                    Seriousness = OffenceSeriousness.Serious, Authority = modelCode,
                    Description = "Absence without leave or a reasonable excuse, for fewer than five "
                                + "continuous working days.",
                    DefaultFirstPenalty = DisciplinaryPenalty.WrittenWarning
                },
                new()
                {
                    Code = "NEGLIGENCE", Name = "Negligence in the performance of duties", DisplayOrder = 12,
                    Seriousness = OffenceSeriousness.Serious, Authority = modelCode,
                    Description = "Careless performance of duties causing, or risking, loss or damage.",
                    DefaultFirstPenalty = DisciplinaryPenalty.WrittenWarning
                },
                new()
                {
                    Code = "INSOLENCE", Name = "Insolence or abusive language", DisplayOrder = 13,
                    Seriousness = OffenceSeriousness.Serious, Authority = modelCode,
                    Description = "Abusive, threatening or insulting language or behaviour towards a "
                                + "supervisor, a fellow employee or a client.",
                    DefaultFirstPenalty = DisciplinaryPenalty.WrittenWarning
                },
                new()
                {
                    Code = "INTOXICATION", Name = "Reporting for duty under the influence", DisplayOrder = 14,
                    Seriousness = OffenceSeriousness.Serious, Authority = modelCode,
                    Description = "Reporting for or remaining on duty while under the influence of "
                                + "alcohol or drugs.",
                    DefaultFirstPenalty = DisciplinaryPenalty.FinalWritten
                },
                new()
                {
                    Code = "SAFETY", Name = "Breach of health and safety rules", DisplayOrder = 15,
                    Seriousness = OffenceSeriousness.Serious, Authority = modelCode,
                    Description = "Failure to observe safety rules, or to use protective equipment "
                                + "provided.",
                    DefaultFirstPenalty = DisciplinaryPenalty.WrittenWarning
                },
                new()
                {
                    Code = "CONFIDENTIALITY", Name = "Breach of confidentiality", DisplayOrder = 16,
                    Seriousness = OffenceSeriousness.Serious, Authority = "Employer code of conduct",
                    Description = "Unauthorised disclosure of confidential information belonging to "
                                + "the employer, a client or a fellow employee.",
                    DefaultFirstPenalty = DisciplinaryPenalty.FinalWritten
                },
                new()
                {
                    Code = "MISUSE", Name = "Unauthorised use of employer property", DisplayOrder = 17,
                    Seriousness = OffenceSeriousness.Serious, Authority = modelCode,
                    Description = "Using the employer's property, vehicles or systems for private "
                                + "purposes without authority.",
                    DefaultFirstPenalty = DisciplinaryPenalty.WrittenWarning
                },
                new()
                {
                    Code = "HARASSMENT", Name = "Harassment or discrimination", DisplayOrder = 18,
                    Seriousness = OffenceSeriousness.Gross, Authority = "Labour Act [Chapter 28:01] s.8",
                    Description = "Conduct amounting to harassment, including sexual harassment, or "
                                + "discrimination on a prohibited ground.",
                    DismissableFirstOffence = true,
                    DefaultFirstPenalty = DisciplinaryPenalty.SummaryDismissal
                }
            };

            var missing = defaults.Where(d => !existing.Contains(d.Code)).ToList();
            if (missing.Count == 0) return 0;

            _db.DisciplinaryOffences.AddRange(missing);
            await _db.SaveChangesAsync(ct);
            return missing.Count;
        }

        // ── Leave types ──────────────────────────────────────────────────────────

        /// <summary>
        /// The leave categories the Labour Act provides for. Added only if the code is not already
        /// present, so an employer that has tuned an entitlement upwards keeps its version.
        /// </summary>
        private async Task<int> SeedLeaveTypesAsync(CancellationToken ct)
        {
            var existing = await _db.LeaveTypes.Select(t => t.Code).ToListAsync(ct);

            var defaults = new List<LeaveType>
            {
                new()
                {
                    Code = "VACATION", Name = "Vacation leave", DisplayOrder = 1, Colour = "#2c9e6e",
                    Authority = "Labour Act [Chapter 28:01] s.14A",
                    Description = "Accrues at one twelfth of 30 days for each month of service — "
                                + "30 days a year. A statutory minimum; a contract or NEC agreement may give more.",
                    AccrualPerMonth = 2.5m, AnnualEntitlementDays = 30m,
                    IsPaid = true, CountsWorkingDaysOnly = true,
                    MaxCarryOverDays = 30m, NoticeDaysRequired = 14,
                    PaidOutOnTermination = true
                },
                new()
                {
                    Code = "SICK", Name = "Sick leave", DisplayOrder = 2, Colour = "#e53e3e",
                    Authority = "Labour Act [Chapter 28:01] s.14",
                    Description = "Ninety days on full pay in any twelve-month period, then a further "
                                + "ninety at half pay. Once both are exhausted the incapacity "
                                + "provisions of s.14 may apply.",
                    AnnualEntitlementDays = 90m, HasHalfPayTier = true, HalfPayDays = 90,
                    IsPaid = true, CountsWorkingDaysOnly = true,
                    RequiresMedicalCertificate = true, CertificateRequiredAfterDays = 1,
                    PaidOutOnTermination = false
                },
                new()
                {
                    Code = "MATERNITY", Name = "Maternity leave", DisplayOrder = 3, Colour = "#8b5cf6",
                    Authority = "Labour Act [Chapter 28:01] s.18",
                    Description = "Ninety-eight days on full pay. Counted in calendar days.",
                    AnnualEntitlementDays = 98m, QualifyingMonths = 12,
                    IsPaid = true, CountsWorkingDaysOnly = false,
                    RestrictedToGender = "Female",
                    RequiresMedicalCertificate = true, CertificateRequiredAfterDays = 1,
                    NoticeDaysRequired = 30, PaidOutOnTermination = false
                },
                new()
                {
                    Code = "PATERNITY", Name = "Paternity leave", DisplayOrder = 4, Colour = "#3b6ea5",
                    Authority = "Labour Amendment Act, 2023",
                    Description = "Introduced by the 2023 amendment. Confirm the entitlement and its "
                                + "conditions against the Act as amended.",
                    AnnualEntitlementDays = 14m,
                    IsPaid = true, CountsWorkingDaysOnly = false,
                    RestrictedToGender = "Male",
                    NoticeDaysRequired = 14, PaidOutOnTermination = false
                },
                new()
                {
                    Code = "SPECIAL", Name = "Special leave", DisplayOrder = 5, Colour = "#f5b042",
                    Authority = "Labour Act [Chapter 28:01] s.14B",
                    Description = "Up to twelve days a year on full pay — bereavement, detention or "
                                + "remand, or sitting an approved examination.",
                    AnnualEntitlementDays = 12m,
                    IsPaid = true, CountsWorkingDaysOnly = true,
                    PaidOutOnTermination = false
                },
                new()
                {
                    Code = "UNPAID", Name = "Unpaid leave", DisplayOrder = 6, Colour = "#64748b",
                    Authority = "Employer discretion",
                    Description = "Granted at the employer's discretion. Not statutory, and unpaid.",
                    IsPaid = false, CountsWorkingDaysOnly = true,
                    NoticeDaysRequired = 30, PaidOutOnTermination = false
                },
                new()
                {
                    Code = "COMPASSIONATE", Name = "Compassionate leave", DisplayOrder = 7, Colour = "#0d9488",
                    Authority = "Employer policy",
                    Description = "Employer policy rather than statute. Bereavement may alternatively "
                                + "be taken as special leave under s.14B.",
                    AnnualEntitlementDays = 5m,
                    IsPaid = true, CountsWorkingDaysOnly = true,
                    PaidOutOnTermination = false
                }
            };

            var missing = defaults.Where(d => !existing.Contains(d.Code)).ToList();
            if (missing.Count == 0) return 0;

            _db.LeaveTypes.AddRange(missing);
            await _db.SaveChangesAsync(ct);
            return missing.Count;
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

                // Seeded at zero deliberately. A medical aid credit reduces the tax payable, so a
                // guessed rate under-deducts PAYE silently. Nothing is applied until the rate in
                // force is entered, and the benefits module says loudly that it is not configured.
                P(StatutoryKeys.MedicalAidCreditRate, "Medical aid contribution tax credit", 0m,
                    StatutoryValueKind.Percentage, "Income Tax Act [Chapter 23:06]",
                    "The proportion of a medical aid contribution allowed as a credit against tax "
                    + "payable — a credit against the tax, not a deduction from income. Seeded at "
                    + "zero: enter the rate in force before relying on it, because a guessed rate "
                    + "under-deducts PAYE without anyone noticing."),

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
