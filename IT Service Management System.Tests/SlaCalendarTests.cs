using IT_Service_Management_System.Models.Itsm;
using IT_Service_Management_System.Services.Itsm;

namespace IT_Service_Management_System.Tests
{
    public class SlaCalendarTests
    {
        [Fact]
        public void Custom_hours_are_used()
        {
            var calendar = new SlaCalendar
            {
                WorkDayStart = new TimeSpan(7, 30, 0),
                WorkDayEnd = new TimeSpan(16, 0, 0),
                WorkingDaysMask = SlaCalendar.MondayToFriday
            };

            var result = SlaService.AddBusinessMinutes(new DateTime(2026, 7, 20, 6, 0, 0), 60, calendar);

            Assert.Equal(new DateTime(2026, 7, 20, 8, 30, 0), result);
        }

        [Fact]
        public void Configured_holiday_is_skipped()
        {
            var calendar = new SlaCalendar
            {
                WorkDayStart = new TimeSpan(8, 0, 0),
                WorkDayEnd = new TimeSpan(17, 0, 0),
                WorkingDaysMask = SlaCalendar.MondayToFriday,
                Holidays = new List<SlaHoliday>
                {
                    new() { Name = "Holiday", Date = new DateOnly(2026, 7, 21) }
                }
            };

            var result = SlaService.AddBusinessMinutes(new DateTime(2026, 7, 20, 16, 0, 0), 120, calendar);

            Assert.Equal(new DateTime(2026, 7, 22, 9, 0, 0), result);
        }

        [Fact]
        public void Calendar_respects_selected_working_days()
        {
            var mondayAndWednesday = (1 << (int)DayOfWeek.Monday) | (1 << (int)DayOfWeek.Wednesday);
            var calendar = new SlaCalendar { WorkingDaysMask = mondayAndWednesday };

            var result = SlaService.AddBusinessMinutes(new DateTime(2026, 7, 20, 16, 0, 0), 120, calendar);

            Assert.Equal(new DateTime(2026, 7, 22, 9, 0, 0), result);
        }
    }
}
