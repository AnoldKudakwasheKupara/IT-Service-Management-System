using IT_Service_Management_System.Models;
using IT_Service_Management_System.Models.Efm;
using IT_Service_Management_System.Models.Itsm;
using IT_Service_Management_System.Models.Ims;
using Microsoft.EntityFrameworkCore;

namespace IT_Service_Management_System.DbContexts
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Ticket> Tickets { get; set; }
        public DbSet<TicketMessage> TicketMessages { get; set; }
        public DbSet<TicketAttachment> TicketAttachments { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<Asset> Assets { get; set; }
        public DbSet<AssetHistory> AssetHistories { get; set; }
        public DbSet<Department> Departments { get; set; }
        public DbSet<SSLCertificate> SSLCertificates { get; set; }
        public DbSet<PaymentSchedule> PaymentSchedules { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<Activity> Activities { get; set; }
        public DbSet<ActivityCategory> ActivityCategories { get; set; }
        public DbSet<ExitClearance> ExitClearances { get; set; }
        public DbSet<ClearanceWorkflow> ClearanceWorkflows { get; set; }
        public DbSet<ExitClearanceEmployeeDetails> ExitClearanceEmployeeDetails { get; set; }
        public DbSet<FinanceClearance> FinanceClearances { get; set; }
        public DbSet<SystemsAdminClearance> SystemsAdminClearances { get; set; }
        public DbSet<DevelopmentClearance> DevelopmentClearances { get; set; }
        public DbSet<StockHandoverItem> StockHandoverItems { get; set; }
        public DbSet<MaintenanceRecord> MaintenanceRecords { get; set; }
        public DbSet<SupervisorApproval> SupervisorApprovals { get; set; }
        public DbSet<HodApproval> HodApprovals { get; set; }
        public DbSet<HrApproval> HrApprovals { get; set; }
        public DbSet<ExitInterview> ExitInterviews { get; set; }
        public DbSet<EngagementStayInterview> EngagementStayInterviews { get; set; }
        public DbSet<TalentIdentification> TalentIdentifications { get; set; }
        public DbSet<TalentDirectReportAssessment> TalentDirectReportAssessments { get; set; }
        public DbSet<TalentDevelopmentAction> TalentDevelopmentActions { get; set; }
        public DbSet<UserAccessRight> UserAccessRights { get; set; }
        public DbSet<UserAccessRightItem> UserAccessRightItems { get; set; }
        public DbSet<UserSession> UserSessions { get; set; }
        public DbSet<AppConfiguration> AppConfigurations { get; set; }
        public DbSet<CannedResponse> CannedResponses { get; set; }
        public DbSet<EmployeeFile> EmployeeFiles { get; set; }

        // ── Employee File Management (EFM) ─────────────────────────────────────────
        public DbSet<DocumentFolder> DocumentFolders { get; set; }
        public DbSet<DocumentCategory> DocumentCategories { get; set; }
        public DbSet<RequiredDocument> RequiredDocuments { get; set; }
        public DbSet<EmployeeDocument> EmployeeDocuments { get; set; }
        public DbSet<DocumentVersion> DocumentVersions { get; set; }
        public DbSet<DocumentTag> DocumentTags { get; set; }
        public DbSet<DocumentTagMap> DocumentTagMaps { get; set; }
        public DbSet<DocumentAuditLog> DocumentAuditLogs { get; set; }
        public DbSet<DocumentShare> DocumentShares { get; set; }
        public DbSet<DocumentApproval> DocumentApprovals { get; set; }
        public DbSet<DocumentComment> DocumentComments { get; set; }
        public DbSet<DocumentNotification> DocumentNotifications { get; set; }
        public DbSet<StorageProvider> StorageProviders { get; set; }
        public DbSet<RetentionPolicy> RetentionPolicies { get; set; }
        public DbSet<ExpiryAlert> ExpiryAlerts { get; set; }
        public DbSet<DocumentRequest> DocumentRequests { get; set; }

        // ── ITSM / ITIL (CMDB, Problems, Changes) + SLA ────────────────────────────
        public DbSet<ConfigurationItem> ConfigurationItems { get; set; }
        public DbSet<Problem> Problems { get; set; }
        public DbSet<ChangeRequest> ChangeRequests { get; set; }
        public DbSet<SlaPolicy> SlaPolicies { get; set; }
        public DbSet<SlaCalendar> SlaCalendars { get; set; }
        public DbSet<SlaHoliday> SlaHolidays { get; set; }
        public DbSet<SlaEvent> SlaEvents { get; set; }
        public DbSet<ServiceCatalogItem> ServiceCatalogItems { get; set; }
        public DbSet<ServiceRequest> ServiceRequests { get; set; }
        public DbSet<MajorIncident> MajorIncidents { get; set; }
        public DbSet<MajorIncidentAffectedItem> MajorIncidentAffectedItems { get; set; }
        public DbSet<MajorIncidentTimelineEntry> MajorIncidentTimelineEntries { get; set; }
        public DbSet<MajorIncidentUpdate> MajorIncidentUpdates { get; set; }
        public DbSet<MajorIncidentFollowUp> MajorIncidentFollowUps { get; set; }

        // ── Meeting Minutes (Operations) ───────────────────────────────────────────
        public DbSet<Meeting> Meetings { get; set; }
        public DbSet<MeetingRosterMember> MeetingRosterMembers { get; set; }
        public DbSet<MeetingAttendance> MeetingAttendances { get; set; }
        public DbSet<ActionItem> ActionItems { get; set; }
        public DbSet<ActionItemUpdate> ActionItemUpdates { get; set; }

        // ── IMS / ISO (ISO 9001:2015 & ISO/IEC 27001:2022) ─────────────────────────
        // Document Control
        public DbSet<IsoDocumentCategory> IsoDocumentCategories { get; set; }
        public DbSet<IsoDocument> IsoDocuments { get; set; }
        public DbSet<IsoDocumentVersion> IsoDocumentVersions { get; set; }
        public DbSet<IsoDocumentApproval> IsoDocumentApprovals { get; set; }
        public DbSet<IsoDocumentAcknowledgement> IsoDocumentAcknowledgements { get; set; }
        public DbSet<IsoDocumentDistribution> IsoDocumentDistributions { get; set; }
        public DbSet<IsoDocumentReview> IsoDocumentReviews { get; set; }
        // Internal Audits & Findings
        public DbSet<AuditProgramme> AuditProgrammes { get; set; }
        public DbSet<Audit> Audits { get; set; }
        public DbSet<AuditTeamMember> AuditTeamMembers { get; set; }
        public DbSet<AuditChecklistItem> AuditChecklistItems { get; set; }
        public DbSet<AuditFinding> AuditFindings { get; set; }
        // CAPA & Non-Conformance
        public DbSet<Capa> Capas { get; set; }
        public DbSet<NonConformance> NonConformances { get; set; }
        // Incident Management (Improvement)
        public DbSet<Incident> Incidents { get; set; }
        public DbSet<IncidentInvestigator> IncidentInvestigators { get; set; }
        public DbSet<IncidentDamage> IncidentDamages { get; set; }
        public DbSet<IncidentAction> IncidentActions { get; set; }
        public DbSet<IncidentAttachment> IncidentAttachments { get; set; }
        // Risk & Opportunity
        public DbSet<Risk> Risks { get; set; }
        public DbSet<Opportunity> Opportunities { get; set; }
        // Suppliers
        public DbSet<Supplier> Suppliers { get; set; }
        public DbSet<SupplierEvaluation> SupplierEvaluations { get; set; }
        // Training & Competency
        public DbSet<TrainingCourse> TrainingCourses { get; set; }
        public DbSet<TrainingRecord> TrainingRecords { get; set; }
        public DbSet<Competency> Competencies { get; set; }
        public DbSet<UserCompetency> UserCompetencies { get; set; }
        // Management Review
        public DbSet<ManagementReview> ManagementReviews { get; set; }
        public DbSet<ManagementReviewAttendee> ManagementReviewAttendees { get; set; }
        public DbSet<ManagementReviewInput> ManagementReviewInputs { get; set; }
        public DbSet<ManagementReviewAction> ManagementReviewActions { get; set; }
        // Objectives & KPIs
        public DbSet<Objective> Objectives { get; set; }
        public DbSet<ObjectiveMeasurement> ObjectiveMeasurements { get; set; }
        // Compliance, Improvement, Evidence, Reference
        public DbSet<ComplianceObligation> ComplianceObligations { get; set; }
        public DbSet<Improvement> Improvements { get; set; }
        public DbSet<IsoEvidence> IsoEvidences { get; set; }
        public DbSet<IsoClause> IsoClauses { get; set; }
        public DbSet<IsoNotification> IsoNotifications { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Ticket>()
                .Property(t => t.Status)
                .HasConversion<string>();

            modelBuilder.Entity<Ticket>()
                .Property(t => t.Priority)
                .HasConversion<string>();

            modelBuilder.Entity<User>()
                .Property(u => u.Role)
                .HasConversion<string>();

            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            modelBuilder.Entity<Ticket>()
                .HasOne(t => t.CreatedBy)
                .WithMany(u => u.TicketsCreated)
                .HasForeignKey(t => t.CreatedById)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Ticket>()
                .HasOne(t => t.AssignedTo)
                .WithMany()
                .HasForeignKey(t => t.AssignedToId)
                .OnDelete(DeleteBehavior.Restrict);

            // ✅ MESSAGE RELATIONSHIPS
            modelBuilder.Entity<TicketMessage>()
                .HasOne(m => m.Ticket)
                .WithMany(t => t.Messages)
                .HasForeignKey(m => m.TicketId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<TicketMessage>()
                .HasOne(m => m.Sender)
                .WithMany(u => u.Messages)
                .HasForeignKey(m => m.SenderId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<TicketAttachment>()
                .HasOne(a => a.Ticket)
                .WithMany(t => t.Attachments)
                .HasForeignKey(a => a.TicketId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<TicketAttachment>()
                .HasOne(a => a.TicketMessage)
                .WithMany(m => m.Attachments)
                .HasForeignKey(a => a.TicketMessageId)
                .OnDelete(DeleteBehavior.Restrict);

            // User -> Department
            modelBuilder.Entity<User>()
                .HasOne(u => u.Department)
                .WithMany(d => d.Users)
                .HasForeignKey(u => u.DepartmentId)
                .OnDelete(DeleteBehavior.Restrict);

            // Department -> HOD
            modelBuilder.Entity<Department>()
                .HasOne(d => d.Hod)
                .WithMany()
                .HasForeignKey(d => d.HodId)
                .OnDelete(DeleteBehavior.Restrict);

            // User -> Supervisor
            modelBuilder.Entity<User>()
                .HasOne(u => u.Supervisor)
                .WithMany(u => u.Subordinates)
                .HasForeignKey(u => u.SupervisorId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ClearanceWorkflow>()
                .HasOne(c => c.AssignedToUser)
                .WithMany()
                .HasForeignKey(c => c.AssignedToUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ExitClearance>()
                .HasOne(c => c.Employee)
                .WithMany()
                .HasForeignKey(c => c.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Department>()
                .HasOne(d => d.Hod)
                .WithMany()
                .HasForeignKey(d => d.HodId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<User>()
                .HasOne(u => u.Supervisor)
                .WithMany(u => u.Subordinates)
                .HasForeignKey(u => u.SupervisorId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<UserAccessRightItem>()
                .HasOne(u => u.UserAccessRight)
                .WithMany(h => h.Users)
                .HasForeignKey(u => u.UserAccessRightsId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<TicketAttachment>()
                .ToTable(t => t.HasCheckConstraint("CK_Attachment_Owner",
                    "[TicketId] IS NOT NULL OR [TicketMessageId] IS NOT NULL"));

            // Decimal precision — avoids silent truncation (and EF model-validation warnings).
            modelBuilder.Entity<Asset>().Property(a => a.PurchaseCost).HasPrecision(18, 2);
            modelBuilder.Entity<Payment>().Property(p => p.Amount).HasPrecision(18, 2);
            modelBuilder.Entity<PaymentSchedule>().Property(p => p.Amount).HasPrecision(18, 2);

            // Soft-delete: retain deleted tickets but hide them from normal queries.
            modelBuilder.Entity<Ticket>().HasQueryFilter(t => !t.IsDeleted);

            // UserSession -> User
            modelBuilder.Entity<UserSession>()
                .HasOne(s => s.User)
                .WithMany()
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UserSession>()
                .HasIndex(s => s.SessionToken);

            modelBuilder.Entity<UserSession>()
                .HasIndex(s => new { s.UserId, s.RevokedAt });

            // EmployeeFile -> User (employee). Cascade so a user's files are removed with them.
            modelBuilder.Entity<EmployeeFile>()
                .HasOne(f => f.Employee)
                .WithMany()
                .HasForeignKey(f => f.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<EmployeeFile>()
                .HasIndex(f => f.EmployeeId);

            // ── Employee File Management (EFM) relationships & indexes ──────────────
            modelBuilder.Entity<EmployeeDocument>().HasQueryFilter(d => !d.IsDeleted);
            modelBuilder.Entity<EmployeeDocument>()
                .HasOne(d => d.Employee).WithMany().HasForeignKey(d => d.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<EmployeeDocument>()
                .HasOne(d => d.Folder).WithMany(f => f.Documents).HasForeignKey(d => d.FolderId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<EmployeeDocument>()
                .HasOne(d => d.Category).WithMany(c => c.Documents).HasForeignKey(d => d.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<EmployeeDocument>()
                .HasOne(d => d.CurrentVersion).WithMany().HasForeignKey(d => d.CurrentVersionId)
                .OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<EmployeeDocument>().HasIndex(d => new { d.EmployeeId, d.FolderId });
            modelBuilder.Entity<EmployeeDocument>().HasIndex(d => d.CategoryId);
            modelBuilder.Entity<EmployeeDocument>().HasIndex(d => d.Status);
            modelBuilder.Entity<EmployeeDocument>().HasIndex(d => d.ExpiryDate);

            modelBuilder.Entity<DocumentVersion>()
                .HasOne(v => v.Document).WithMany(d => d.Versions).HasForeignKey(v => v.EmployeeDocumentId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<DocumentVersion>().HasIndex(v => v.EmployeeDocumentId);

            modelBuilder.Entity<DocumentTagMap>().HasKey(m => new { m.EmployeeDocumentId, m.DocumentTagId });
            modelBuilder.Entity<DocumentTagMap>()
                .HasOne(m => m.Document).WithMany(d => d.Tags).HasForeignKey(m => m.EmployeeDocumentId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<DocumentTagMap>()
                .HasOne(m => m.Tag).WithMany(t => t.Documents).HasForeignKey(m => m.DocumentTagId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<DocumentTag>().HasIndex(t => t.Name).IsUnique();

            modelBuilder.Entity<DocumentCategory>()
                .HasOne(c => c.DefaultFolder).WithMany().HasForeignKey(c => c.DefaultFolderId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<RequiredDocument>()
                .HasOne(r => r.Category).WithMany().HasForeignKey(r => r.CategoryId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<RequiredDocument>()
                .HasOne(r => r.AppliesToDepartment).WithMany().HasForeignKey(r => r.AppliesToDepartmentId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<DocumentApproval>()
                .HasOne(a => a.Document).WithMany(d => d.Approvals).HasForeignKey(a => a.EmployeeDocumentId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<DocumentComment>()
                .HasOne(c => c.Document).WithMany(d => d.Comments).HasForeignKey(c => c.EmployeeDocumentId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<DocumentShare>()
                .HasOne(s => s.Document).WithMany().HasForeignKey(s => s.EmployeeDocumentId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<DocumentShare>().HasIndex(s => s.Token).IsUnique();
            modelBuilder.Entity<ExpiryAlert>()
                .HasOne(e => e.Document).WithMany().HasForeignKey(e => e.EmployeeDocumentId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<RetentionPolicy>()
                .HasOne(r => r.Category).WithMany().HasForeignKey(r => r.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<RetentionPolicy>()
                .HasOne(r => r.Folder).WithMany().HasForeignKey(r => r.FolderId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<DocumentRequest>()
                .HasOne(r => r.Employee).WithMany().HasForeignKey(r => r.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<DocumentRequest>()
                .HasOne(r => r.Category).WithMany().HasForeignKey(r => r.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<DocumentRequest>()
                .HasOne(r => r.FulfilledDocument).WithMany().HasForeignKey(r => r.FulfilledDocumentId)
                .OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<DocumentRequest>().HasIndex(r => new { r.EmployeeId, r.Status });
            modelBuilder.Entity<DocumentRequest>().HasIndex(r => r.Status);

            modelBuilder.Entity<DocumentAuditLog>().HasIndex(a => a.EmployeeDocumentId);
            modelBuilder.Entity<DocumentAuditLog>().HasIndex(a => a.EmployeeId);
            modelBuilder.Entity<DocumentAuditLog>().HasIndex(a => a.Timestamp);
            modelBuilder.Entity<DocumentNotification>().HasIndex(n => new { n.RecipientUserId, n.IsRead });

            // ── ITSM / ITIL relationships (all nullable links use SetNull/NoAction to avoid
            //     multiple-cascade-path errors on SQL Server) ─────────────────────────────
            modelBuilder.Entity<Ticket>()
                .HasOne(t => t.Problem).WithMany(p => p.Incidents).HasForeignKey(t => t.ProblemId)
                .OnDelete(DeleteBehavior.SetNull);
            modelBuilder.Entity<Ticket>()
                .HasOne(t => t.ConfigurationItem).WithMany(c => c.Incidents).HasForeignKey(t => t.ConfigurationItemId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<ConfigurationItem>()
                .HasOne(c => c.Owner).WithMany().HasForeignKey(c => c.OwnerId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<ConfigurationItem>()
                .HasOne(c => c.Asset).WithMany().HasForeignKey(c => c.AssetId).OnDelete(DeleteBehavior.SetNull);
            modelBuilder.Entity<ConfigurationItem>().HasIndex(c => c.Name);
            modelBuilder.Entity<ConfigurationItem>().HasIndex(c => c.Status);

            modelBuilder.Entity<Problem>()
                .HasOne(p => p.ConfigurationItem).WithMany(c => c.Problems).HasForeignKey(p => p.ConfigurationItemId)
                .OnDelete(DeleteBehavior.SetNull);
            modelBuilder.Entity<Problem>()
                .HasOne(p => p.AssignedTo).WithMany().HasForeignKey(p => p.AssignedToId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<Problem>()
                .HasOne(p => p.CreatedBy).WithMany().HasForeignKey(p => p.CreatedById).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<Problem>().HasIndex(p => p.Status);

            modelBuilder.Entity<ChangeRequest>()
                .HasOne(c => c.ConfigurationItem).WithMany(ci => ci.Changes).HasForeignKey(c => c.ConfigurationItemId)
                .OnDelete(DeleteBehavior.SetNull);
            modelBuilder.Entity<ChangeRequest>()
                .HasOne(c => c.Problem).WithMany(p => p.Changes).HasForeignKey(c => c.ProblemId)
                .OnDelete(DeleteBehavior.SetNull);
            modelBuilder.Entity<ChangeRequest>()
                .HasOne(c => c.AssignedTo).WithMany().HasForeignKey(c => c.AssignedToId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<ChangeRequest>()
                .HasOne(c => c.ApprovedBy).WithMany().HasForeignKey(c => c.ApprovedById).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<ChangeRequest>()
                .HasOne(c => c.CreatedBy).WithMany().HasForeignKey(c => c.CreatedById).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<ChangeRequest>().HasIndex(c => c.Status);

            modelBuilder.Entity<SlaPolicy>().HasIndex(s => new { s.Priority, s.IsActive });
            modelBuilder.Entity<SlaPolicy>().Property(s => s.WarningThresholdPercent).HasDefaultValue(75);
            modelBuilder.Entity<SlaPolicy>()
                .HasOne(s => s.Calendar).WithMany(c => c.Policies).HasForeignKey(s => s.SlaCalendarId)
                .OnDelete(DeleteBehavior.SetNull);
            modelBuilder.Entity<SlaCalendar>().HasIndex(c => c.Name).IsUnique();
            modelBuilder.Entity<SlaHoliday>()
                .HasOne(h => h.SlaCalendar).WithMany(c => c.Holidays).HasForeignKey(h => h.SlaCalendarId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<SlaHoliday>().HasIndex(h => new { h.SlaCalendarId, h.Date }).IsUnique();
            modelBuilder.Entity<SlaEvent>().Property(e => e.Type).HasConversion<string>();
            modelBuilder.Entity<SlaEvent>()
                .HasOne(e => e.Ticket).WithMany(t => t.SlaEvents).HasForeignKey(e => e.TicketId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<SlaEvent>().HasIndex(e => new { e.TicketId, e.Type }).IsUnique();
            modelBuilder.Entity<Ticket>()
                .HasOne(t => t.SlaPolicy).WithMany().HasForeignKey(t => t.SlaPolicyId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<ServiceCatalogItem>().Property(i => i.DefaultPriority).HasConversion<string>();
            modelBuilder.Entity<ServiceCatalogItem>()
                .HasOne(i => i.Owner).WithMany().HasForeignKey(i => i.OwnerId).OnDelete(DeleteBehavior.SetNull);
            modelBuilder.Entity<ServiceCatalogItem>().HasIndex(i => new { i.IsPublished, i.Category });

            modelBuilder.Entity<ServiceRequest>().Property(r => r.Priority).HasConversion<string>();
            modelBuilder.Entity<ServiceRequest>().Property(r => r.Status).HasConversion<string>();
            modelBuilder.Entity<ServiceRequest>()
                .HasOne(r => r.ServiceCatalogItem).WithMany(i => i.Requests)
                .HasForeignKey(r => r.ServiceCatalogItemId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<ServiceRequest>()
                .HasOne(r => r.RequestedBy).WithMany().HasForeignKey(r => r.RequestedById).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<ServiceRequest>()
                .HasOne(r => r.AssignedTo).WithMany().HasForeignKey(r => r.AssignedToId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<ServiceRequest>()
                .HasOne(r => r.ApprovedBy).WithMany().HasForeignKey(r => r.ApprovedById).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<ServiceRequest>().HasIndex(r => new { r.Status, r.DueAt });
            modelBuilder.Entity<ServiceRequest>().HasIndex(r => new { r.RequestedById, r.CreatedAt });

            // ── Major incident management ───────────────────────────────────────────────
            // Enums stored as readable strings; children cascade; user/ticket links never cascade.
            modelBuilder.Entity<MajorIncident>().Property(m => m.Severity).HasConversion<string>();
            modelBuilder.Entity<MajorIncident>().Property(m => m.Status).HasConversion<string>();
            modelBuilder.Entity<MajorIncident>()
                .HasOne(m => m.SourceTicket).WithMany().HasForeignKey(m => m.SourceTicketId).OnDelete(DeleteBehavior.SetNull);
            modelBuilder.Entity<MajorIncident>()
                .HasOne(m => m.DeclaredBy).WithMany().HasForeignKey(m => m.DeclaredById).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<MajorIncident>()
                .HasOne(m => m.Commander).WithMany().HasForeignKey(m => m.CommanderId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<MajorIncident>()
                .HasOne(m => m.TechnicalLead).WithMany().HasForeignKey(m => m.TechnicalLeadId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<MajorIncident>()
                .HasOne(m => m.CommunicationsLead).WithMany().HasForeignKey(m => m.CommunicationsLeadId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<MajorIncident>()
                .HasOne(m => m.ReviewFacilitator).WithMany().HasForeignKey(m => m.ReviewFacilitatorId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<MajorIncident>().HasIndex(m => new { m.Status, m.Severity });
            modelBuilder.Entity<MajorIncident>().HasIndex(m => m.DeclaredAt);

            modelBuilder.Entity<MajorIncidentAffectedItem>()
                .HasOne(a => a.MajorIncident).WithMany(m => m.AffectedItems).HasForeignKey(a => a.MajorIncidentId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<MajorIncidentAffectedItem>()
                .HasOne(a => a.ConfigurationItem).WithMany().HasForeignKey(a => a.ConfigurationItemId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<MajorIncidentTimelineEntry>().Property(e => e.Type).HasConversion<string>();
            modelBuilder.Entity<MajorIncidentTimelineEntry>()
                .HasOne(e => e.MajorIncident).WithMany(m => m.Timeline).HasForeignKey(e => e.MajorIncidentId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<MajorIncidentTimelineEntry>()
                .HasOne(e => e.LoggedBy).WithMany().HasForeignKey(e => e.LoggedById).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<MajorIncidentTimelineEntry>().HasIndex(e => new { e.MajorIncidentId, e.OccurredAt });

            modelBuilder.Entity<MajorIncidentUpdate>().Property(u => u.Channel).HasConversion<string>();
            modelBuilder.Entity<MajorIncidentUpdate>().Property(u => u.StatusAtUpdate).HasConversion<string>();
            modelBuilder.Entity<MajorIncidentUpdate>()
                .HasOne(u => u.MajorIncident).WithMany(m => m.Updates).HasForeignKey(u => u.MajorIncidentId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<MajorIncidentUpdate>()
                .HasOne(u => u.PostedBy).WithMany().HasForeignKey(u => u.PostedById).OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<MajorIncidentFollowUp>().Property(f => f.Status).HasConversion<string>();
            modelBuilder.Entity<MajorIncidentFollowUp>()
                .HasOne(f => f.MajorIncident).WithMany(m => m.FollowUps).HasForeignKey(f => f.MajorIncidentId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<MajorIncidentFollowUp>()
                .HasOne(f => f.Owner).WithMany().HasForeignKey(f => f.OwnerId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<MajorIncidentFollowUp>().HasIndex(f => new { f.MajorIncidentId, f.Status });

            // ── Meeting Minutes relationships ──────────────────────────────────────────
            // Store enums as readable strings (matches the Ticket/User convention above).
            modelBuilder.Entity<Meeting>().Property(m => m.Status).HasConversion<string>();
            modelBuilder.Entity<MeetingAttendance>().Property(a => a.Status).HasConversion<string>();
            modelBuilder.Entity<ActionItem>().Property(a => a.Status).HasConversion<string>();
            modelBuilder.Entity<ActionItem>().Property(a => a.Priority).HasConversion<string>();
            modelBuilder.Entity<ActionItemUpdate>().Property(u => u.StatusAtUpdate).HasConversion<string>();

            // All User/Department FKs use Restrict (no cascade) so the many links from these
            // tables never create multiple-cascade-path errors on SQL Server.
            modelBuilder.Entity<Meeting>()
                .HasOne(m => m.Facilitator).WithMany().HasForeignKey(m => m.FacilitatorId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<Meeting>()
                .HasOne(m => m.MinuteTaker).WithMany().HasForeignKey(m => m.MinuteTakerId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<Meeting>()
                .HasOne(m => m.Department).WithMany().HasForeignKey(m => m.DepartmentId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<Meeting>().HasIndex(m => m.DepartmentId);

            modelBuilder.Entity<MeetingRosterMember>()
                .HasOne(r => r.User).WithMany().HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<MeetingRosterMember>()
                .HasOne(r => r.Department).WithMany().HasForeignKey(r => r.DepartmentId)
                .OnDelete(DeleteBehavior.Restrict);
            // A user appears once per department roster (and once on the org-wide roster).
            modelBuilder.Entity<MeetingRosterMember>().HasIndex(r => new { r.DepartmentId, r.UserId }).IsUnique();

            // Attendance: cascade from its meeting; Restrict on the user link.
            modelBuilder.Entity<MeetingAttendance>()
                .HasOne(a => a.Meeting).WithMany(m => m.Attendances).HasForeignKey(a => a.MeetingId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<MeetingAttendance>()
                .HasOne(a => a.User).WithMany().HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<MeetingAttendance>().HasIndex(a => new { a.MeetingId, a.UserId }).IsUnique();

            // Action items belong to the meeting they were raised in (Restrict: don't lose
            // tracked tasks by deleting a meeting) and to an assignee (Restrict).
            modelBuilder.Entity<ActionItem>()
                .HasOne(a => a.Meeting).WithMany(m => m.ActionItems).HasForeignKey(a => a.MeetingId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<ActionItem>()
                .HasOne(a => a.AssignedTo).WithMany().HasForeignKey(a => a.AssignedToId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<ActionItem>().HasIndex(a => a.Status);

            // Updates cascade from their action item; the optional meeting link is NoAction
            // (avoids a second cascade path into ActionItemUpdate).
            modelBuilder.Entity<ActionItemUpdate>()
                .HasOne(u => u.ActionItem).WithMany(a => a.Updates).HasForeignKey(u => u.ActionItemId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<ActionItemUpdate>()
                .HasOne(u => u.Meeting).WithMany().HasForeignKey(u => u.MeetingId)
                .OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<ActionItemUpdate>()
                .HasOne(u => u.UpdatedBy).WithMany().HasForeignKey(u => u.UpdatedById)
                .OnDelete(DeleteBehavior.Restrict);

            // Default SLA policies (mirror the previous hard-coded targets; HR/Admin can edit these).
            var slaSeed = new DateTime(2026, 7, 1);
            modelBuilder.Entity<SlaPolicy>().HasData(
                new SlaPolicy { Id = 1, Name = "Critical Priority", Priority = Ticket.TicketPriority.Critical, ResponseMinutes = 30, ResolutionMinutes = 240, IsActive = true, CreatedAt = slaSeed },
                new SlaPolicy { Id = 2, Name = "High Priority", Priority = Ticket.TicketPriority.High, ResponseMinutes = 60, ResolutionMinutes = 480, IsActive = true, CreatedAt = slaSeed },
                new SlaPolicy { Id = 3, Name = "Medium Priority", Priority = Ticket.TicketPriority.Medium, ResponseMinutes = 240, ResolutionMinutes = 1440, IsActive = true, CreatedAt = slaSeed },
                new SlaPolicy { Id = 4, Name = "Low Priority", Priority = Ticket.TicketPriority.Low, ResponseMinutes = 480, ResolutionMinutes = 4320, IsActive = true, CreatedAt = slaSeed }
            );

            // ── Seed: system folders, starter categories, default storage provider ──
            var efmSeed = new DateTime(2026, 1, 1);
            modelBuilder.Entity<DocumentFolder>().HasData(
                new DocumentFolder { Id = 1, Name = "Personal Documents", Icon = "fa-id-card", SortOrder = 1, IsSystem = true, IsActive = true },
                new DocumentFolder { Id = 2, Name = "Employment Documents", Icon = "fa-briefcase", SortOrder = 2, IsSystem = true, IsActive = true },
                new DocumentFolder { Id = 3, Name = "Academic Qualifications", Icon = "fa-graduation-cap", SortOrder = 3, IsSystem = true, IsActive = true },
                new DocumentFolder { Id = 4, Name = "Professional Certifications", Icon = "fa-certificate", SortOrder = 4, IsSystem = true, IsActive = true },
                new DocumentFolder { Id = 5, Name = "Medical Records", Icon = "fa-notes-medical", SortOrder = 5, IsSystem = true, IsActive = true },
                new DocumentFolder { Id = 6, Name = "Payroll Documents", Icon = "fa-money-check-dollar", SortOrder = 6, IsSystem = true, IsActive = true },
                new DocumentFolder { Id = 7, Name = "Tax Documents", Icon = "fa-file-invoice-dollar", SortOrder = 7, IsSystem = true, IsActive = true },
                new DocumentFolder { Id = 8, Name = "Contracts", Icon = "fa-file-contract", SortOrder = 8, IsSystem = true, IsActive = true },
                new DocumentFolder { Id = 9, Name = "Disciplinary Records", Icon = "fa-gavel", SortOrder = 9, IsSystem = true, IsActive = true },
                new DocumentFolder { Id = 10, Name = "Performance Reviews", Icon = "fa-chart-line", SortOrder = 10, IsSystem = true, IsActive = true },
                new DocumentFolder { Id = 11, Name = "Training Records", Icon = "fa-chalkboard-user", SortOrder = 11, IsSystem = true, IsActive = true },
                new DocumentFolder { Id = 12, Name = "Leave Documents", Icon = "fa-plane-departure", SortOrder = 12, IsSystem = true, IsActive = true },
                new DocumentFolder { Id = 13, Name = "Promotion Documents", Icon = "fa-arrow-up-right-dots", SortOrder = 13, IsSystem = true, IsActive = true },
                new DocumentFolder { Id = 14, Name = "Transfer Documents", Icon = "fa-right-left", SortOrder = 14, IsSystem = true, IsActive = true },
                new DocumentFolder { Id = 15, Name = "Awards", Icon = "fa-award", SortOrder = 15, IsSystem = true, IsActive = true },
                new DocumentFolder { Id = 16, Name = "Warnings", Icon = "fa-triangle-exclamation", SortOrder = 16, IsSystem = true, IsActive = true },
                new DocumentFolder { Id = 17, Name = "Exit Documents", Icon = "fa-door-open", SortOrder = 17, IsSystem = true, IsActive = true },
                new DocumentFolder { Id = 18, Name = "Resignation Documents", Icon = "fa-file-signature", SortOrder = 18, IsSystem = true, IsActive = true },
                new DocumentFolder { Id = 19, Name = "Retirement Documents", Icon = "fa-umbrella-beach", SortOrder = 19, IsSystem = true, IsActive = true },
                new DocumentFolder { Id = 20, Name = "Other Documents", Icon = "fa-folder", SortOrder = 20, IsSystem = true, IsActive = true }
            );

            modelBuilder.Entity<DocumentCategory>().HasData(
                new DocumentCategory { Id = 1, Name = "Passport", DefaultFolderId = 1, IsExpiryTracked = true, IsActive = true, CreatedAt = efmSeed },
                new DocumentCategory { Id = 2, Name = "National ID", DefaultFolderId = 1, IsActive = true, CreatedAt = efmSeed },
                new DocumentCategory { Id = 3, Name = "Birth Certificate", DefaultFolderId = 1, IsActive = true, CreatedAt = efmSeed },
                new DocumentCategory { Id = 4, Name = "Driver's License", DefaultFolderId = 1, IsExpiryTracked = true, IsActive = true, CreatedAt = efmSeed },
                new DocumentCategory { Id = 5, Name = "Police Clearance", DefaultFolderId = 1, IsExpiryTracked = true, IsActive = true, CreatedAt = efmSeed },
                new DocumentCategory { Id = 6, Name = "CV", DefaultFolderId = 2, IsActive = true, CreatedAt = efmSeed },
                new DocumentCategory { Id = 7, Name = "Offer Letter", DefaultFolderId = 2, IsActive = true, CreatedAt = efmSeed },
                new DocumentCategory { Id = 8, Name = "Employment Contract", DefaultFolderId = 8, IsActive = true, CreatedAt = efmSeed },
                new DocumentCategory { Id = 9, Name = "NDA", DefaultFolderId = 8, IsActive = true, CreatedAt = efmSeed },
                new DocumentCategory { Id = 10, Name = "Medical Aid Card", DefaultFolderId = 5, IsExpiryTracked = true, IsActive = true, CreatedAt = efmSeed },
                new DocumentCategory { Id = 11, Name = "NSSA", DefaultFolderId = 6, IsActive = true, CreatedAt = efmSeed },
                new DocumentCategory { Id = 12, Name = "Tax Certificate", DefaultFolderId = 7, IsActive = true, CreatedAt = efmSeed },
                new DocumentCategory { Id = 13, Name = "Degree Certificate", DefaultFolderId = 3, IsActive = true, CreatedAt = efmSeed },
                new DocumentCategory { Id = 14, Name = "Diploma", DefaultFolderId = 3, IsActive = true, CreatedAt = efmSeed },
                new DocumentCategory { Id = 15, Name = "Professional License", DefaultFolderId = 4, IsExpiryTracked = true, IsActive = true, CreatedAt = efmSeed },
                new DocumentCategory { Id = 16, Name = "Performance Review", DefaultFolderId = 10, IsActive = true, CreatedAt = efmSeed },
                new DocumentCategory { Id = 17, Name = "Training Certificate", DefaultFolderId = 11, IsActive = true, CreatedAt = efmSeed },
                new DocumentCategory { Id = 18, Name = "Warning Letter", DefaultFolderId = 16, IsActive = true, CreatedAt = efmSeed },
                new DocumentCategory { Id = 19, Name = "Promotion Letter", DefaultFolderId = 13, IsActive = true, CreatedAt = efmSeed },
                new DocumentCategory { Id = 20, Name = "Termination Letter", DefaultFolderId = 17, IsActive = true, CreatedAt = efmSeed },
                new DocumentCategory { Id = 21, Name = "Retirement Letter", DefaultFolderId = 19, IsActive = true, CreatedAt = efmSeed },
                new DocumentCategory { Id = 22, Name = "Other", DefaultFolderId = 20, IsActive = true, CreatedAt = efmSeed }
            );

            modelBuilder.Entity<StorageProvider>().HasData(
                new StorageProvider { Id = 1, Name = "Local Disk", Type = StorageProviderType.LocalDisk, RootLocation = "employee-documents", IsDefault = true, IsActive = true, CreatedAt = efmSeed }
            );

            // ── IMS / ISO (ISO 9001:2015 & ISO/IEC 27001:2022) ──────────────────────
            // Persist every IMS enum as a readable string, keep decimals at (18,2), and set every IMS
            // foreign key to Restrict so SQL Server never sees multiple cascade paths. IMS parents are
            // soft-deleted (or never hard-deleted), so cascade delete is intentionally not used here.
            foreach (var entityType in modelBuilder.Model.GetEntityTypes()
                         .Where(e => e.ClrType.Namespace == "IT_Service_Management_System.Models.Ims")
                         .ToList())
            {
                var entity = modelBuilder.Entity(entityType.ClrType);

                foreach (var prop in entityType.GetProperties().ToList())
                {
                    var clr = Nullable.GetUnderlyingType(prop.ClrType) ?? prop.ClrType;
                    if (clr.IsEnum)
                        entity.Property(prop.Name).HasConversion<string>();
                    else if (clr == typeof(decimal))
                        entity.Property(prop.Name).HasPrecision(18, 2);
                }

                foreach (var fk in entityType.GetForeignKeys())
                    fk.DeleteBehavior = DeleteBehavior.Restrict;
            }

            // A document has many versions (history) and one "current" version pointer — configured
            // explicitly so the two relationships between the same pair of types are unambiguous.
            modelBuilder.Entity<IsoDocumentVersion>()
                .HasOne(v => v.Document)
                .WithMany(d => d.Versions)
                .HasForeignKey(v => v.IsoDocumentId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<IsoDocument>()
                .HasOne(d => d.CurrentVersionRef)
                .WithMany()
                .HasForeignKey(d => d.CurrentVersionId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<IsoDocument>().HasIndex(d => d.DocumentNumber).IsUnique();
            modelBuilder.Entity<IsoDocument>().HasQueryFilter(d => !d.IsDeleted);
            modelBuilder.Entity<IsoClause>().HasIndex(c => new { c.Standard, c.ClauseNumber });

            // ── Performance indexes ─────────────────────────────────────────────────
            // Status/date columns are filtered/counted constantly by the dashboards,
            // registers and the ECIE compliance-health engine but were unindexed, so
            // every load scanned the table. (Enum columns are persisted as strings;
            // an index on the nvarchar column serves equality filters well.)
            modelBuilder.Entity<Ticket>().HasIndex(t => new { t.Status, t.AssignedToId });
            modelBuilder.Entity<Ticket>().HasIndex(t => new { t.Status, t.Priority });
            modelBuilder.Entity<AuditLog>().HasIndex(a => a.Timestamp);
            modelBuilder.Entity<AuditLog>().HasIndex(a => new { a.UserId, a.Timestamp });

            modelBuilder.Entity<IsoDocument>().HasIndex(d => d.Status);
            modelBuilder.Entity<IsoDocument>().HasIndex(d => d.ReviewDate);
            modelBuilder.Entity<IsoDocument>().HasIndex(d => d.ExpiryDate);
            modelBuilder.Entity<IsoDocumentAcknowledgement>().HasIndex(a => a.Status);
            modelBuilder.Entity<Risk>().HasIndex(r => r.Status);
            modelBuilder.Entity<Risk>().HasIndex(r => r.Category);
            modelBuilder.Entity<Capa>().HasIndex(c => new { c.Status, c.DueDate });
            modelBuilder.Entity<NonConformance>().HasIndex(n => n.Status);

            // Incidents: children cascade with the parent; department link must not cascade.
            modelBuilder.Entity<Incident>().HasIndex(i => new { i.Year, i.IncidentNo }).IsUnique();
            modelBuilder.Entity<Incident>().HasIndex(i => i.Status);
            modelBuilder.Entity<Incident>()
                .HasOne(i => i.Department).WithMany().HasForeignKey(i => i.DepartmentId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<Incident>()
                .HasOne(i => i.CreatedBy).WithMany().HasForeignKey(i => i.CreatedById)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<Incident>()
                .HasOne(i => i.Capa).WithMany().HasForeignKey(i => i.CapaId)
                .OnDelete(DeleteBehavior.SetNull);
            modelBuilder.Entity<Incident>()
                .HasOne(i => i.DeptManagerSignedBy).WithMany().HasForeignKey(i => i.DeptManagerSignedById)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<Incident>()
                .HasOne(i => i.QaSignedBy).WithMany().HasForeignKey(i => i.QaSignedById)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<Incident>()
                .HasOne(i => i.GmSignedBy).WithMany().HasForeignKey(i => i.GmSignedById)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<IncidentInvestigator>()
                .HasOne(x => x.Incident).WithMany(i => i.Investigators).HasForeignKey(x => x.IncidentId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<IncidentDamage>()
                .HasOne(x => x.Incident).WithMany(i => i.Damages).HasForeignKey(x => x.IncidentId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<IncidentAction>()
                .HasOne(x => x.Incident).WithMany(i => i.Actions).HasForeignKey(x => x.IncidentId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<IncidentAttachment>()
                .HasOne(x => x.Incident).WithMany(i => i.Attachments).HasForeignKey(x => x.IncidentId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<IncidentAttachment>()
                .HasOne(x => x.UploadedBy).WithMany().HasForeignKey(x => x.UploadedById)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<IncidentAttachment>().HasIndex(x => x.IncidentId);
            modelBuilder.Entity<AuditFinding>().HasIndex(f => f.Status);
            modelBuilder.Entity<Audit>().HasIndex(a => new { a.Status, a.ActualEndDate });
            modelBuilder.Entity<TrainingRecord>().HasIndex(t => t.Status);
            modelBuilder.Entity<TrainingRecord>().HasIndex(t => t.CertificateExpiry);
            modelBuilder.Entity<Supplier>().HasIndex(s => s.Status);
            modelBuilder.Entity<ManagementReview>().HasIndex(m => new { m.Status, m.MeetingDate });
            modelBuilder.Entity<ManagementReviewAction>().HasIndex(a => new { a.Status, a.DueDate });
            modelBuilder.Entity<Objective>().HasIndex(o => o.Status);
            modelBuilder.Entity<ComplianceObligation>().HasIndex(o => o.Status);
            modelBuilder.Entity<Improvement>().HasIndex(i => i.Status);

            // Seed document categories
            modelBuilder.Entity<IsoDocumentCategory>().HasData(
                new IsoDocumentCategory { Id = 1, Name = "Quality Management", Code = "QMS", IsActive = true },
                new IsoDocumentCategory { Id = 2, Name = "Information Security", Code = "ISMS", IsActive = true },
                new IsoDocumentCategory { Id = 3, Name = "Human Resources", Code = "HR", IsActive = true },
                new IsoDocumentCategory { Id = 4, Name = "Information Technology", Code = "IT", IsActive = true },
                new IsoDocumentCategory { Id = 5, Name = "Operations", Code = "OPS", IsActive = true },
                new IsoDocumentCategory { Id = 6, Name = "Finance", Code = "FIN", IsActive = true },
                new IsoDocumentCategory { Id = 7, Name = "Health, Safety & Environment", Code = "HSE", IsActive = true },
                new IsoDocumentCategory { Id = 8, Name = "General / Administration", Code = "GEN", IsActive = true }
            );

            // Seed key ISO clauses (reference data for tagging, evidence and the AI compliance assistant)
            modelBuilder.Entity<IsoClause>().HasData(
                // ISO 9001:2015
                new IsoClause { Id = 1, Standard = IsoStandard.Iso9001, ClauseNumber = "4", Title = "Context of the organization" },
                new IsoClause { Id = 2, Standard = IsoStandard.Iso9001, ClauseNumber = "5", Title = "Leadership" },
                new IsoClause { Id = 3, Standard = IsoStandard.Iso9001, ClauseNumber = "6", Title = "Planning" },
                new IsoClause { Id = 4, Standard = IsoStandard.Iso9001, ClauseNumber = "6.2", Title = "Quality objectives and planning to achieve them" },
                new IsoClause { Id = 5, Standard = IsoStandard.Iso9001, ClauseNumber = "7", Title = "Support" },
                new IsoClause { Id = 6, Standard = IsoStandard.Iso9001, ClauseNumber = "7.2", Title = "Competence" },
                new IsoClause { Id = 7, Standard = IsoStandard.Iso9001, ClauseNumber = "7.5", Title = "Documented information" },
                new IsoClause { Id = 8, Standard = IsoStandard.Iso9001, ClauseNumber = "8", Title = "Operation" },
                new IsoClause { Id = 9, Standard = IsoStandard.Iso9001, ClauseNumber = "8.4", Title = "Control of externally provided processes, products and services" },
                new IsoClause { Id = 10, Standard = IsoStandard.Iso9001, ClauseNumber = "8.5", Title = "Production and service provision" },
                new IsoClause { Id = 11, Standard = IsoStandard.Iso9001, ClauseNumber = "9", Title = "Performance evaluation" },
                new IsoClause { Id = 12, Standard = IsoStandard.Iso9001, ClauseNumber = "9.2", Title = "Internal audit" },
                new IsoClause { Id = 13, Standard = IsoStandard.Iso9001, ClauseNumber = "9.3", Title = "Management review" },
                new IsoClause { Id = 14, Standard = IsoStandard.Iso9001, ClauseNumber = "10", Title = "Improvement" },
                new IsoClause { Id = 15, Standard = IsoStandard.Iso9001, ClauseNumber = "10.2", Title = "Nonconformity and corrective action" },
                // ISO/IEC 27001:2022
                new IsoClause { Id = 16, Standard = IsoStandard.Iso27001, ClauseNumber = "4", Title = "Context of the organization" },
                new IsoClause { Id = 17, Standard = IsoStandard.Iso27001, ClauseNumber = "5", Title = "Leadership" },
                new IsoClause { Id = 18, Standard = IsoStandard.Iso27001, ClauseNumber = "6", Title = "Planning" },
                new IsoClause { Id = 19, Standard = IsoStandard.Iso27001, ClauseNumber = "6.1.2", Title = "Information security risk assessment" },
                new IsoClause { Id = 20, Standard = IsoStandard.Iso27001, ClauseNumber = "6.1.3", Title = "Information security risk treatment" },
                new IsoClause { Id = 21, Standard = IsoStandard.Iso27001, ClauseNumber = "7", Title = "Support" },
                new IsoClause { Id = 22, Standard = IsoStandard.Iso27001, ClauseNumber = "7.5", Title = "Documented information" },
                new IsoClause { Id = 23, Standard = IsoStandard.Iso27001, ClauseNumber = "8", Title = "Operation" },
                new IsoClause { Id = 24, Standard = IsoStandard.Iso27001, ClauseNumber = "9", Title = "Performance evaluation" },
                new IsoClause { Id = 25, Standard = IsoStandard.Iso27001, ClauseNumber = "9.2", Title = "Internal audit" },
                new IsoClause { Id = 26, Standard = IsoStandard.Iso27001, ClauseNumber = "9.3", Title = "Management review" },
                new IsoClause { Id = 27, Standard = IsoStandard.Iso27001, ClauseNumber = "10", Title = "Improvement" },
                new IsoClause { Id = 28, Standard = IsoStandard.Iso27001, ClauseNumber = "10.2", Title = "Nonconformity and corrective action" }
            );
        }
    }
}
