using IT_Service_Management_System.Models.Ims;
using IT_Service_Management_System.Services.Ims;
using Xunit;

namespace IT_Service_Management_System.Tests
{
    public class IsoDocumentServiceTests
    {
        [Theory]
        [InlineData("1.0", true, "2.0")]
        [InlineData("1.0", false, "1.1")]
        [InlineData("2.3", false, "2.4")]
        [InlineData("2.3", true, "3.0")]
        [InlineData(null, true, "1.0")]
        [InlineData(null, false, "0.1")]
        [InlineData("", false, "0.1")]
        public void NextVersionNumber_bumps_major_or_minor(string? current, bool major, string expected)
            => Assert.Equal(expected, IsoDocumentService.NextVersionNumber(current, major));

        [Fact]
        public void NextReviewDate_honours_frequency()
        {
            var from = new DateTime(2026, 1, 15);
            Assert.Equal(from.AddMonths(1), IsoDocumentService.NextReviewDate(from, ReviewFrequency.Monthly));
            Assert.Equal(from.AddMonths(3), IsoDocumentService.NextReviewDate(from, ReviewFrequency.Quarterly));
            Assert.Equal(from.AddMonths(6), IsoDocumentService.NextReviewDate(from, ReviewFrequency.SemiAnnual));
            Assert.Equal(from.AddYears(1), IsoDocumentService.NextReviewDate(from, ReviewFrequency.Annual));
            Assert.Equal(from.AddYears(2), IsoDocumentService.NextReviewDate(from, ReviewFrequency.Biennial));
            Assert.Equal(from.AddYears(3), IsoDocumentService.NextReviewDate(from, ReviewFrequency.Triennial));
            Assert.Null(IsoDocumentService.NextReviewDate(from, ReviewFrequency.None));
        }

        [Theory]
        [InlineData(DocumentStatus.DepartmentReview, ApprovalStage.DepartmentReview)]
        [InlineData(DocumentStatus.QualityReview, ApprovalStage.QualityReview)]
        [InlineData(DocumentStatus.ManagementApproval, ApprovalStage.ManagementApproval)]
        public void StageForCurrent_maps_in_workflow_statuses(DocumentStatus status, ApprovalStage expected)
        {
            var doc = new IsoDocument { Status = status };
            Assert.Equal(expected, IsoDocumentService.StageForCurrent(doc));
        }

        [Theory]
        [InlineData(DocumentStatus.Draft)]
        [InlineData(DocumentStatus.Published)]
        [InlineData(DocumentStatus.Archived)]
        public void StageForCurrent_is_null_outside_workflow(DocumentStatus status)
        {
            var doc = new IsoDocument { Status = status };
            Assert.Null(IsoDocumentService.StageForCurrent(doc));
        }
    }
}
