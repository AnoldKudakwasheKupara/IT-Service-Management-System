using IT_Service_Management_System.Models;
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

        public DbSet<SupervisorApproval> SupervisorApprovals { get; set; }
        public DbSet<HodApproval> HodApprovals { get; set; }
        public DbSet<HrApproval> HrApprovals { get; set; }

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


            modelBuilder.Entity<TicketAttachment>()
                .HasCheckConstraint("CK_Attachment_Owner",
                    "[TicketId] IS NOT NULL OR [TicketMessageId] IS NOT NULL");
        }
    }
}