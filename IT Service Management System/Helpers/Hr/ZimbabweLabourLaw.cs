using IT_Service_Management_System.Models.Hr;

namespace IT_Service_Management_System.Helpers.Hr
{
    /// <summary>
    /// Calculations fixed by the Labour Act [Chapter 28:01] rather than by configuration.
    /// <para>
    /// These are statutory <em>minimums</em>. A contract of employment, a registered code of
    /// conduct or a National Employment Council collective bargaining agreement may improve on
    /// them, and where it does the better term applies — so every method here returns the floor,
    /// and callers should compare it against whatever the contract says.
    /// </para>
    /// <para>
    /// The figures are current to the best of the author's knowledge at the time of writing and
    /// should be confirmed against the Act as amended before being relied on for a real
    /// termination or retrenchment.
    /// </para>
    /// </summary>
    public static class ZimbabweLabourLaw
    {
        /// <summary>
        /// Minimum notice on termination, by the length of the contract — Labour Act s.12(4).
        /// The period turns on the duration of the contract rather than on service to date.
        /// </summary>
        public static NoticePeriod MinimumNotice(EmploymentType employmentType, double contractMonths)
        {
            // A casual or seasonal engagement carries a day's notice regardless of length.
            if (employmentType is EmploymentType.Temporary or EmploymentType.Intern && contractMonths < 6)
                return new NoticePeriod(1, NoticeUnit.Days, "s.12(4)(e) — casual work or seasonal work");

            return contractMonths switch
            {
                >= 24 => new NoticePeriod(3, NoticeUnit.Months, "s.12(4)(a) — contract of two years or more"),
                >= 12 => new NoticePeriod(2, NoticeUnit.Months, "s.12(4)(b) — contract of one year or more but less than two"),
                >= 6 => new NoticePeriod(1, NoticeUnit.Months, "s.12(4)(c) — contract of six months or more but less than one year"),
                >= 3 => new NoticePeriod(2, NoticeUnit.Weeks, "s.12(4)(d) — contract of three months or more but less than six"),
                _ => new NoticePeriod(1, NoticeUnit.Days, "s.12(4)(e) — contract of less than three months")
            };
        }

        public record NoticePeriod(int Length, NoticeUnit Unit, string Authority)
        {
            public override string ToString() =>
                $"{Length} {Unit.ToString().ToLowerInvariant().TrimEnd('s')}{(Length == 1 ? "" : "s")}";

            /// <summary>The date notice expires if given on <paramref name="given"/>.</summary>
            public DateTime ExpiryFrom(DateTime given) => Unit switch
            {
                NoticeUnit.Days => given.AddDays(Length),
                NoticeUnit.Weeks => given.AddDays(Length * 7),
                _ => given.AddMonths(Length)
            };
        }

        public enum NoticeUnit { Days, Weeks, Months }

        /// <summary>
        /// The minimum retrenchment package — Labour Act s.12C, as introduced by the Labour
        /// Amendment Act, 2015: one month's salary for every two years of service, or a
        /// proportionate part. Service is counted to the retrenchment date.
        /// </summary>
        public static RetrenchmentPackage MinimumRetrenchmentPackage(
            decimal monthlySalary, DateTime? hireDate, DateTime retrenchmentDate, decimal monthsPerTwoYears = 1m)
        {
            if (!hireDate.HasValue || monthlySalary <= 0)
                return new RetrenchmentPackage(0, 0, 0, "Service dates or salary missing — cannot compute.");

            var years = (retrenchmentDate.Date - hireDate.Value.Date).TotalDays / 365.25;
            if (years <= 0) return new RetrenchmentPackage(0, 0, 0, "No completed service.");

            // One month per two years, pro-rated — the Act allows a proportionate part for an
            // incomplete second year rather than rounding down to whole two-year blocks.
            var monthsPayable = (decimal)(years / 2.0) * monthsPerTwoYears;
            var amount = Math.Round(monthlySalary * monthsPayable, 2);

            return new RetrenchmentPackage(
                Math.Round((decimal)years, 2),
                Math.Round(monthsPayable, 2),
                amount,
                "s.12C — minimum retrenchment package of one month's salary for every two years of service");
        }

        public record RetrenchmentPackage(decimal YearsOfService, decimal MonthsPayable, decimal Amount, string Basis);

        /// <summary>
        /// Whether an employee has completed the qualifying service for maternity leave, and how
        /// long remains if not — Labour Act s.18.
        /// </summary>
        public static (bool Qualifies, int MonthsShort) QualifiesForMaternityLeave(
            DateTime? hireDate, DateTime asAt, int qualifyingMonths)
        {
            if (!hireDate.HasValue) return (false, qualifyingMonths);

            var months = (asAt.Year - hireDate.Value.Year) * 12 + asAt.Month - hireDate.Value.Month;
            if (asAt.Day < hireDate.Value.Day) months--;

            return months >= qualifyingMonths
                ? (true, 0)
                : (false, qualifyingMonths - Math.Max(0, months));
        }

