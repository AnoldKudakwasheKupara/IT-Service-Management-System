using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IT_Service_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class AddRenewalFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsRenewed",
                table: "SSLCertificates",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastRenewedDate",
                table: "SSLCertificates",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsRenewed",
                table: "SSLCertificates");

            migrationBuilder.DropColumn(
                name: "LastRenewedDate",
                table: "SSLCertificates");
        }
    }
}
