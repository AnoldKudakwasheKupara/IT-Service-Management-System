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
        private const int WorkStartHour = 8;
        private const int WorkEndHour = 17;                 // 9 business hours/day
        private const int MinutesPerWorkDay = (WorkEndHour - WorkStartHour) * 60;

        private readonly ApplicationDbContext _db;
        public SlaService(ApplicationDbContext db) => _db = db;

        public async Task<SlaPolicy?> ResolveAsync(TicketPriority priority, string? category)
        {
            var candidates = await _db.SlaPolicies.AsNoTracking()
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
                ? AddBusinessMinutes(from, policy.ResponseMinutes)
                : from.AddMinutes(policy.ResponseMinutes);
            DateTime resolution = policy.BusinessHoursOnly
                ? AddBusinessMinutes(from, policy.ResolutionMinutes)
                : from.AddMinutes(policy.ResolutionMinutes);

            return new SlaTargets(policy.Id, response, resolution);
        }

        /// <summary>Adds working minutes, skipping weekends and non-working hours.</summary>
        public static DateTime AddBusinessMinutes(DateTime start, int minutes)
        {
            var cursor = start;
            var remaining = minutes;

            while (remaining > 0)
            {
                // Advance to the next working moment.
                if (cursor.DayOfWeek == DayOfWeek.Saturday) { cursor = cursor.Date.AddDays(2).AddHours(WorkStartHour); continue; }
                if (cursor.DayOfWeek == DayOfWeek.Sunday) { cursor = cursor.Date.AddDays(1).AddHours(WorkStartHour); continue; }
                if (cursor.Hour < WorkStartHour) { cursor = cursor.Date.AddHours(WorkStartHour); continue; }
                if (cursor.Hour >= WorkEndHour) { cursor = NextWorkDayStart(cursor); continue; }

                var endOfDay = cursor.Date.AddHours(WorkEndHour);
                var availableToday = (int)(endOfDay - cursor).TotalMinutes;
                if (availableToday <= 0) { cursor = NextWorkDayStart(cursor); continue; }

                var take = Math.Min(remaining, availableToday);
                cursor = cursor.AddMinutes(take);
                remaining -= take;

                if (remaining > 0) cursor = NextWorkDayStart(cursor);
            }
            return cursor;
        }

        private static DateTime NextWorkDayStart(DateTime from)
        {
            var d = from.Date.AddDays(1);
            while (d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) d = d.AddDays(1);
            return d.AddHours(WorkStartHour);
        }
    }
}
