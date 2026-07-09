using IT_Service_Management_System.Models.Ims;
using Xunit;

namespace IT_Service_Management_System.Tests
{
    public class RiskScoringTests
    {
        [Theory]
        [InlineData(1, RiskBand.Low)]
        [InlineData(4, RiskBand.Low)]
        [InlineData(5, RiskBand.Medium)]
        [InlineData(9, RiskBand.Medium)]
        [InlineData(10, RiskBand.High)]
        [InlineData(15, RiskBand.High)]
        [InlineData(16, RiskBand.Critical)]
        [InlineData(25, RiskBand.Critical)]
        public void BandFor_maps_score_to_correct_band(int score, RiskBand expected)
            => Assert.Equal(expected, RiskScoring.BandFor(score));

        [Fact]
        public void CssClass_matches_band()
        {
            Assert.Equal("b-low", RiskScoring.CssClass(RiskBand.Low));
            Assert.Equal("b-medium", RiskScoring.CssClass(RiskBand.Medium));
            Assert.Equal("b-high", RiskScoring.CssClass(RiskBand.High));
            Assert.Equal("b-critical", RiskScoring.CssClass(RiskBand.Critical));
        }

        [Fact]
        public void Risk_score_is_likelihood_times_impact_and_band_follows()
        {
            var risk = new Risk { Likelihood = 5, Impact = 4 };
            Assert.Equal(20, risk.Score);
            Assert.Equal(RiskBand.Critical, risk.Band);
        }
    }
}
