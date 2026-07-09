using IT_Service_Management_System.Services.Itsm;
using Xunit;

namespace IT_Service_Management_System.Tests
{
    public class SlaServiceTests
    {
        // Working hours are 08:00–17:00, Monday–Friday. 2026-07-10 is a Friday, 2026-07-13 a Monday.
        private static readonly DateTime FriAfternoon = new(2026, 7, 10, 16, 0, 0);
        private static readonly DateTime MonMorning = new(2026, 7, 13, 8, 0, 0);

        [Fact]
        public void Zero_minutes_returns_start_unchanged()
            => Assert.Equal(MonMorning, SlaService.AddBusinessMinutes(MonMorning, 0));

        [Fact]
        public void Within_the_day_advances_normally()
            => Assert.Equal(new DateTime(2026, 7, 13, 9, 0, 0), SlaService.AddBusinessMinutes(MonMorning, 60));

        [Fact]
        public void Rolls_over_the_weekend()
        {
            // Fri 16:00 + 120 min: 60 min fills to 17:00 Fri, remaining 60 starts Mon 08:00 -> Mon 09:00.
            Assert.Equal(new DateTime(2026, 7, 13, 9, 0, 0), SlaService.AddBusinessMinutes(FriAfternoon, 120));
        }

        [Fact]
        public void Before_hours_start_snaps_to_work_start()
        {
            var early = new DateTime(2026, 7, 13, 6, 0, 0);
            Assert.Equal(new DateTime(2026, 7, 13, 9, 0, 0), SlaService.AddBusinessMinutes(early, 60));
        }

        [Fact]
        public void Weekend_start_snaps_to_monday()
        {
            var saturday = new DateTime(2026, 7, 11, 10, 0, 0);
            Assert.Equal(new DateTime(2026, 7, 13, 9, 0, 0), SlaService.AddBusinessMinutes(saturday, 60));
        }

        [Fact]
        public void Result_never_lands_on_a_weekend()
        {
            var result = SlaService.AddBusinessMinutes(FriAfternoon, 3000);
            Assert.NotEqual(DayOfWeek.Saturday, result.DayOfWeek);
            Assert.NotEqual(DayOfWeek.Sunday, result.DayOfWeek);
        }
    }
}
