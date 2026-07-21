using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IT_Service_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class AddProactiveSlaEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SlaPolicyId",
                table: "Tickets",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SlaCalendarId",
                table: "SlaPolicies",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WarningThresholdPercent",
                table: "SlaPolicies",
                type: "int",
                nullable: false,
                defaultValue: 75);

            migrationBuilder.CreateTable(
                name: "SlaCalendars",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    WorkDayStart = table.Column<TimeSpan>(type: "time", nullable: false),
                    WorkDayEnd = table.Column<TimeSpan>(type: "time", nullable: false),
                    WorkingDaysMask = table.Column<int>(type: "int", nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SlaCalendars", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SlaEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TicketId = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ThresholdPercent = table.Column<int>(type: "int", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SlaEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SlaEvents_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SlaHolidays",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SlaCalendarId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SlaHolidays", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SlaHolidays_SlaCalendars_SlaCalendarId",
                        column: x => x.SlaCalendarId,
                        principalTable: "SlaCalendars",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "SlaPolicies",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "SlaCalendarId", "WarningThresholdPercent" },
                values: new object[] { null, 75 });

            migrationBuilder.UpdateData(
                table: "SlaPolicies",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "SlaCalendarId", "WarningThresholdPercent" },
                values: new object[] { null, 75 });

            migrationBuilder.UpdateData(
                table: "SlaPolicies",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "SlaCalendarId", "WarningThresholdPercent" },
                values: new object[] { null, 75 });

            migrationBuilder.UpdateData(
                table: "SlaPolicies",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "SlaCalendarId", "WarningThresholdPercent" },
                values: new object[] { null, 75 });

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_SlaPolicyId",
                table: "Tickets",
                column: "SlaPolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_SlaPolicies_SlaCalendarId",
                table: "SlaPolicies",
                column: "SlaCalendarId");

            migrationBuilder.CreateIndex(
                name: "IX_SlaCalendars_Name",
                table: "SlaCalendars",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SlaEvents_TicketId_Type",
                table: "SlaEvents",
                columns: new[] { "TicketId", "Type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SlaHolidays_SlaCalendarId_Date",
                table: "SlaHolidays",
                columns: new[] { "SlaCalendarId", "Date" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_SlaPolicies_SlaCalendars_SlaCalendarId",
                table: "SlaPolicies",
                column: "SlaCalendarId",
                principalTable: "SlaCalendars",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_SlaPolicies_SlaPolicyId",
                table: "Tickets",
                column: "SlaPolicyId",
                principalTable: "SlaPolicies",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SlaPolicies_SlaCalendars_SlaCalendarId",
                table: "SlaPolicies");

            migrationBuilder.DropForeignKey(
                name: "FK_Tickets_SlaPolicies_SlaPolicyId",
                table: "Tickets");

            migrationBuilder.DropTable(
                name: "SlaEvents");

            migrationBuilder.DropTable(
                name: "SlaHolidays");

            migrationBuilder.DropTable(
                name: "SlaCalendars");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_SlaPolicyId",
                table: "Tickets");

            migrationBuilder.DropIndex(
                name: "IX_SlaPolicies_SlaCalendarId",
                table: "SlaPolicies");

            migrationBuilder.DropColumn(
                name: "SlaPolicyId",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "SlaCalendarId",
                table: "SlaPolicies");

            migrationBuilder.DropColumn(
                name: "WarningThresholdPercent",
                table: "SlaPolicies");
        }
    }
}
