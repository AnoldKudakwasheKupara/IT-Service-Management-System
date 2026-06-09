using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IT_Service_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class UpdateFinanceClearanceForm : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FinalDuesProcessed",
                table: "FinanceClearances");

            migrationBuilder.DropColumn(
                name: "ReceiptsHandedOver",
                table: "FinanceClearances");

            migrationBuilder.DropColumn(
                name: "StaffAdvanceCleared",
                table: "FinanceClearances");

            migrationBuilder.AddColumn<DateTime>(
                name: "FinalDuesDate",
                table: "FinanceClearances",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FinalDuesReceivedBy",
                table: "FinanceClearances",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FinalDuesReference",
                table: "FinanceClearances",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReceiptsDate",
                table: "FinanceClearances",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReceiptsReceivedBy",
                table: "FinanceClearances",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReceiptsReference",
                table: "FinanceClearances",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StaffAdvanceDate",
                table: "FinanceClearances",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StaffAdvanceReceivedBy",
                table: "FinanceClearances",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StaffAdvanceReference",
                table: "FinanceClearances",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FinalDuesDate",
                table: "FinanceClearances");

            migrationBuilder.DropColumn(
                name: "FinalDuesReceivedBy",
                table: "FinanceClearances");

            migrationBuilder.DropColumn(
                name: "FinalDuesReference",
                table: "FinanceClearances");

            migrationBuilder.DropColumn(
                name: "ReceiptsDate",
                table: "FinanceClearances");

            migrationBuilder.DropColumn(
                name: "ReceiptsReceivedBy",
                table: "FinanceClearances");

            migrationBuilder.DropColumn(
                name: "ReceiptsReference",
                table: "FinanceClearances");

            migrationBuilder.DropColumn(
                name: "StaffAdvanceDate",
                table: "FinanceClearances");

            migrationBuilder.DropColumn(
                name: "StaffAdvanceReceivedBy",
                table: "FinanceClearances");

            migrationBuilder.DropColumn(
                name: "StaffAdvanceReference",
                table: "FinanceClearances");

            migrationBuilder.AddColumn<bool>(
                name: "FinalDuesProcessed",
                table: "FinanceClearances",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ReceiptsHandedOver",
                table: "FinanceClearances",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "StaffAdvanceCleared",
                table: "FinanceClearances",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }
    }
}
