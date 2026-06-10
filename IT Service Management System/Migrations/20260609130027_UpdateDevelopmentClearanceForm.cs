using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IT_Service_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class UpdateDevelopmentClearanceForm : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BitbucketRemoved",
                table: "DevelopmentClearances");

            migrationBuilder.DropColumn(
                name: "ManageEngineRemoved",
                table: "DevelopmentClearances");

            migrationBuilder.DropColumn(
                name: "ProjectManagementRemoved",
                table: "DevelopmentClearances");

            migrationBuilder.AddColumn<string>(
                name: "BitbucketAsset",
                table: "DevelopmentClearances",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "BitbucketDate",
                table: "DevelopmentClearances",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BitbucketReceivedBy",
                table: "DevelopmentClearances",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ManageEngineAsset",
                table: "DevelopmentClearances",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ManageEngineDate",
                table: "DevelopmentClearances",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ManageEngineReceivedBy",
                table: "DevelopmentClearances",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProjectManagementAsset",
                table: "DevelopmentClearances",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProjectManagementDate",
                table: "DevelopmentClearances",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProjectManagementReceivedBy",
                table: "DevelopmentClearances",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BitbucketAsset",
                table: "DevelopmentClearances");

            migrationBuilder.DropColumn(
                name: "BitbucketDate",
                table: "DevelopmentClearances");

            migrationBuilder.DropColumn(
                name: "BitbucketReceivedBy",
                table: "DevelopmentClearances");

            migrationBuilder.DropColumn(
                name: "ManageEngineAsset",
                table: "DevelopmentClearances");

            migrationBuilder.DropColumn(
                name: "ManageEngineDate",
                table: "DevelopmentClearances");

            migrationBuilder.DropColumn(
                name: "ManageEngineReceivedBy",
                table: "DevelopmentClearances");

            migrationBuilder.DropColumn(
                name: "ProjectManagementAsset",
                table: "DevelopmentClearances");

            migrationBuilder.DropColumn(
                name: "ProjectManagementDate",
                table: "DevelopmentClearances");

            migrationBuilder.DropColumn(
                name: "ProjectManagementReceivedBy",
                table: "DevelopmentClearances");

            migrationBuilder.AddColumn<bool>(
                name: "BitbucketRemoved",
                table: "DevelopmentClearances",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ManageEngineRemoved",
                table: "DevelopmentClearances",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ProjectManagementRemoved",
                table: "DevelopmentClearances",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }
    }
}
