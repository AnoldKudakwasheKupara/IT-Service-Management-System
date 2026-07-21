using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Models;
using IT_Service_Management_System.Models.Itsm;
using IT_Service_Management_System.Services.Realtime;
using Microsoft.EntityFrameworkCore;
using static IT_Service_Management_System.Models.Ticket;

namespace IT_Service_Management_System.Services.Itsm
{
    public static class SlaMonitorRules
    {
        public static bool WarningReached(DateTime startedAt, DateTime dueAt, int thresholdPercent, DateTime now)
        {
            if (dueAt <= startedAt) return now >= dueAt;
            var warningAt = startedAt.AddTicks((long)((dueAt - startedAt).Ticks * (thresholdPercent / 100d)));
            return now >= warningAt;
        }
    }

    /// <summary>Creates one persistent warning/breach event per ticket and automatically escalates breaches.</summary>
    public class SlaMonitoringService
    {
        private readonly ApplicationDbContext _db;
        private readonly ISlaService _sla;
        private readonly IRealtimeNotifier _realtime;
        private readonly ILogger<SlaMonitoringService> _logger;

        public SlaMonitoringService(ApplicationDbContext db, ISlaService sla,
            IRealtimeNotifier realtime, ILogger<SlaMonitoringService> logger)
        {
            _db = db;
            _sla = sla;
            _realtime = realtime;
            _logger = logger;
        }

        public async Task<int> ProcessAsync(DateTime now, CancellationToken ct = default)
        {
            var tickets = await _db.Tickets
                .Include(t => t.SlaPolicy)
                .Where(t => t.Status != TicketStatus.Resolved && t.Status != TicketStatus.Closed &&
                            t.Status != TicketStatus.OnHold &&
                            (t.ResponseDueAt != null || t.DueAt != null))
                .ToListAsync(ct);
            if (tickets.Count == 0) return 0;

            var ticketIds = tickets.Select(t => t.Id).ToList();
            var existing = await _db.SlaEvents.Where(e => ticketIds.Contains(e.TicketId))
                .Select(e => new { e.TicketId, e.Type }).ToListAsync(ct);
            var seen = existing.Select(e => (e.TicketId, e.Type)).ToHashSet();
            var notices = new List<(Ticket Ticket, SlaEvent Event)>();

            foreach (var ticket in tickets)
            {
                var policy = ticket.SlaPolicy ?? await _sla.ResolveAsync(ticket.Priority, ticket.Category);
                var threshold = Math.Clamp(policy?.WarningThresholdPercent ?? 75, 1, 99);

                if (ticket.FirstRespondedAt == null && ticket.ResponseDueAt.HasValue)
                {
                    AddIfDue(ticket, SlaEventType.ResponseWarning, threshold,
                        SlaMonitorRules.WarningReached(ticket.CreatedAt, ticket.ResponseDueAt.Value, threshold, now),
                        $"{ticket.Reference} has used {threshold}% of its response SLA.", now, seen, notices);
                    AddIfDue(ticket, SlaEventType.ResponseBreached, 100, now >= ticket.ResponseDueAt.Value,
                        $"{ticket.Reference} breached its response SLA.", now, seen, notices);
                }

                if (ticket.DueAt.HasValue)
                {
                    AddIfDue(ticket, SlaEventType.ResolutionWarning, threshold,
                        SlaMonitorRules.WarningReached(ticket.CreatedAt, ticket.DueAt.Value, threshold, now),
                        $"{ticket.Reference} has used {threshold}% of its resolution SLA.", now, seen, notices);
                    AddIfDue(ticket, SlaEventType.ResolutionBreached, 100, now >= ticket.DueAt.Value,
                        $"{ticket.Reference} breached its resolution SLA.", now, seen, notices);
                }
            }

            if (notices.Count == 0) return 0;

            foreach (var ticket in notices.Where(n => n.Event.Type is SlaEventType.ResponseBreached or SlaEventType.ResolutionBreached)
                         .Select(n => n.Ticket).DistinctBy(t => t.Id))
            {
                ticket.EscalatedAt ??= now;
                ticket.Priority = ticket.Priority switch
                {
                    TicketPriority.Low => TicketPriority.Medium,
                    TicketPriority.Medium => TicketPriority.High,
                    _ => TicketPriority.Critical
                };
                ticket.UpdatedAt = now;
            }

            await _db.SaveChangesAsync(ct);

            foreach (var (ticket, slaEvent) in notices)
            {
                var level = slaEvent.Type is SlaEventType.ResponseBreached or SlaEventType.ResolutionBreached ? "error" : "warning";
                var notice = new RealtimeNotice("SLA alert", slaEvent.Message, $"/Tickets/Details/{ticket.Id}", level);
                if (ticket.AssignedToId.HasValue)
                    await _realtime.NotifyUserAsync(ticket.AssignedToId.Value, notice);
                await _realtime.NotifyStaffAsync(notice);
            }

            _logger.LogInformation("Created {Count} proactive SLA events.", notices.Count);
            return notices.Count;
        }

        private void AddIfDue(Ticket ticket, SlaEventType type, int threshold, bool isDue, string message,
            DateTime now, HashSet<(int, SlaEventType)> seen, List<(Ticket, SlaEvent)> notices)
        {
            if (!isDue || !seen.Add((ticket.Id, type))) return;
            var slaEvent = new SlaEvent
            {
                TicketId = ticket.Id,
                Type = type,
                ThresholdPercent = threshold,
                Message = message,
                OccurredAt = now
            };
            _db.SlaEvents.Add(slaEvent);
            notices.Add((ticket, slaEvent));
        }
    }

    public class SlaMonitoringHostedService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<SlaMonitoringHostedService> _logger;

        public SlaMonitoringHostedService(IServiceScopeFactory scopeFactory, ILogger<SlaMonitoringHostedService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
            do
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<SlaMonitoringService>()
                        .ProcessAsync(DateTime.Now, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Proactive SLA monitoring cycle failed.");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
    }
}
