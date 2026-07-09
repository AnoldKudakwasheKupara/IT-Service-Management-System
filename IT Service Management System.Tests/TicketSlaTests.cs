using IT_Service_Management_System.Helpers;
using Xunit;
using static IT_Service_Management_System.Models.Ticket;

namespace IT_Service_Management_System.Tests
{
    public class TicketSlaTests
    {
        [Fact]
        public void Targets_get_tighter_with_priority()
        {
            Assert.Equal(TimeSpan.FromHours(4), TicketSla.TargetFor(TicketPriority.Critical));
            Assert.Equal(TimeSpan.FromHours(8), TicketSla.TargetFor(TicketPriority.High));
            Assert.Equal(TimeSpan.FromHours(24), TicketSla.TargetFor(TicketPriority.Medium));
            Assert.Equal(TimeSpan.FromHours(72), TicketSla.TargetFor(TicketPriority.Low));
        }

        [Fact]
        public void DueFrom_is_created_plus_target()
        {
            var created = new DateTime(2026, 7, 8, 9, 0, 0);
            Assert.Equal(created.AddHours(4), TicketSla.DueFrom(created, TicketPriority.Critical));
        }

        [Fact]
        public void Describe_handles_null_and_closed()
        {
            Assert.Equal("—", TicketSla.Describe(null, true));
            Assert.Equal("Met", TicketSla.Describe(DateTime.Now, false));
        }

        [Fact]
        public void Describe_flags_overdue_when_due_in_the_past()
        {
            var result = TicketSla.Describe(DateTime.Now.AddHours(-2), isOpen: true);
            Assert.StartsWith("Overdue", result);
        }

        [Fact]
        public void Describe_shows_time_remaining_when_due_in_the_future()
        {
            var result = TicketSla.Describe(DateTime.Now.AddHours(3), isOpen: true);
            Assert.StartsWith("Due in", result);
        }
    }
}