        /// <summary>
        /// Vacation leave accrued between two dates, at the statutory minimum rate per completed
        /// month of service — Labour Act s.14A.
        /// </summary>
        public static decimal AccruedVacationLeave(DateTime from, DateTime to, decimal daysPerMonth)
        {
            if (to <= from) return 0;

            var months = (to.Year - from.Year) * 12 + to.Month - from.Month;
            if (to.Day < from.Day) months--;
            if (months <= 0) return 0;

            return Math.Round(months * daysPerMonth, 2);
        }

        /// <summary>
        /// How a period of sick leave splits between full pay, half pay and unpaid, given what has
        /// already been taken in the current twelve-month cycle — Labour Act s.14.
        /// <para>
        /// Once both entitlements are exhausted the employer may begin the incapacity process, so
        /// the result flags that rather than leaving it to be noticed.
        /// </para>
        /// </summary>
        public static SickLeaveSplit SplitSickLeave(int daysRequested, int fullPayTaken, int halfPayTaken,
            int fullPayEntitlement, int halfPayEntitlement)
        {
            var fullRemaining = Math.Max(0, fullPayEntitlement - fullPayTaken);
            var halfRemaining = Math.Max(0, halfPayEntitlement - halfPayTaken);

            var atFullPay = Math.Min(daysRequested, fullRemaining);
            var atHalfPay = Math.Min(daysRequested - atFullPay, halfRemaining);
            var unpaid = daysRequested - atFullPay - atHalfPay;

            return new SickLeaveSplit(
                atFullPay, atHalfPay, unpaid,
                fullRemaining - atFullPay,
                halfRemaining - atHalfPay,
                unpaid > 0);
        }

        public record SickLeaveSplit(
            int FullPayDays, int HalfPayDays, int UnpaidDays,
            int FullPayRemaining, int HalfPayRemaining,
            bool EntitlementExhausted);

        /// <summary>
        /// The standard Zimbabwean public holidays for a year, with the Monday substitution the
        /// Public Holidays and Prohibition of Business Act [Chapter 10:21] requires when a fixed
        /// date falls on a Sunday.
        /// <para>
        /// Easter is derived rather than listed. Holidays declared by the President for a
        /// particular occasion are not predictable and must be added by hand.
        /// </para>
        /// </summary>
        public static List<PublicHoliday> StandardHolidays(int year)
        {
            var holidays = new List<PublicHoliday>();

            void Fixed(int month, int day, string name)
            {
                var date = new DateTime(year, month, day);
                // A fixed holiday falling on a Sunday is observed on the Monday.
                var shifted = date.DayOfWeek == DayOfWeek.Sunday;
                holidays.Add(new PublicHoliday
                {
                    Name = shifted ? $"{name} (observed)" : name,
                    Date = shifted ? date.AddDays(1) : date,
                    IsObservedShift = shifted
                });
            }

            Fixed(1, 1, "New Year's Day");
            Fixed(2, 21, "Robert Gabriel Mugabe National Youth Day");
            Fixed(4, 18, "Independence Day");
            Fixed(5, 1, "Workers' Day");
            Fixed(5, 25, "Africa Day");
            Fixed(12, 22, "Unity Day");
            Fixed(12, 25, "Christmas Day");
            Fixed(12, 26, "Boxing Day");

            // Easter — Good Friday, Easter Saturday and Easter Monday are all holidays.
            var easter = EasterSunday(year);
            holidays.Add(new PublicHoliday { Name = "Good Friday", Date = easter.AddDays(-2) });
            holidays.Add(new PublicHoliday { Name = "Easter Saturday", Date = easter.AddDays(-1) });
            holidays.Add(new PublicHoliday { Name = "Easter Monday", Date = easter.AddDays(1) });

            // Heroes' Day is the second Monday of August; Defence Forces Day the day after.
            var heroes = new DateTime(year, 8, 1);
            while (heroes.DayOfWeek != DayOfWeek.Monday) heroes = heroes.AddDays(1);
            heroes = heroes.AddDays(7);
            holidays.Add(new PublicHoliday { Name = "Heroes' Day", Date = heroes });
            holidays.Add(new PublicHoliday { Name = "Defence Forces Day", Date = heroes.AddDays(1) });

            return holidays.OrderBy(h => h.Date).ToList();
        }

        /// <summary>Anonymous Gregorian computus — the standard algorithm for the date of Easter.</summary>
        private static DateTime EasterSunday(int year)
        {
            var a = year % 19;
            var b = year / 100;
            var c = year % 100;
            var d = b / 4;
            var e = b % 4;
            var f = (b + 8) / 25;
            var g = (b - f + 1) / 3;
            var h = (19 * a + b - d - g + 15) % 30;
            var i = c / 4;
            var k = c % 4;
            var l = (32 + 2 * e + 2 * i - h - k) % 7;
            var m = (a + 11 * h + 22 * l) / 451;
            var month = (h + l - 7 * m + 114) / 31;
            var day = (h + l - 7 * m + 114) % 31 + 1;
            return new DateTime(year, month, day);
        }
    }
}
