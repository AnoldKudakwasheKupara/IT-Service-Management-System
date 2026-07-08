using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace IT_Service_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class AddImsIsoModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditProgrammes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    Standard = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Objectives = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditProgrammes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditProgrammes_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Competencies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Competencies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ComplianceObligations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Standard = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(3000)", maxLength: 3000, nullable: true),
                    Requirement = table.Column<string>(type: "nvarchar(3000)", maxLength: 3000, nullable: true),
                    Authority = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LegalReference = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    OwnerId = table.Column<int>(type: "int", nullable: true),
                    DepartmentId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastAssessedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NextReviewDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EvidenceNotes = table.Column<string>(type: "nvarchar(3000)", maxLength: 3000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComplianceObligations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ComplianceObligations_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ComplianceObligations_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ComplianceObligations_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Improvements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Standard = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(3000)", maxLength: 3000, nullable: false),
                    ExpectedBenefit = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ProposedById = table.Column<int>(type: "int", nullable: true),
                    OwnerId = table.Column<int>(type: "int", nullable: true),
                    DepartmentId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TargetDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ActualBenefit = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Improvements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Improvements_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Improvements_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Improvements_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Improvements_Users_ProposedById",
                        column: x => x.ProposedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IsoClauses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Standard = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClauseNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IsoClauses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IsoDocumentCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IsoDocumentCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IsoEvidences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Standard = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsoClause = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    StoredFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: true),
                    OriginalFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: true),
                    ContentType = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    StorageProvider = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    LinkedEntityType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    LinkedEntityId = table.Column<int>(type: "int", nullable: true),
                    UploadedById = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IsoEvidences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IsoEvidences_Users_UploadedById",
                        column: x => x.UploadedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IsoNotifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RecipientUserId = table.Column<int>(type: "int", nullable: true),
                    RecipientId = table.Column<int>(type: "int", nullable: true),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Url = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    RelatedEntityType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    RelatedEntityId = table.Column<int>(type: "int", nullable: true),
                    IsRead = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IsoNotifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IsoNotifications_Users_RecipientId",
                        column: x => x.RecipientId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ManagementReviews",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Standard = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MeetingDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Location = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ChairId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AgendaNotes = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Decisions = table.Column<string>(type: "nvarchar(max)", maxLength: 6000, nullable: true),
                    Conclusions = table.Column<string>(type: "nvarchar(max)", maxLength: 6000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ManagementReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ManagementReviews_Users_ChairId",
                        column: x => x.ChairId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ManagementReviews_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NonConformances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(3000)", maxLength: 3000, nullable: false),
                    Severity = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Standard = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DepartmentId = table.Column<int>(type: "int", nullable: true),
                    RaisedById = table.Column<int>(type: "int", nullable: true),
                    AssignedToId = table.Column<int>(type: "int", nullable: true),
                    DetectedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RootCause = table.Column<string>(type: "nvarchar(3000)", maxLength: 3000, nullable: true),
                    Evidence = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NonConformances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NonConformances_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NonConformances_Users_AssignedToId",
                        column: x => x.AssignedToId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NonConformances_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NonConformances_Users_RaisedById",
                        column: x => x.RaisedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Objectives",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Standard = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DepartmentId = table.Column<int>(type: "int", nullable: true),
                    OwnerId = table.Column<int>(type: "int", nullable: true),
                    TargetValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Unit = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    BaselineValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CurrentValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Direction = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Frequency = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Objectives", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Objectives_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Objectives_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Objectives_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Opportunities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2500)", maxLength: 2500, nullable: true),
                    Standard = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Benefit = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Likelihood = table.Column<int>(type: "int", nullable: false),
                    BenefitScore = table.Column<int>(type: "int", nullable: false),
                    ActionPlan = table.Column<string>(type: "nvarchar(3000)", maxLength: 3000, nullable: true),
                    OwnerId = table.Column<int>(type: "int", nullable: true),
                    DepartmentId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TargetDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Opportunities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Opportunities_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Opportunities_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Opportunities_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Risks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Standard = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2500)", maxLength: 2500, nullable: true),
                    AssetId = table.Column<int>(type: "int", nullable: true),
                    Threat = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Vulnerability = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Likelihood = table.Column<int>(type: "int", nullable: false),
                    Impact = table.Column<int>(type: "int", nullable: false),
                    Treatment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TreatmentPlan = table.Column<string>(type: "nvarchar(3000)", maxLength: 3000, nullable: true),
                    OwnerId = table.Column<int>(type: "int", nullable: true),
                    DepartmentId = table.Column<int>(type: "int", nullable: true),
                    ResidualLikelihood = table.Column<int>(type: "int", nullable: true),
                    ResidualImpact = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReviewDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Risks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Risks_Assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Risks_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Risks_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Risks_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Suppliers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContactName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    ProductsServices = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ApprovedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ContractStart = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ContractEnd = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CertificateName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CertificateExpiry = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Suppliers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Suppliers_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Audits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Standard = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Scope = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Objectives = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Criteria = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    AuditProgrammeId = table.Column<int>(type: "int", nullable: true),
                    DepartmentId = table.Column<int>(type: "int", nullable: true),
                    LeadAuditorId = table.Column<int>(type: "int", nullable: true),
                    PlannedStartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PlannedEndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ActualStartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ActualEndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(3000)", maxLength: 3000, nullable: true),
                    Conclusion = table.Column<string>(type: "nvarchar(3000)", maxLength: 3000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Audits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Audits_AuditProgrammes_AuditProgrammeId",
                        column: x => x.AuditProgrammeId,
                        principalTable: "AuditProgrammes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Audits_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Audits_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Audits_Users_LeadAuditorId",
                        column: x => x.LeadAuditorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserCompetencies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompetencyId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Level = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RequiredLevel = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AssessedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AssessedById = table.Column<int>(type: "int", nullable: true),
                    ExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserCompetencies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserCompetencies_Competencies_CompetencyId",
                        column: x => x.CompetencyId,
                        principalTable: "Competencies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserCompetencies_Users_AssessedById",
                        column: x => x.AssessedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserCompetencies_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ManagementReviewActions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ManagementReviewId = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    AssignedToId = table.Column<int>(type: "int", nullable: true),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompletedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ManagementReviewActions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ManagementReviewActions_ManagementReviews_ManagementReviewId",
                        column: x => x.ManagementReviewId,
                        principalTable: "ManagementReviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ManagementReviewActions_Users_AssignedToId",
                        column: x => x.AssignedToId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ManagementReviewAttendees",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ManagementReviewId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Present = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ManagementReviewAttendees", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ManagementReviewAttendees_ManagementReviews_ManagementReviewId",
                        column: x => x.ManagementReviewId,
                        principalTable: "ManagementReviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ManagementReviewAttendees_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ManagementReviewInputs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ManagementReviewId = table.Column<int>(type: "int", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Sequence = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ManagementReviewInputs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ManagementReviewInputs_ManagementReviews_ManagementReviewId",
                        column: x => x.ManagementReviewId,
                        principalTable: "ManagementReviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Capas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SourceReference = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Standard = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(3000)", maxLength: 3000, nullable: false),
                    Containment = table.Column<string>(type: "nvarchar(3000)", maxLength: 3000, nullable: true),
                    Correction = table.Column<string>(type: "nvarchar(3000)", maxLength: 3000, nullable: true),
                    RootCause = table.Column<string>(type: "nvarchar(3000)", maxLength: 3000, nullable: true),
                    CorrectiveAction = table.Column<string>(type: "nvarchar(3000)", maxLength: 3000, nullable: true),
                    PreventiveAction = table.Column<string>(type: "nvarchar(3000)", maxLength: 3000, nullable: true),
                    ResponsibleId = table.Column<int>(type: "int", nullable: true),
                    DepartmentId = table.Column<int>(type: "int", nullable: true),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    VerificationNotes = table.Column<string>(type: "nvarchar(3000)", maxLength: 3000, nullable: true),
                    VerifiedById = table.Column<int>(type: "int", nullable: true),
                    VerifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EffectivenessReview = table.Column<string>(type: "nvarchar(3000)", maxLength: 3000, nullable: true),
                    EffectivenessReviewDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Escalated = table.Column<bool>(type: "bit", nullable: false),
                    EscalatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NonConformanceId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Capas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Capas_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Capas_NonConformances_NonConformanceId",
                        column: x => x.NonConformanceId,
                        principalTable: "NonConformances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Capas_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Capas_Users_ResponsibleId",
                        column: x => x.ResponsibleId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Capas_Users_VerifiedById",
                        column: x => x.VerifiedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ObjectiveMeasurements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ObjectiveId = table.Column<int>(type: "int", nullable: false),
                    PeriodLabel = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Value = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    RecordedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RecordedById = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ObjectiveMeasurements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ObjectiveMeasurements_Objectives_ObjectiveId",
                        column: x => x.ObjectiveId,
                        principalTable: "Objectives",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ObjectiveMeasurements_Users_RecordedById",
                        column: x => x.RecordedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SupplierEvaluations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SupplierId = table.Column<int>(type: "int", nullable: false),
                    EvaluationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Period = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    QualityScore = table.Column<int>(type: "int", nullable: false),
                    DeliveryScore = table.Column<int>(type: "int", nullable: false),
                    PricingScore = table.Column<int>(type: "int", nullable: false),
                    SupportScore = table.Column<int>(type: "int", nullable: false),
                    ComplianceScore = table.Column<int>(type: "int", nullable: false),
                    Comments = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    EvaluatedById = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierEvaluations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierEvaluations_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierEvaluations_Users_EvaluatedById",
                        column: x => x.EvaluatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AuditChecklistItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AuditId = table.Column<int>(type: "int", nullable: false),
                    ClauseReference = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Question = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Result = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Evidence = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Sequence = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditChecklistItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditChecklistItems_Audits_AuditId",
                        column: x => x.AuditId,
                        principalTable: "Audits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AuditTeamMembers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AuditId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    RoleOnTeam = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditTeamMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditTeamMembers_Audits_AuditId",
                        column: x => x.AuditId,
                        principalTable: "Audits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AuditTeamMembers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AuditFindings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AuditId = table.Column<int>(type: "int", nullable: true),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ClauseReference = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Evidence = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    DepartmentId = table.Column<int>(type: "int", nullable: true),
                    RaisedById = table.Column<int>(type: "int", nullable: true),
                    AssignedToId = table.Column<int>(type: "int", nullable: true),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CapaId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditFindings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditFindings_Audits_AuditId",
                        column: x => x.AuditId,
                        principalTable: "Audits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AuditFindings_Capas_CapaId",
                        column: x => x.CapaId,
                        principalTable: "Capas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AuditFindings_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AuditFindings_Users_AssignedToId",
                        column: x => x.AssignedToId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AuditFindings_Users_RaisedById",
                        column: x => x.RaisedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IsoDocumentAcknowledgements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IsoDocumentId = table.Column<int>(type: "int", nullable: false),
                    IsoDocumentVersionId = table.Column<int>(type: "int", nullable: true),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OpenedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DownloadedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AcknowledgedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Accepted = table.Column<bool>(type: "bit", nullable: false),
                    SignatureName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    SignatureHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    SignedIp = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    Comments = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IsoDocumentAcknowledgements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IsoDocumentAcknowledgements_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IsoDocumentApprovals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IsoDocumentId = table.Column<int>(type: "int", nullable: false),
                    IsoDocumentVersionId = table.Column<int>(type: "int", nullable: true),
                    Stage = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    ApproverId = table.Column<int>(type: "int", nullable: true),
                    ApproverRole = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Decision = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Comments = table.Column<string>(type: "nvarchar(1500)", maxLength: 1500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DecisionAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IsoDocumentApprovals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IsoDocumentApprovals_Users_ApproverId",
                        column: x => x.ApproverId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IsoDocumentDistributions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IsoDocumentId = table.Column<int>(type: "int", nullable: false),
                    TargetType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    DepartmentId = table.Column<int>(type: "int", nullable: true),
                    RoleName = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    RequiresAcknowledgement = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IsoDocumentDistributions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IsoDocumentDistributions_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IsoDocumentDistributions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IsoDocumentReviews",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IsoDocumentId = table.Column<int>(type: "int", nullable: false),
                    ScheduledDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ActualDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewerId = table.Column<int>(type: "int", nullable: true),
                    Outcome = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    NextReviewDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IsoDocumentReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IsoDocumentReviews_Users_ReviewerId",
                        column: x => x.ReviewerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IsoDocuments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentNumber = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CategoryId = table.Column<int>(type: "int", nullable: true),
                    DepartmentId = table.Column<int>(type: "int", nullable: true),
                    OwnerId = table.Column<int>(type: "int", nullable: true),
                    ApproverId = table.Column<int>(type: "int", nullable: true),
                    Standard = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsoClause = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Classification = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CurrentVersion = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IssueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewFrequency = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReviewDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Keywords = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    Summary = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CurrentVersionId = table.Column<int>(type: "int", nullable: true),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false),
                    ArchivedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PublishedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IsoDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IsoDocuments_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IsoDocuments_IsoDocumentCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "IsoDocumentCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IsoDocuments_Users_ApproverId",
                        column: x => x.ApproverId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IsoDocuments_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IsoDocuments_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IsoDocumentVersions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IsoDocumentId = table.Column<int>(type: "int", nullable: false),
                    VersionNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RevisionNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    StoredFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: true),
                    OriginalFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: true),
                    ContentType = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    StorageProvider = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsCurrent = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedById = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IsoDocumentVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IsoDocumentVersions_IsoDocuments_IsoDocumentId",
                        column: x => x.IsoDocumentId,
                        principalTable: "IsoDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IsoDocumentVersions_Users_ApprovedById",
                        column: x => x.ApprovedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IsoDocumentVersions_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TrainingCourses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Standard = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Provider = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DurationHours = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    LinkedDocumentId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrainingCourses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrainingCourses_IsoDocuments_LinkedDocumentId",
                        column: x => x.LinkedDocumentId,
                        principalTable: "IsoDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TrainingCourses_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TrainingRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TrainingCourseId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    ScheduledDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Score = table.Column<int>(type: "int", nullable: true),
                    CertificateName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CertificateExpiry = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrainingRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrainingRecords_TrainingCourses_TrainingCourseId",
                        column: x => x.TrainingCourseId,
                        principalTable: "TrainingCourses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TrainingRecords_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "IsoClauses",
                columns: new[] { "Id", "ClauseNumber", "Description", "Standard", "Title" },
                values: new object[,]
                {
                    { 1, "4", null, "Iso9001", "Context of the organization" },
                    { 2, "5", null, "Iso9001", "Leadership" },
                    { 3, "6", null, "Iso9001", "Planning" },
                    { 4, "6.2", null, "Iso9001", "Quality objectives and planning to achieve them" },
                    { 5, "7", null, "Iso9001", "Support" },
                    { 6, "7.2", null, "Iso9001", "Competence" },
                    { 7, "7.5", null, "Iso9001", "Documented information" },
                    { 8, "8", null, "Iso9001", "Operation" },
                    { 9, "8.4", null, "Iso9001", "Control of externally provided processes, products and services" },
                    { 10, "8.5", null, "Iso9001", "Production and service provision" },
                    { 11, "9", null, "Iso9001", "Performance evaluation" },
                    { 12, "9.2", null, "Iso9001", "Internal audit" },
                    { 13, "9.3", null, "Iso9001", "Management review" },
                    { 14, "10", null, "Iso9001", "Improvement" },
                    { 15, "10.2", null, "Iso9001", "Nonconformity and corrective action" },
                    { 16, "4", null, "Iso27001", "Context of the organization" },
                    { 17, "5", null, "Iso27001", "Leadership" },
                    { 18, "6", null, "Iso27001", "Planning" },
                    { 19, "6.1.2", null, "Iso27001", "Information security risk assessment" },
                    { 20, "6.1.3", null, "Iso27001", "Information security risk treatment" },
                    { 21, "7", null, "Iso27001", "Support" },
                    { 22, "7.5", null, "Iso27001", "Documented information" },
                    { 23, "8", null, "Iso27001", "Operation" },
                    { 24, "9", null, "Iso27001", "Performance evaluation" },
                    { 25, "9.2", null, "Iso27001", "Internal audit" },
                    { 26, "9.3", null, "Iso27001", "Management review" },
                    { 27, "10", null, "Iso27001", "Improvement" },
                    { 28, "10.2", null, "Iso27001", "Nonconformity and corrective action" }
                });

            migrationBuilder.InsertData(
                table: "IsoDocumentCategories",
                columns: new[] { "Id", "Code", "Description", "IsActive", "Name" },
                values: new object[,]
                {
                    { 1, "QMS", null, true, "Quality Management" },
                    { 2, "ISMS", null, true, "Information Security" },
                    { 3, "HR", null, true, "Human Resources" },
                    { 4, "IT", null, true, "Information Technology" },
                    { 5, "OPS", null, true, "Operations" },
                    { 6, "FIN", null, true, "Finance" },
                    { 7, "HSE", null, true, "Health, Safety & Environment" },
                    { 8, "GEN", null, true, "General / Administration" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditChecklistItems_AuditId",
                table: "AuditChecklistItems",
                column: "AuditId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditFindings_AssignedToId",
                table: "AuditFindings",
                column: "AssignedToId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditFindings_AuditId",
                table: "AuditFindings",
                column: "AuditId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditFindings_CapaId",
                table: "AuditFindings",
                column: "CapaId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditFindings_DepartmentId",
                table: "AuditFindings",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditFindings_RaisedById",
                table: "AuditFindings",
                column: "RaisedById");

            migrationBuilder.CreateIndex(
                name: "IX_AuditProgrammes_CreatedById",
                table: "AuditProgrammes",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_Audits_AuditProgrammeId",
                table: "Audits",
                column: "AuditProgrammeId");

            migrationBuilder.CreateIndex(
                name: "IX_Audits_CreatedById",
                table: "Audits",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_Audits_DepartmentId",
                table: "Audits",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Audits_LeadAuditorId",
                table: "Audits",
                column: "LeadAuditorId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditTeamMembers_AuditId",
                table: "AuditTeamMembers",
                column: "AuditId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditTeamMembers_UserId",
                table: "AuditTeamMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Capas_CreatedById",
                table: "Capas",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_Capas_DepartmentId",
                table: "Capas",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Capas_NonConformanceId",
                table: "Capas",
                column: "NonConformanceId");

            migrationBuilder.CreateIndex(
                name: "IX_Capas_ResponsibleId",
                table: "Capas",
                column: "ResponsibleId");

            migrationBuilder.CreateIndex(
                name: "IX_Capas_VerifiedById",
                table: "Capas",
                column: "VerifiedById");

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceObligations_CreatedById",
                table: "ComplianceObligations",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceObligations_DepartmentId",
                table: "ComplianceObligations",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceObligations_OwnerId",
                table: "ComplianceObligations",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Improvements_CreatedById",
                table: "Improvements",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_Improvements_DepartmentId",
                table: "Improvements",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Improvements_OwnerId",
                table: "Improvements",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Improvements_ProposedById",
                table: "Improvements",
                column: "ProposedById");

            migrationBuilder.CreateIndex(
                name: "IX_IsoClauses_Standard_ClauseNumber",
                table: "IsoClauses",
                columns: new[] { "Standard", "ClauseNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_IsoDocumentAcknowledgements_IsoDocumentId",
                table: "IsoDocumentAcknowledgements",
                column: "IsoDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_IsoDocumentAcknowledgements_IsoDocumentVersionId",
                table: "IsoDocumentAcknowledgements",
                column: "IsoDocumentVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_IsoDocumentAcknowledgements_UserId",
                table: "IsoDocumentAcknowledgements",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_IsoDocumentApprovals_ApproverId",
                table: "IsoDocumentApprovals",
                column: "ApproverId");

            migrationBuilder.CreateIndex(
                name: "IX_IsoDocumentApprovals_IsoDocumentId",
                table: "IsoDocumentApprovals",
                column: "IsoDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_IsoDocumentApprovals_IsoDocumentVersionId",
                table: "IsoDocumentApprovals",
                column: "IsoDocumentVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_IsoDocumentDistributions_DepartmentId",
                table: "IsoDocumentDistributions",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_IsoDocumentDistributions_IsoDocumentId",
                table: "IsoDocumentDistributions",
                column: "IsoDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_IsoDocumentDistributions_UserId",
                table: "IsoDocumentDistributions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_IsoDocumentReviews_IsoDocumentId",
                table: "IsoDocumentReviews",
                column: "IsoDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_IsoDocumentReviews_ReviewerId",
                table: "IsoDocumentReviews",
                column: "ReviewerId");

            migrationBuilder.CreateIndex(
                name: "IX_IsoDocuments_ApproverId",
                table: "IsoDocuments",
                column: "ApproverId");

            migrationBuilder.CreateIndex(
                name: "IX_IsoDocuments_CategoryId",
                table: "IsoDocuments",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_IsoDocuments_CreatedById",
                table: "IsoDocuments",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_IsoDocuments_CurrentVersionId",
                table: "IsoDocuments",
                column: "CurrentVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_IsoDocuments_DepartmentId",
                table: "IsoDocuments",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_IsoDocuments_DocumentNumber",
                table: "IsoDocuments",
                column: "DocumentNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IsoDocuments_OwnerId",
                table: "IsoDocuments",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_IsoDocumentVersions_ApprovedById",
                table: "IsoDocumentVersions",
                column: "ApprovedById");

            migrationBuilder.CreateIndex(
                name: "IX_IsoDocumentVersions_CreatedById",
                table: "IsoDocumentVersions",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_IsoDocumentVersions_IsoDocumentId",
                table: "IsoDocumentVersions",
                column: "IsoDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_IsoEvidences_UploadedById",
                table: "IsoEvidences",
                column: "UploadedById");

            migrationBuilder.CreateIndex(
                name: "IX_IsoNotifications_RecipientId",
                table: "IsoNotifications",
                column: "RecipientId");

            migrationBuilder.CreateIndex(
                name: "IX_ManagementReviewActions_AssignedToId",
                table: "ManagementReviewActions",
                column: "AssignedToId");

            migrationBuilder.CreateIndex(
                name: "IX_ManagementReviewActions_ManagementReviewId",
                table: "ManagementReviewActions",
                column: "ManagementReviewId");

            migrationBuilder.CreateIndex(
                name: "IX_ManagementReviewAttendees_ManagementReviewId",
                table: "ManagementReviewAttendees",
                column: "ManagementReviewId");

            migrationBuilder.CreateIndex(
                name: "IX_ManagementReviewAttendees_UserId",
                table: "ManagementReviewAttendees",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ManagementReviewInputs_ManagementReviewId",
                table: "ManagementReviewInputs",
                column: "ManagementReviewId");

            migrationBuilder.CreateIndex(
                name: "IX_ManagementReviews_ChairId",
                table: "ManagementReviews",
                column: "ChairId");

            migrationBuilder.CreateIndex(
                name: "IX_ManagementReviews_CreatedById",
                table: "ManagementReviews",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_NonConformances_AssignedToId",
                table: "NonConformances",
                column: "AssignedToId");

            migrationBuilder.CreateIndex(
                name: "IX_NonConformances_CreatedById",
                table: "NonConformances",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_NonConformances_DepartmentId",
                table: "NonConformances",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_NonConformances_RaisedById",
                table: "NonConformances",
                column: "RaisedById");

            migrationBuilder.CreateIndex(
                name: "IX_ObjectiveMeasurements_ObjectiveId",
                table: "ObjectiveMeasurements",
                column: "ObjectiveId");

            migrationBuilder.CreateIndex(
                name: "IX_ObjectiveMeasurements_RecordedById",
                table: "ObjectiveMeasurements",
                column: "RecordedById");

            migrationBuilder.CreateIndex(
                name: "IX_Objectives_CreatedById",
                table: "Objectives",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_Objectives_DepartmentId",
                table: "Objectives",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Objectives_OwnerId",
                table: "Objectives",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Opportunities_CreatedById",
                table: "Opportunities",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_Opportunities_DepartmentId",
                table: "Opportunities",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Opportunities_OwnerId",
                table: "Opportunities",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Risks_AssetId",
                table: "Risks",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_Risks_CreatedById",
                table: "Risks",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_Risks_DepartmentId",
                table: "Risks",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Risks_OwnerId",
                table: "Risks",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierEvaluations_EvaluatedById",
                table: "SupplierEvaluations",
                column: "EvaluatedById");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierEvaluations_SupplierId",
                table: "SupplierEvaluations",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_CreatedById",
                table: "Suppliers",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_TrainingCourses_CreatedById",
                table: "TrainingCourses",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_TrainingCourses_LinkedDocumentId",
                table: "TrainingCourses",
                column: "LinkedDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_TrainingRecords_TrainingCourseId",
                table: "TrainingRecords",
                column: "TrainingCourseId");

            migrationBuilder.CreateIndex(
                name: "IX_TrainingRecords_UserId",
                table: "TrainingRecords",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserCompetencies_AssessedById",
                table: "UserCompetencies",
                column: "AssessedById");

            migrationBuilder.CreateIndex(
                name: "IX_UserCompetencies_CompetencyId",
                table: "UserCompetencies",
                column: "CompetencyId");

            migrationBuilder.CreateIndex(
                name: "IX_UserCompetencies_UserId",
                table: "UserCompetencies",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_IsoDocumentAcknowledgements_IsoDocumentVersions_IsoDocumentVersionId",
                table: "IsoDocumentAcknowledgements",
                column: "IsoDocumentVersionId",
                principalTable: "IsoDocumentVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_IsoDocumentAcknowledgements_IsoDocuments_IsoDocumentId",
                table: "IsoDocumentAcknowledgements",
                column: "IsoDocumentId",
                principalTable: "IsoDocuments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_IsoDocumentApprovals_IsoDocumentVersions_IsoDocumentVersionId",
                table: "IsoDocumentApprovals",
                column: "IsoDocumentVersionId",
                principalTable: "IsoDocumentVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_IsoDocumentApprovals_IsoDocuments_IsoDocumentId",
                table: "IsoDocumentApprovals",
                column: "IsoDocumentId",
                principalTable: "IsoDocuments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_IsoDocumentDistributions_IsoDocuments_IsoDocumentId",
                table: "IsoDocumentDistributions",
                column: "IsoDocumentId",
                principalTable: "IsoDocuments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_IsoDocumentReviews_IsoDocuments_IsoDocumentId",
                table: "IsoDocumentReviews",
                column: "IsoDocumentId",
                principalTable: "IsoDocuments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_IsoDocuments_IsoDocumentVersions_CurrentVersionId",
                table: "IsoDocuments",
                column: "CurrentVersionId",
                principalTable: "IsoDocumentVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_IsoDocuments_IsoDocumentVersions_CurrentVersionId",
                table: "IsoDocuments");

            migrationBuilder.DropTable(
                name: "AuditChecklistItems");

            migrationBuilder.DropTable(
                name: "AuditFindings");

            migrationBuilder.DropTable(
                name: "AuditTeamMembers");

            migrationBuilder.DropTable(
                name: "ComplianceObligations");

            migrationBuilder.DropTable(
                name: "Improvements");

            migrationBuilder.DropTable(
                name: "IsoClauses");

            migrationBuilder.DropTable(
                name: "IsoDocumentAcknowledgements");

            migrationBuilder.DropTable(
                name: "IsoDocumentApprovals");

            migrationBuilder.DropTable(
                name: "IsoDocumentDistributions");

            migrationBuilder.DropTable(
                name: "IsoDocumentReviews");

            migrationBuilder.DropTable(
                name: "IsoEvidences");

            migrationBuilder.DropTable(
                name: "IsoNotifications");

            migrationBuilder.DropTable(
                name: "ManagementReviewActions");

            migrationBuilder.DropTable(
                name: "ManagementReviewAttendees");

            migrationBuilder.DropTable(
                name: "ManagementReviewInputs");

            migrationBuilder.DropTable(
                name: "ObjectiveMeasurements");

            migrationBuilder.DropTable(
                name: "Opportunities");

            migrationBuilder.DropTable(
                name: "Risks");

            migrationBuilder.DropTable(
                name: "SupplierEvaluations");

            migrationBuilder.DropTable(
                name: "TrainingRecords");

            migrationBuilder.DropTable(
                name: "UserCompetencies");

            migrationBuilder.DropTable(
                name: "Capas");

            migrationBuilder.DropTable(
                name: "Audits");

            migrationBuilder.DropTable(
                name: "ManagementReviews");

            migrationBuilder.DropTable(
                name: "Objectives");

            migrationBuilder.DropTable(
                name: "Suppliers");

            migrationBuilder.DropTable(
                name: "TrainingCourses");

            migrationBuilder.DropTable(
                name: "Competencies");

            migrationBuilder.DropTable(
                name: "NonConformances");

            migrationBuilder.DropTable(
                name: "AuditProgrammes");

            migrationBuilder.DropTable(
                name: "IsoDocumentVersions");

            migrationBuilder.DropTable(
                name: "IsoDocuments");

            migrationBuilder.DropTable(
                name: "IsoDocumentCategories");
        }
    }
}
