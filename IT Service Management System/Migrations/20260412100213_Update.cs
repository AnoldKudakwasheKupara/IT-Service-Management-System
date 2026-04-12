using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IT_Service_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class Update : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DayOfMonth",
                table: "PaymentSchedules");

            migrationBuilder.DropColumn(
                name: "FixedDate",
                table: "PaymentSchedules");

            migrationBuilder.AddColumn<DateTime>(
                name: "PaymentDate",
                table: "PaymentSchedules",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaymentDate",
                table: "PaymentSchedules");

            migrationBuilder.AddColumn<int>(
                name: "DayOfMonth",
                table: "PaymentSchedules",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FixedDate",
                table: "PaymentSchedules",
                type: "datetime2",
                nullable: true);
        }
    }
}
