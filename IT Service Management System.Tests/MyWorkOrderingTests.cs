using IT_Service_Management_System.ViewModels.Itsm;

namespace IT_Service_Management_System.Tests
{
    public class MyWorkOrderingTests
    {
        private static readonly DateTime Now = new(2026, 7, 21, 12, 0, 0);

        [Fact]
        public void Overdue_work_is_first()
        {
            var items = new[]
            {
                new WorkItemVm { Reference = "NORMAL", DueAt = Now.AddDays(1), PriorityScore = 4 },
                new WorkItemVm { Reference = "OVERDUE", DueAt = Now.AddMinutes(-1), PriorityScore = 1 }
            };
            Assert.Equal("OVERDUE", MyWorkOrdering.Prioritize(items, Now).First().Reference);
        }

        [Fact]
        public void Decisions_outrank_normal_work_when_dates_are_equal()
        {
            var items = new[]
            {
                new WorkItemVm { Reference = "TASK", DueAt = Now.AddHours(1), PriorityScore = 4 },
                new WorkItemVm { Reference = "APPROVAL", DueAt = Now.AddHours(1), RequiresDecision = true }
            };
            Assert.Equal("APPROVAL", MyWorkOrdering.Prioritize(items, Now).First().Reference);
        }

        [Fact]
        public void Due_today_uses_local_calendar_date()
        {
            var item = new WorkItemVm { DueAt = Now.Date.AddHours(23) };
            Assert.True(item.IsDueToday(Now));
        }
    }
}
