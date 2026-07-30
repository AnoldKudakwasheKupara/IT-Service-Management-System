using IT_Service_Management_System.Models;
using IT_Service_Management_System.Services.Itsm;
using static IT_Service_Management_System.Models.Ticket;

namespace IT_Service_Management_System.Tests
{
    /// <summary>
    /// The on-hold SLA pause arithmetic. These assertions are only possible because
    /// <see cref="TicketService.ApplyStatusTimestamps"/> takes the instant as a parameter instead of
    /// reading <c>DateTime.Now</c> — the whole point of routing the services through TimeProvider.
    /// </summary>
    public class TicketStatusTimestampTests
    {
        private static readonly DateTime Created = new(2026, 7, 20, 9, 0, 0);

        private static Ticket OpenTicket() => new()
        {
            Status = TicketStatus.Open,
            CreatedAt = Created,
            ResponseDueAt = Created.AddMinutes(30),
            DueAt = Created.AddHours(8)
        };

        [Fact]
        public void Entering_hold_stamps_the_pause_start()
        {
            var ticket = OpenTicket();
            var heldAt = Created.AddHours(1);

            ticket.Status = TicketStatus.OnHold;
            TicketService.ApplyStatusTimestamps(ticket, TicketStatus.Open, heldAt);

            Assert.Equal(heldAt, ticket.OnHoldSince);
            Assert.Equal(0, ticket.PausedMinutes);
            Assert.Equal(Created.AddHours(8), ticket.DueAt);   // targets untouched while paused
        }

        [Fact]
        public void Leaving_hold_pushes_both_targets_out_by_the_paused_time()
        {
            var ticket = OpenTicket();
            ticket.Status = TicketStatus.OnHold;
            TicketService.ApplyStatusTimestamps(ticket, TicketStatus.Open, Created.AddHours(1));

            ticket.Status = TicketStatus.InProgress;
            TicketService.ApplyStatusTimestamps(ticket, TicketStatus.OnHold, Created.AddHours(3));

            Assert.Equal(120, ticket.PausedMinutes);
            Assert.Equal(Created.AddHours(10), ticket.DueAt);              // 8h + 2h paused
            Assert.Equal(Created.AddMinutes(150), ticket.ResponseDueAt);   // 30m + 2h paused
            Assert.Null(ticket.OnHoldSince);
        }

        [Fact]
        public void Paused_time_accumulates_across_repeated_holds()
        {
            var ticket = OpenTicket();

            ticket.Status = TicketStatus.OnHold;
            TicketService.ApplyStatusTimestamps(ticket, TicketStatus.Open, Created.AddHours(1));
            ticket.Status = TicketStatus.InProgress;
            TicketService.ApplyStatusTimestamps(ticket, TicketStatus.OnHold, Created.AddHours(2));

            ticket.Status = TicketStatus.OnHold;
            TicketService.ApplyStatusTimestamps(ticket, TicketStatus.InProgress, Created.AddHours(4));
            ticket.Status = TicketStatus.InProgress;
            TicketService.ApplyStatusTimestamps(ticket, TicketStatus.OnHold, Created.AddHours(7));

            Assert.Equal(240, ticket.PausedMinutes);              // 1h + 3h
            Assert.Equal(Created.AddHours(12), ticket.DueAt);     // 8h + 4h paused
        }

        [Fact]
        public void Response_target_is_not_extended_once_the_first_reply_has_landed()
        {
            var ticket = OpenTicket();
            ticket.FirstRespondedAt = Created.AddMinutes(10);

            ticket.Status = TicketStatus.OnHold;
            TicketService.ApplyStatusTimestamps(ticket, TicketStatus.Open, Created.AddHours(1));
            ticket.Status = TicketStatus.InProgress;
            TicketService.ApplyStatusTimestamps(ticket, TicketStatus.OnHold, Created.AddHours(3));

            Assert.Equal(Created.AddMinutes(30), ticket.ResponseDueAt);   // unchanged — already met
            Assert.Equal(Created.AddHours(10), ticket.DueAt);             // resolution still extended
        }

        [Fact]
        public void Resolving_stamps_ResolvedAt_and_reopening_clears_it()
        {
            var ticket = OpenTicket();
            var resolvedAt = Created.AddHours(2);

            ticket.Status = TicketStatus.Resolved;
            TicketService.ApplyStatusTimestamps(ticket, TicketStatus.Open, resolvedAt);
            Assert.Equal(resolvedAt, ticket.ResolvedAt);

            ticket.Status = TicketStatus.Open;
            TicketService.ApplyStatusTimestamps(ticket, TicketStatus.Resolved, Created.AddHours(5));
            Assert.Null(ticket.ResolvedAt);
            Assert.Null(ticket.ClosedAt);
        }

        [Fact]
        public void A_ticket_on_hold_is_never_counted_as_breached()
        {
            var ticket = OpenTicket();
            ticket.Status = TicketStatus.OnHold;
            ticket.OnHoldSince = Created.AddHours(1);

            var wellPastDue = Created.AddHours(20);
            Assert.False(ticket.IsSlaBreachedAt(wellPastDue));
            Assert.False(ticket.IsResponseBreachedAt(wellPastDue));
        }

        [Fact]
        public void An_overdue_open_ticket_is_breached()
        {
            var ticket = OpenTicket();
            Assert.False(ticket.IsSlaBreachedAt(Created.AddHours(7)));
            Assert.True(ticket.IsSlaBreachedAt(Created.AddHours(9)));
            Assert.True(ticket.IsResponseBreachedAt(Created.AddHours(1)));
        }
    }
}
