using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IT_Service_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class UpdateHodApprovalForm : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Approved",
                table: "HodApprovals");

            migrationBuilder.DropColumn(
                name: "HodId",
                table: "HodApprovals");

            migrationBuilder.AddColumn<int>(
                name: "ApprovedById",
                table: "HodApprovals",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConfirmedBy",
                table: "HodApprovals",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "HandoverDate",
                table: "HodApprovals",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HandoverSignature",
                table: "HodApprovals",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApprovedById",
                table: "HodApprovals");

            migrationBuilder.DropColumn(
                name: "ConfirmedBy",
                table: "HodApprovals");

            migrationBuilder.DropColumn(
                name: "HandoverDate",
                table: "HodApprovals");

            migrationBuilder.DropColumn(
                name: "HandoverSignature",
                table: "HodApprovals");

            migrationBuilder.AddColumn<bool>(
                name: "Approved",
                table: "HodApprovals",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "HodId",
                table: "HodApprovals",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}
