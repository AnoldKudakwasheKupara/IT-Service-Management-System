using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IT_Service_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class UpdateHrApprovalForm : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Approved",
                table: "HrApprovals");

            migrationBuilder.DropColumn(
                name: "HrUserId",
                table: "HrApprovals");

            migrationBuilder.RenameColumn(
                name: "StaffIdCardReturned",
                table: "HrApprovals",
                newName: "StaffIdReturned");

            migrationBuilder.AddColumn<int>(
                name: "ApprovedById",
                table: "HrApprovals",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExitInterviewDate",
                table: "HrApprovals",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExitInterviewReceivedBy",
                table: "HrApprovals",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FuneralPolicyDate",
                table: "HrApprovals",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FuneralPolicyReceivedBy",
                table: "HrApprovals",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "HandoverReportDate",
                table: "HrApprovals",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HandoverReportReceivedBy",
                table: "HrApprovals",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "MedicalAidDate",
                table: "HrApprovals",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MedicalAidReceivedBy",
                table: "HrApprovals",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NSSADate",
                table: "HrApprovals",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NSSAReceivedBy",
                table: "HrApprovals",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ResignationLetterDate",
                table: "HrApprovals",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResignationLetterReceivedBy",
                table: "HrApprovals",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StaffIdDate",
                table: "HrApprovals",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StaffIdReceivedBy",
                table: "HrApprovals",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApprovedById",
                table: "HrApprovals");

            migrationBuilder.DropColumn(
                name: "ExitInterviewDate",
                table: "HrApprovals");

            migrationBuilder.DropColumn(
                name: "ExitInterviewReceivedBy",
                table: "HrApprovals");

            migrationBuilder.DropColumn(
                name: "FuneralPolicyDate",
                table: "HrApprovals");

            migrationBuilder.DropColumn(
                name: "FuneralPolicyReceivedBy",
                table: "HrApprovals");

            migrationBuilder.DropColumn(
                name: "HandoverReportDate",
                table: "HrApprovals");

            migrationBuilder.DropColumn(
                name: "HandoverReportReceivedBy",
                table: "HrApprovals");

            migrationBuilder.DropColumn(
                name: "MedicalAidDate",
                table: "HrApprovals");

            migrationBuilder.DropColumn(
                name: "MedicalAidReceivedBy",
                table: "HrApprovals");

            migrationBuilder.DropColumn(
                name: "NSSADate",
                table: "HrApprovals");

            migrationBuilder.DropColumn(
                name: "NSSAReceivedBy",
                table: "HrApprovals");

            migrationBuilder.DropColumn(
                name: "ResignationLetterDate",
                table: "HrApprovals");

            migrationBuilder.DropColumn(
                name: "ResignationLetterReceivedBy",
                table: "HrApprovals");

            migrationBuilder.DropColumn(
                name: "StaffIdDate",
                table: "HrApprovals");

            migrationBuilder.DropColumn(
                name: "StaffIdReceivedBy",
                table: "HrApprovals");

            migrationBuilder.RenameColumn(
                name: "StaffIdReturned",
                table: "HrApprovals",
                newName: "StaffIdCardReturned");

            migrationBuilder.AddColumn<bool>(
                name: "Approved",
                table: "HrApprovals",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "HrUserId",
                table: "HrApprovals",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}
