using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IT_Service_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class updateDbToBeUptoDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ExitInterviewCompleted",
                table: "HrApprovals",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "FuneralPolicyCancelled",
                table: "HrApprovals",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HandoverReportSubmitted",
                table: "HrApprovals",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "MedicalAidCancelled",
                table: "HrApprovals",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "NSSACancelled",
                table: "HrApprovals",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ResignationLetterReceived",
                table: "HrApprovals",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "StaffIdCardReturned",
                table: "HrApprovals",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "AccessToken",
                table: "ExitClearances",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EmployeeSubmittedDate",
                table: "ExitClearances",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsSent",
                table: "ExitClearances",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "SentDate",
                table: "ExitClearances",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExitInterviewCompleted",
                table: "HrApprovals");

            migrationBuilder.DropColumn(
                name: "FuneralPolicyCancelled",
                table: "HrApprovals");

            migrationBuilder.DropColumn(
                name: "HandoverReportSubmitted",
                table: "HrApprovals");

            migrationBuilder.DropColumn(
                name: "MedicalAidCancelled",
                table: "HrApprovals");

            migrationBuilder.DropColumn(
                name: "NSSACancelled",
                table: "HrApprovals");

            migrationBuilder.DropColumn(
                name: "ResignationLetterReceived",
                table: "HrApprovals");

            migrationBuilder.DropColumn(
                name: "StaffIdCardReturned",
                table: "HrApprovals");

            migrationBuilder.DropColumn(
                name: "AccessToken",
                table: "ExitClearances");

            migrationBuilder.DropColumn(
                name: "EmployeeSubmittedDate",
                table: "ExitClearances");

            migrationBuilder.DropColumn(
                name: "IsSent",
                table: "ExitClearances");

            migrationBuilder.DropColumn(
                name: "SentDate",
                table: "ExitClearances");
        }
    }
}
