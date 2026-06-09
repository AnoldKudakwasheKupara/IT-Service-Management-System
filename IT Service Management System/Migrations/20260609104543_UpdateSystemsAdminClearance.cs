using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IT_Service_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class UpdateSystemsAdminClearance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccessRemoved",
                table: "SystemsAdminClearances");

            migrationBuilder.DropColumn(
                name: "AccessoriesReturned",
                table: "SystemsAdminClearances");

            migrationBuilder.DropColumn(
                name: "EmailDisabled",
                table: "SystemsAdminClearances");

            migrationBuilder.DropColumn(
                name: "EmailRedirected",
                table: "SystemsAdminClearances");

            migrationBuilder.DropColumn(
                name: "EndpointSecurityRemoved",
                table: "SystemsAdminClearances");

            migrationBuilder.DropColumn(
                name: "LaptopBagReturned",
                table: "SystemsAdminClearances");

            migrationBuilder.DropColumn(
                name: "LaptopReturned",
                table: "SystemsAdminClearances");

            migrationBuilder.DropColumn(
                name: "LockerReturned",
                table: "SystemsAdminClearances");

            migrationBuilder.DropColumn(
                name: "SocialMediaRemoved",
                table: "SystemsAdminClearances");

            migrationBuilder.DropColumn(
                name: "SophosCredentialsRemoved",
                table: "SystemsAdminClearances");

            migrationBuilder.AddColumn<string>(
                name: "AccessRemovalAsset",
                table: "SystemsAdminClearances",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AccessRemovalDate",
                table: "SystemsAdminClearances",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AccessRemovalReceivedBy",
                table: "SystemsAdminClearances",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AccessoriesAsset",
                table: "SystemsAdminClearances",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AccessoriesDate",
                table: "SystemsAdminClearances",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AccessoriesReceivedBy",
                table: "SystemsAdminClearances",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmailAsset",
                table: "SystemsAdminClearances",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EmailDate",
                table: "SystemsAdminClearances",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmailReceivedBy",
                table: "SystemsAdminClearances",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EndpointSecurityAsset",
                table: "SystemsAdminClearances",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EndpointSecurityDate",
                table: "SystemsAdminClearances",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EndpointSecurityReceivedBy",
                table: "SystemsAdminClearances",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LaptopAsset",
                table: "SystemsAdminClearances",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LaptopBagAsset",
                table: "SystemsAdminClearances",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LaptopBagDate",
                table: "SystemsAdminClearances",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LaptopBagReceivedBy",
                table: "SystemsAdminClearances",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LaptopDate",
                table: "SystemsAdminClearances",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LaptopReceivedBy",
                table: "SystemsAdminClearances",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LockerAsset",
                table: "SystemsAdminClearances",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LockerDate",
                table: "SystemsAdminClearances",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LockerReceivedBy",
                table: "SystemsAdminClearances",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SocialMediaAsset",
                table: "SystemsAdminClearances",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SocialMediaDate",
                table: "SystemsAdminClearances",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SocialMediaReceivedBy",
                table: "SystemsAdminClearances",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SophosAsset",
                table: "SystemsAdminClearances",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SophosDate",
                table: "SystemsAdminClearances",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SophosReceivedBy",
                table: "SystemsAdminClearances",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccessRemovalAsset",
                table: "SystemsAdminClearances");

            migrationBuilder.DropColumn(
                name: "AccessRemovalDate",
                table: "SystemsAdminClearances");

            migrationBuilder.DropColumn(
                name: "AccessRemovalReceivedBy",
                table: "SystemsAdminClearances");

            migrationBuilder.DropColumn(
                name: "AccessoriesAsset",
                table: "SystemsAdminClearances");

            migrationBuilder.DropColumn(
                name: "AccessoriesDate",
                table: "SystemsAdminClearances");

            migrationBuilder.DropColumn(
                name: "AccessoriesReceivedBy",
                table: "SystemsAdminClearances");

            migrationBuilder.DropColumn(
                name: "EmailAsset",
                table: "SystemsAdminClearances");

            migrationBuilder.DropColumn(
                name: "EmailDate",
                table: "SystemsAdminClearances");

            migrationBuilder.DropColumn(
                name: "EmailReceivedBy",
                table: "SystemsAdminClearances");

            migrationBuilder.DropColumn(
                name: "EndpointSecurityAsset",
                table: "SystemsAdminClearances");

            migrationBuilder.DropColumn(
                name: "EndpointSecurityDate",
                table: "SystemsAdminClearances");

            migrationBuilder.DropColumn(
                name: "EndpointSecurityReceivedBy",
                table: "SystemsAdminClearances");

            migrationBuilder.DropColumn(
                name: "LaptopAsset",
                table: "SystemsAdminClearances");

            migrationBuilder.DropColumn(
                name: "LaptopBagAsset",
                table: "SystemsAdminClearances");

            migrationBuilder.DropColumn(
                name: "LaptopBagDate",
                table: "SystemsAdminClearances");

            migrationBuilder.DropColumn(
                name: "LaptopBagReceivedBy",
                table: "SystemsAdminClearances");

            migrationBuilder.DropColumn(
                name: "LaptopDate",
                table: "SystemsAdminClearances");

            migrationBuilder.DropColumn(
                name: "LaptopReceivedBy",
                table: "SystemsAdminClearances");

            migrationBuilder.DropColumn(
                name: "LockerAsset",
                table: "SystemsAdminClearances");

            migrationBuilder.DropColumn(
                name: "LockerDate",
                table: "SystemsAdminClearances");

            migrationBuilder.DropColumn(
                name: "LockerReceivedBy",
                table: "SystemsAdminClearances");

            migrationBuilder.DropColumn(
                name: "SocialMediaAsset",
                table: "SystemsAdminClearances");

            migrationBuilder.DropColumn(
                name: "SocialMediaDate",
                table: "SystemsAdminClearances");

            migrationBuilder.DropColumn(
                name: "SocialMediaReceivedBy",
                table: "SystemsAdminClearances");

            migrationBuilder.DropColumn(
                name: "SophosAsset",
                table: "SystemsAdminClearances");

            migrationBuilder.DropColumn(
                name: "SophosDate",
                table: "SystemsAdminClearances");

            migrationBuilder.DropColumn(
                name: "SophosReceivedBy",
                table: "SystemsAdminClearances");

            migrationBuilder.AddColumn<bool>(
                name: "AccessRemoved",
                table: "SystemsAdminClearances",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AccessoriesReturned",
                table: "SystemsAdminClearances",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EmailDisabled",
                table: "SystemsAdminClearances",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EmailRedirected",
                table: "SystemsAdminClearances",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EndpointSecurityRemoved",
                table: "SystemsAdminClearances",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "LaptopBagReturned",
                table: "SystemsAdminClearances",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "LaptopReturned",
                table: "SystemsAdminClearances",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "LockerReturned",
                table: "SystemsAdminClearances",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SocialMediaRemoved",
                table: "SystemsAdminClearances",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SophosCredentialsRemoved",
                table: "SystemsAdminClearances",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }
    }
}
