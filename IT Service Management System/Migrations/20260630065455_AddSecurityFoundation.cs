using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IT_Service_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class AddSecurityFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FailedLoginCount",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastLoginAt",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LockoutEnd",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "MfaEnabled",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "PasswordChangedAt",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SecurityStamp",
                table: "Users",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AppConfigurations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PasswordMinLength = table.Column<int>(type: "int", nullable: false),
                    PasswordRequireUppercase = table.Column<bool>(type: "bit", nullable: false),
                    PasswordRequireLowercase = table.Column<bool>(type: "bit", nullable: false),
                    PasswordRequireDigit = table.Column<bool>(type: "bit", nullable: false),
                    PasswordRequireSpecial = table.Column<bool>(type: "bit", nullable: false),
                    PasswordExpiryDays = table.Column<int>(type: "int", nullable: false),
                    LockoutMaxFailedAttempts = table.Column<int>(type: "int", nullable: false),
                    LockoutDurationMinutes = table.Column<int>(type: "int", nullable: false),
                    SessionIdleTimeoutMinutes = table.Column<int>(type: "int", nullable: false),
                    MfaEnabled = table.Column<bool>(type: "bit", nullable: false),
                    MfaRequiredForAdmins = table.Column<bool>(type: "bit", nullable: false),
                    MfaOtpValidityMinutes = table.Column<int>(type: "int", nullable: false),
                    SmtpServer = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SmtpPort = table.Column<int>(type: "int", nullable: false),
                    SenderName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    SenderEmail = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    BackupEnabled = table.Column<bool>(type: "bit", nullable: false),
                    BackupFrequency = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    BackupTime = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    BackupRetentionCount = table.Column<int>(type: "int", nullable: false),
                    BackupPath = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    AlertEmailRecipients = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    AlertOnMultipleFailedLogins = table.Column<bool>(type: "bit", nullable: false),
                    AlertOnNewAdminAccount = table.Column<bool>(type: "bit", nullable: false),
                    AlertOnPrivilegeEscalation = table.Column<bool>(type: "bit", nullable: false),
                    AlertOnSuspiciousLocation = table.Column<bool>(type: "bit", nullable: false),
                    AlertOnLargeDataExport = table.Column<bool>(type: "bit", nullable: false),
                    AlertOnBackupFailure = table.Column<bool>(type: "bit", nullable: false),
                    AlertOnDatabaseFailure = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppConfigurations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    SessionToken = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    IpAddress = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Location = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Device = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RevokedReason = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserSessions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserSessions_SessionToken",
                table: "UserSessions",
                column: "SessionToken");

            migrationBuilder.CreateIndex(
                name: "IX_UserSessions_UserId_RevokedAt",
                table: "UserSessions",
                columns: new[] { "UserId", "RevokedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppConfigurations");

            migrationBuilder.DropTable(
                name: "UserSessions");

            migrationBuilder.DropColumn(
                name: "FailedLoginCount",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LastLoginAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LockoutEnd",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "MfaEnabled",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PasswordChangedAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SecurityStamp",
                table: "Users");
        }
    }
}
