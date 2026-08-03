using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IT_Service_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class DebrandHrFieldNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ChallengesApplyingAxisValues",
                table: "TalentIdentifications",
                newName: "ChallengesApplyingCompanyValues");

            migrationBuilder.RenameColumn(
                name: "DateJoinedAxis",
                table: "EngagementStayInterviews",
                newName: "DateJoined");

            migrationBuilder.RenameColumn(
                name: "ChangesToWorkingAtAxis",
                table: "EngagementStayInterviews",
                newName: "ChangesToWorkingHere");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ChallengesApplyingCompanyValues",
                table: "TalentIdentifications",
                newName: "ChallengesApplyingAxisValues");

            migrationBuilder.RenameColumn(
                name: "DateJoined",
                table: "EngagementStayInterviews",
                newName: "DateJoinedAxis");

            migrationBuilder.RenameColumn(
                name: "ChangesToWorkingHere",
                table: "EngagementStayInterviews",
                newName: "ChangesToWorkingAtAxis");
        }
    }
}
