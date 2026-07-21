using IT_Service_Management_System.Services.Itsm;

namespace IT_Service_Management_System.Tests
{
    public class SlaMonitorRulesTests
    {
        private static readonly DateTime Start = new(2026, 7, 21, 8, 0, 0);
        private static readonly DateTime Due = new(2026, 7, 21, 12, 0, 0);

        [Fact]
        public void Warning_is_not_reached_before_threshold()
            => Assert.False(SlaMonitorRules.WarningReached(Start, Due, 75, new DateTime(2026, 7, 21, 10, 59, 0)));

        [Fact]
        public void Warning_is_reached_at_threshold()
            => Assert.True(SlaMonitorRules.WarningReached(Start, Due, 75, new DateTime(2026, 7, 21, 11, 0, 0)));

        [Fact]
        public void Invalid_window_is_treated_as_due_at_deadline()
            => Assert.True(SlaMonitorRules.WarningReached(Due, Start, 75, Due));
    }
}
