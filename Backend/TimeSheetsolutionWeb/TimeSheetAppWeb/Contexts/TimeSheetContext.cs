using Microsoft.EntityFrameworkCore;
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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // -------------------- USER --------------------
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();
            modelBuilder.Entity<User>()
                .Property(u => u.Role)
                .HasConversion<string>();

            modelBuilder.Entity<User>()
                .HasIndex(u => u.EmployeeId)
                .IsUnique();

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

            // -------------------- DEPARTMENT --------------------
            modelBuilder.Entity<Department>()
                .HasMany(d => d.Users)
                .WithOne(u => u.Department)
                .HasForeignKey(u => u.DepartmentId);

            // -------------------- PROJECT --------------------
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

            // -------------------- TIMESHEET --------------------
            modelBuilder.Entity<Timesheet>()
                .HasOne(t => t.ApprovedBy)
                .WithMany()
                .HasForeignKey(t => t.ApprovedById)
                .OnDelete(DeleteBehavior.Restrict);

            // -------------------- LEAVE REQUEST --------------------
            modelBuilder.Entity<LeaveRequest>()
                .HasOne(l => l.ApprovedBy)
                .WithMany()
                .HasForeignKey(l => l.ApprovedById)
                .OnDelete(DeleteBehavior.Restrict);

            // -------------------- INTERN TASK --------------------
            modelBuilder.Entity<InternTask>()
                .HasOne(it => it.Intern)
                .WithMany()
                .HasForeignKey(it => it.InternId)
                .OnDelete(DeleteBehavior.Cascade);

            // -------------------- PAYROLL --------------------
            modelBuilder.Entity<Payroll>(entity =>
            {
                entity.Property(p => p.BasicSalary).HasPrecision(18, 2);
                entity.Property(p => p.OvertimeAmount).HasPrecision(18, 2);
                entity.Property(p => p.Deductions).HasPrecision(18, 2);
                entity.Property(p => p.NetSalary).HasPrecision(18, 2);
            });

            modelBuilder.Entity<InternDetails>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.HasOne(e => e.User)
                      .WithMany() // or .WithMany(i => i.Interns)
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(DeleteBehavior.Cascade); // keep cascade here

                entity.HasOne(e => e.Mentor)
                      .WithMany() // or .WithMany(m => m.Mentees)
                      .HasForeignKey(e => e.MentorId)
                      .OnDelete(DeleteBehavior.NoAction); // change this line
            });
        }
    }
}