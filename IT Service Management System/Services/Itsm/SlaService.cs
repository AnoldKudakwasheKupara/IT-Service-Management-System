using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Models.Itsm;
using Microsoft.EntityFrameworkCore;
using static IT_Service_Management_System.Models.Ticket;

namespace IT_Service_Management_System.Services.Itsm
{
    /// <summary>Response + resolution deadlines derived from the applicable SLA policy.</summary>
    public record SlaTargets(int? PolicyId, DateTime? ResponseDueAt, DateTime? ResolutionDueAt);

    public interface ISlaService
    {
        Task<SlaPolicy?> ResolveAsync(TicketPriority priority, string? category);
        Task<SlaTargets> ComputeAsync(TicketPriority priority, string? category, DateTime from);
    }

    /// <summary>
    /// Resolves the most specific active SLA policy for a ticket and computes its deadlines.
    /// Specificity: priority+category &gt; priority-only &gt; category-only &gt; catch-all.
    /// Supports business-hours (Mon–Fri, 08:00–17:00) or 24×7 targets.
    /// </summary>
    public class SlaService : ISlaService
    {
        private readonly ApplicationDbContext _db;
        public SlaService(ApplicationDbContext db) => _db = db;

        public async Task<SlaPolicy?> ResolveAsync(TicketPriority priority, string? category)
        {
            var candidates = await _db.SlaPolicies.AsNoTracking()
                .Include(p => p.Calendar).ThenInclude(c => c!.Holidays)
                .Where(p => p.IsActive &&
                            (p.Priority == null || p.Priority == priority) &&
                            (p.Category == null || p.Category == category))
                .ToListAsync();

            return candidates
                .OrderByDescending(p => (p.Priority != null ? 2 : 0) + (p.Category != null ? 1 : 0))
                .ThenBy(p => p.Id)
                .FirstOrDefault();
        }

        public async Task<SlaTargets> ComputeAsync(TicketPriority priority, string? category, DateTime from)
        {
            var policy = await ResolveAsync(priority, category);
            if (policy == null) return new SlaTargets(null, null, null);

            DateTime response = policy.BusinessHoursOnly
                ? AddBusinessMinutes(from, policy.ResponseMinutes, policy.Calendar)
                : from.AddMinutes(policy.ResponseMinutes);
            DateTime resolution = policy.BusinessHoursOnly
                ? AddBusinessMinutes(from, policy.ResolutionMinutes, policy.Calendar)
                : from.AddMinutes(policy.ResolutionMinutes);

            return new SlaTargets(policy.Id, response, resolution);
        }

        /// <summary>Adds working minutes using the default Monday-Friday, 08:00-17:00 calendar.</summary>
        public static DateTime AddBusinessMinutes(DateTime start, int minutes) =>
            AddBusinessMinutes(start, minutes, null);

        /// <summary>Adds working minutes, skipping the calendar's non-working days and holidays.</summary>
        public static DateTime AddBusinessMinutes(DateTime start, int minutes, SlaCalendar? calendar)
        {
            if (minutes <= 0) return start;

            var workStart = calendar?.WorkDayStart ?? new TimeSpan(8, 0, 0);
            var workEnd = calendar?.WorkDayEnd ?? new TimeSpan(17, 0, 0);
            if (workEnd <= workStart) { workStart = new TimeSpan(8, 0, 0); workEnd = new TimeSpan(17, 0, 0); }
            var workingDays = calendar?.WorkingDaysMask ?? SlaCalendar.MondayToFriday;
            if (workingDays == 0) workingDays = SlaCalendar.MondayToFriday;
            var holidays = calendar?.Holidays.Select(h => h.Date).ToHashSet() ?? new HashSet<DateOnly>();

            bool IsWorkingDate(DateTime value) =>
                (workingDays & (1 << (int)value.DayOfWeek)) != 0 &&
                !holidays.Contains(DateOnly.FromDateTime(value));

            DateTime NextWorkDayStart(DateTime value)
            {
                var day = value.Date.AddDays(1);
                while (!IsWorkingDate(day)) day = day.AddDays(1);
                return day.Add(workStart);
            }

            var cursor = start;
            var remaining = minutes;
            while (remaining > 0)
            {
                if (!IsWorkingDate(cursor)) { cursor = NextWorkDayStart(cursor); continue; }
                if (cursor.TimeOfDay < workStart) { cursor = cursor.Date.Add(workStart); continue; }
                if (cursor.TimeOfDay >= workEnd) { cursor = NextWorkDayStart(cursor); continue; }

                var endOfDay = cursor.Date.Add(workEnd);
                var availableToday = (int)(endOfDay - cursor).TotalMinutes;
                if (availableToday <= 0) { cursor = NextWorkDayStart(cursor); continue; }

                var take = Math.Min(remaining, availableToday);
                cursor = cursor.AddMinutes(take);
                remaining -= take;
                if (remaining > 0) cursor = NextWorkDayStart(cursor);
            }
            return cursor;
        }
    }
}
