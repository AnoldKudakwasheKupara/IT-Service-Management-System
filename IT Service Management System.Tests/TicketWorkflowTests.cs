using IT_Service_Management_System.Services.Itsm;
using static IT_Service_Management_System.Models.Ticket;

namespace IT_Service_Management_System.Tests
{
    public class TicketWorkflowTests
    {
        [Theory]
        [InlineData(TicketStatus.Open, TicketStatus.InProgress)]
        [InlineData(TicketStatus.Open, TicketStatus.OnHold)]
        [InlineData(TicketStatus.Open, TicketStatus.Resolved)]
        [InlineData(TicketStatus.Open, TicketStatus.Closed)]        // duplicate / spam, closed unworked
        [InlineData(TicketStatus.InProgress, TicketStatus.Open)]    // handed back to the queue
        [InlineData(TicketStatus.InProgress, TicketStatus.OnHold)]
        [InlineData(TicketStatus.InProgress, TicketStatus.Resolved)]
        [InlineData(TicketStatus.OnHold, TicketStatus.InProgress)]  // resume
        [InlineData(TicketStatus.OnHold, TicketStatus.Open)]
        [InlineData(TicketStatus.OnHold, TicketStatus.Resolved)]
        [InlineData(TicketStatus.Resolved, TicketStatus.Closed)]
        [InlineData(TicketStatus.Resolved, TicketStatus.Open)]      // reopen — fix didn't hold
        [InlineData(TicketStatus.Closed, TicketStatus.Open)]        // reopen
        public void Valid_transitions_are_allowed(TicketStatus current, TicketStatus next)
            => Assert.True(TicketWorkflow.CanTransition(current, next));

        [Theory]
        [InlineData(TicketStatus.Closed, TicketStatus.InProgress)]  // must be reopened first
        [InlineData(TicketStatus.Closed, TicketStatus.Resolved)]
        [InlineData(TicketStatus.Closed, TicketStatus.OnHold)]
        [InlineData(TicketStatus.Resolved, TicketStatus.OnHold)]    // no active work left to pause
        public void Invalid_transitions_are_rejected(TicketStatus current, TicketStatus next)
            => Assert.False(TicketWorkflow.CanTransition(current, next));

        [Theory]
        [InlineData(TicketStatus.Open)]
        [InlineData(TicketStatus.InProgress)]
        [InlineData(TicketStatus.OnHold)]
        [InlineData(TicketStatus.Resolved)]
        [InlineData(TicketStatus.Closed)]
        public void Same_status_is_a_no_op_not_a_move(TicketStatus status)
            => Assert.True(TicketWorkflow.CanTransition(status, status));

        [Fact]
        public void Closed_offers_only_reopen()
            => Assert.Equal(new[] { TicketStatus.Open }, TicketWorkflow.NextFrom(TicketStatus.Closed));

        [Fact]
        public void NextFrom_excludes_the_current_status_and_agrees_with_CanTransition()
        {
            foreach (var current in Enum.GetValues<TicketStatus>())
            {
                var next = TicketWorkflow.NextFrom(current);
                Assert.DoesNotContain(current, next);
                Assert.All(next, s => Assert.True(TicketWorkflow.CanTransition(current, s)));

                var rejected = Enum.GetValues<TicketStatus>().Where(s => s != current && !next.Contains(s));
                Assert.All(rejected, s => Assert.False(TicketWorkflow.CanTransition(current, s)));
            }
        }
    }
}
