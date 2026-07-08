using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IT_Service_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class EnhanceMeetingMinutes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MeetingRosterMembers_UserId",
                table: "MeetingRosterMembers");

            migrationBuilder.DropColumn(
                name: "Day",
                table: "Meetings");

            migrationBuilder.AddColumn<int>(
                name: "DepartmentId",
                table: "Meetings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "EndTime",
                table: "Meetings",
                type: "time",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinuteTakerId",
                table: "Meetings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NextMeetingDate",
                table: "Meetings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Objective",
                table: "Meetings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "StartTime",
                table: "Meetings",
                type: "time",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Venue",
                table: "Meetings",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DepartmentId",
                table: "MeetingRosterMembers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AssigneeLabel",
                table: "ActionItems",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Meetings_DepartmentId",
                table: "Meetings",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Meetings_MinuteTakerId",
                table: "Meetings",
                column: "MinuteTakerId");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingRosterMembers_DepartmentId_UserId",
                table: "MeetingRosterMembers",
                columns: new[] { "DepartmentId", "UserId" },
                unique: true,
                filter: "[DepartmentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingRosterMembers_UserId",
                table: "MeetingRosterMembers",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_MeetingRosterMembers_Departments_DepartmentId",
                table: "MeetingRosterMembers",
                column: "DepartmentId",
                principalTable: "Departments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Meetings_Departments_DepartmentId",
                table: "Meetings",
                column: "DepartmentId",
                principalTable: "Departments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Meetings_Users_MinuteTakerId",
                table: "Meetings",
                column: "MinuteTakerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MeetingRosterMembers_Departments_DepartmentId",
                table: "MeetingRosterMembers");

            migrationBuilder.DropForeignKey(
                name: "FK_Meetings_Departments_DepartmentId",
                table: "Meetings");

            migrationBuilder.DropForeignKey(
                name: "FK_Meetings_Users_MinuteTakerId",
                table: "Meetings");

            migrationBuilder.DropIndex(
                name: "IX_Meetings_DepartmentId",
                table: "Meetings");

            migrationBuilder.DropIndex(
                name: "IX_Meetings_MinuteTakerId",
                table: "Meetings");

            migrationBuilder.DropIndex(
                name: "IX_MeetingRosterMembers_DepartmentId_UserId",
                table: "MeetingRosterMembers");

            migrationBuilder.DropIndex(
                name: "IX_MeetingRosterMembers_UserId",
                table: "MeetingRosterMembers");

            migrationBuilder.DropColumn(
                name: "DepartmentId",
                table: "Meetings");

            migrationBuilder.DropColumn(
                name: "EndTime",
                table: "Meetings");

            migrationBuilder.DropColumn(
                name: "MinuteTakerId",
                table: "Meetings");

            migrationBuilder.DropColumn(
                name: "NextMeetingDate",
                table: "Meetings");

            migrationBuilder.DropColumn(
                name: "Objective",
                table: "Meetings");

            migrationBuilder.DropColumn(
                name: "StartTime",
                table: "Meetings");

            migrationBuilder.DropColumn(
                name: "Venue",
                table: "Meetings");

            migrationBuilder.DropColumn(
                name: "DepartmentId",
                table: "MeetingRosterMembers");

            migrationBuilder.DropColumn(
                name: "AssigneeLabel",
                table: "ActionItems");

            migrationBuilder.AddColumn<string>(
                name: "Day",
                table: "Meetings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingRosterMembers_UserId",
                table: "MeetingRosterMembers",
                column: "UserId",
                unique: true);
        }
    }
}
