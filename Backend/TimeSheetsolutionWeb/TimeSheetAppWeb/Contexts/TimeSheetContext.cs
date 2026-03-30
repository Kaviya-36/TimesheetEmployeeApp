using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Text.Json;
using TimeSheetAppWeb.Model;

namespace TimeSheetAppWeb.Contexts
{
    public class TimeSheetContext : DbContext
    {
        public TimeSheetContext(DbContextOptions<TimeSheetContext> options)
            : base(options)
        {
        }

        // -------------------- DbSets --------------------
        public DbSet<User> Users { get; set; }
        public DbSet<Department> Departments { get; set; }
        public DbSet<Project> Projects { get; set; }
        public DbSet<ProjectAssignment> ProjectAssignments { get; set; }
        public DbSet<Timesheet> Timesheets { get; set; }
        public DbSet<LeaveType> LeaveTypes { get; set; }
        public DbSet<LeaveRequest> LeaveRequests { get; set; }
        public DbSet<Attendance> Attendances { get; set; }
        public DbSet<Payroll> Payrolls { get; set; }
        public DbSet<InternTask> InternTasks { get; set; }
        public DbSet<InternDetails> InternDetails { get; set; }

        // ✅ NEW
        public DbSet<AuditLog> AuditLogs { get; set; }

        // -------------------- MODEL CONFIG --------------------
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // USER
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email).IsUnique();

            modelBuilder.Entity<User>()
                .HasIndex(u => u.EmployeeId).IsUnique();

            modelBuilder.Entity<User>()
                .Property(u => u.Role)
                .HasConversion<string>();

            modelBuilder.Entity<User>()
                .HasMany(u => u.Timesheets)
                .WithOne(t => t.User)
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<User>()
                .HasMany(u => u.LeaveRequests)
                .WithOne(l => l.User)
                .HasForeignKey(l => l.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<User>()
                .HasMany(u => u.Attendances)
                .WithOne(a => a.User)
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<User>()
                .HasMany(u => u.ProjectAssignments)
                .WithOne(pa => pa.User)
                .HasForeignKey(pa => pa.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // DEPARTMENT
            modelBuilder.Entity<Department>()
                .HasMany(d => d.Users)
                .WithOne(u => u.Department)
                .HasForeignKey(u => u.DepartmentId);

            // PROJECT
            modelBuilder.Entity<Project>()
                .HasOne(p => p.Manager)
                .WithMany()
                .HasForeignKey(p => p.ManagerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Project>()
                .HasMany(p => p.ProjectAssignments)
                .WithOne(pa => pa.Project)
                .HasForeignKey(pa => pa.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Project>()
                .HasMany(p => p.Timesheets)
                .WithOne(t => t.Project)
                .HasForeignKey(t => t.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            // TIMESHEET
            modelBuilder.Entity<Timesheet>()
                .HasOne(t => t.ApprovedBy)
                .WithMany()
                .HasForeignKey(t => t.ApprovedById)
                .OnDelete(DeleteBehavior.Restrict);

            // LEAVE REQUEST
            modelBuilder.Entity<LeaveRequest>()
                .HasOne(l => l.ApprovedBy)
                .WithMany()
                .HasForeignKey(l => l.ApprovedById)
                .OnDelete(DeleteBehavior.Restrict);

            // INTERN TASK
            modelBuilder.Entity<InternTask>()
                .HasOne(it => it.Intern)
                .WithMany()
                .HasForeignKey(it => it.InternId)
                .OnDelete(DeleteBehavior.Cascade);

            // PAYROLL
            modelBuilder.Entity<Payroll>(entity =>
            {
                entity.Property(p => p.BasicSalary).HasPrecision(18, 2);
                entity.Property(p => p.OvertimeAmount).HasPrecision(18, 2);
                entity.Property(p => p.Deductions).HasPrecision(18, 2);
                entity.Property(p => p.NetSalary).HasPrecision(18, 2);
            });

            // INTERN DETAILS
            modelBuilder.Entity<InternDetails>(entity =>
            {
                entity.HasOne(e => e.User)
                      .WithMany()
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Mentor)
                      .WithMany()
                      .HasForeignKey(e => e.MentorId)
                      .OnDelete(DeleteBehavior.NoAction);
            });
        }

        // -------------------- AUDIT LOGIC 🔥 --------------------
        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            var auditEntries = new List<AuditLog>();

            foreach (var entry in ChangeTracker.Entries().ToList())
            {
                if (entry.Entity is AuditLog ||
                    entry.State == EntityState.Detached ||
                    entry.State == EntityState.Unchanged)
                    continue;

                var audit = new AuditLog
                {
                    TableName = entry.Entity.GetType().Name,
                    Action    = entry.State.ToString(),
                    ChangedAt = DateTime.UtcNow
                };

                var keyValues = new Dictionary<string, object?>();
                foreach (var prop in entry.Properties.Where(p => p.Metadata.IsPrimaryKey()))
                    keyValues[prop.Metadata.Name] = prop.CurrentValue;
                audit.KeyValues = JsonSerializer.Serialize(keyValues);

                var oldValues = new Dictionary<string, object?>();
                var newValues = new Dictionary<string, object?>();

                foreach (var prop in entry.Properties)
                {
                    if (prop.Metadata.Name == "PasswordHash") continue;

                    if (entry.State == EntityState.Modified)
                    {
                        if (!Equals(prop.OriginalValue, prop.CurrentValue))
                        {
                            oldValues[prop.Metadata.Name] = prop.OriginalValue;
                            newValues[prop.Metadata.Name] = prop.CurrentValue;
                        }
                    }
                    else if (entry.State == EntityState.Added)
                        newValues[prop.Metadata.Name] = prop.CurrentValue;
                    else if (entry.State == EntityState.Deleted)
                        oldValues[prop.Metadata.Name] = prop.OriginalValue;
                }

                audit.OldValues = oldValues.Count > 0 ? JsonSerializer.Serialize(oldValues) : null;
                audit.NewValues = newValues.Count > 0 ? JsonSerializer.Serialize(newValues) : null;

                auditEntries.Add(audit);
            }

            // Save main changes
            var result = await base.SaveChangesAsync(cancellationToken);

            // Save audit entries in a separate try-catch so audit failures never break main operations
            if (auditEntries.Any())
            {
                try
                {
                    foreach (var audit in auditEntries)
                        AuditLogs.Add(audit);
                    await base.SaveChangesAsync(cancellationToken);
                }
                catch
                {
                    // Audit logging failure must never break the main operation
                    // Detach any failed audit entries to clean up the context
                    foreach (var entry in ChangeTracker.Entries<AuditLog>().ToList())
                        entry.State = EntityState.Detached;
                }
            }

            return result;
        }
    }
}