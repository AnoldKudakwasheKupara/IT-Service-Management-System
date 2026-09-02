using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IT_Service_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationSettingsAndLog : Migration
    {
        /// <inheritdoc />
        // The column defaults below deliberately mirror the C# property defaults on
        // AppConfiguration rather than SQL's false/0. AppConfigurations holds a single existing
        // row and AddColumn backfills it with the column default, so leaving these false would
        // silently ship the whole notification feature switched off wherever the app is upgraded.
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "DailySummaryEnabled",
                table: "AppConfigurations",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "DailySummaryHour",
                table: "AppConfigurations",
                type: "int",
                nullable: false,
                defaultValue: 7);

            migrationBuilder.AddColumn<bool>(
                name: "DailySummaryPerAgent",
                table: "AppConfigurations",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "NotifyOnCertificateExpiry",
                table: "AppConfigurations",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "NotifyOnMaintenanceDue",
                table: "AppConfigurations",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "NotifyOnOverdue",
                table: "AppConfigurations",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "NotifyOnPaymentDue",
                table: "AppConfigurations",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "NotifyOnSlaEvent",
                table: "AppConfigurations",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "NotifyOnTicketEscalation",
                table: "AppConfigurations",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "OperationsEmailRecipients",
                table: "AppConfigurations",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReminderLeadDays",
                table: "AppConfigurations",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "30,14,7,1");

            migrationBuilder.CreateTable(
                name: "NotificationLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Subject = table.Column<int>(type: "int", nullable: false),
                    EntityId = table.Column<int>(type: "int", nullable: false),
                    TriggerKey = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    DueOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RecipientCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationLogs_Subject_EntityId_TriggerKey_DueOn",
                table: "NotificationLogs",
                columns: new[] { "Subject", "EntityId", "TriggerKey", "DueOn" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NotificationLogs");

            migrationBuilder.DropColumn(
                name: "DailySummaryEnabled",
                table: "AppConfigurations");

            migrationBuilder.DropColumn(
                name: "DailySummaryHour",
                table: "AppConfigurations");

            migrationBuilder.DropColumn(
                name: "DailySummaryPerAgent",
                table: "AppConfigurations");

            migrationBuilder.DropColumn(
                name: "NotifyOnCertificateExpiry",
                table: "AppConfigurations");

            migrationBuilder.DropColumn(
                name: "NotifyOnMaintenanceDue",
                table: "AppConfigurations");

            migrationBuilder.DropColumn(
                name: "NotifyOnOverdue",
                table: "AppConfigurations");

            migrationBuilder.DropColumn(
                name: "NotifyOnPaymentDue",
                table: "AppConfigurations");

            migrationBuilder.DropColumn(
                name: "NotifyOnSlaEvent",
                table: "AppConfigurations");

            migrationBuilder.DropColumn(
                name: "NotifyOnTicketEscalation",
                table: "AppConfigurations");

            migrationBuilder.DropColumn(
                name: "OperationsEmailRecipients",
                table: "AppConfigurations");

            migrationBuilder.DropColumn(
                name: "ReminderLeadDays",
                table: "AppConfigurations");
        }
    }
}
