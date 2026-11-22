using Microsoft.EntityFrameworkCore;
using RepairConnect.Models;

namespace RepairConnect.Data;

public sealed class RepairConnectDbContext(DbContextOptions<RepairConnectDbContext> options)
    : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<TechnicianProfile> TechnicianProfiles => Set<TechnicianProfile>();
    public DbSet<TechnicianDocument> TechnicianDocuments => Set<TechnicianDocument>();
    public DbSet<ServiceCategory> ServiceCategories => Set<ServiceCategory>();
    public DbSet<TechnicianCategory> TechnicianCategories => Set<TechnicianCategory>();
    public DbSet<RepairRequest> RepairRequests => Set<RepairRequest>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<Review> Reviews => Set<Review>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users", table =>
            {
                table.HasCheckConstraint("CK_Users_Roles", "[Roles] BETWEEN 0 AND 7");
                table.HasCheckConstraint("CK_Users_Status", "[Status] IN (N'Active', N'Inactive')");
            });
            entity.Property(user => user.Email).HasMaxLength(256);
            entity.Property(user => user.NormalizedEmail).HasMaxLength(256);
            entity.HasIndex(user => user.NormalizedEmail).IsUnique();
            entity.Property(user => user.PasswordHash).HasMaxLength(255);
            entity.Property(user => user.DisplayName).HasMaxLength(100);
            entity.Property(user => user.PhoneNumber).HasMaxLength(20);
            entity.Property(user => user.Status)
                .HasConversion<string>().HasMaxLength(16).HasDefaultValue(AccountStatus.Active);
            entity.Property(user => user.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        });

        modelBuilder.Entity<TechnicianProfile>(entity =>
        {
            entity.ToTable("TechnicianProfiles", table =>
            {
                table.HasCheckConstraint("CK_TechnicianProfiles_YearsOfExperience", "[YearsOfExperience] >= 0");
                table.HasCheckConstraint("CK_TechnicianProfiles_VerificationStatus",
                    "[VerificationStatus] IN (N'Pending', N'Approved', N'Rejected')");
                table.HasCheckConstraint("CK_TechnicianProfiles_AvailabilityStatus",
                    "[AvailabilityStatus] IN (N'Available', N'Busy', N'Offline')");
            });
            entity.HasKey(profile => profile.UserId);
            entity.Property(profile => profile.UserId).ValueGeneratedNever();
            entity.Property(profile => profile.ProfilePhoto).HasMaxLength(512);
            entity.Property(profile => profile.Specialization).HasMaxLength(100);
            entity.Property(profile => profile.VerificationStatus)
                .HasConversion<string>().HasMaxLength(16).HasDefaultValue(VerificationStatus.Pending);
            entity.Property(profile => profile.AvailabilityStatus)
                .HasConversion<string>().HasMaxLength(16).HasDefaultValue(AvailabilityStatus.Available);
            entity.Property(profile => profile.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasOne(profile => profile.User).WithOne()
                .HasForeignKey<TechnicianProfile>(profile => profile.UserId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(profile => profile.ReviewedByUser).WithMany()
                .HasForeignKey(profile => profile.ReviewedByUserId).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<TechnicianDocument>(entity =>
        {
            entity.Property(document => document.Document).HasMaxLength(512);
            entity.Property(document => document.UploadedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasOne(document => document.Technician).WithMany()
                .HasForeignKey(document => document.TechnicianUserId).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<ServiceCategory>(entity =>
        {
            entity.Property(category => category.Name).HasMaxLength(100);
            entity.HasIndex(category => category.Name).IsUnique();
            entity.Property(category => category.IsActive).HasDefaultValue(true);
        });

        modelBuilder.Entity<TechnicianCategory>(entity =>
        {
            entity.HasKey(category => new { category.TechnicianUserId, category.CategoryId });
            entity.HasOne(category => category.Technician).WithMany()
                .HasForeignKey(category => category.TechnicianUserId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(category => category.Category).WithMany()
                .HasForeignKey(category => category.CategoryId).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<RepairRequest>(entity =>
        {
            entity.ToTable("RepairRequests", table =>
            {
                table.HasCheckConstraint("CK_RepairRequests_Status",
                    "[Status] IN (N'Open', N'Booked', N'Completed', N'Cancelled')");
                table.HasCheckConstraint("CK_RepairRequests_Urgency", "[Urgency] IN (N'Low', N'Medium', N'High')");
            });
            entity.Property(request => request.Title).HasMaxLength(150);
            entity.Property(request => request.Urgency)
                .HasConversion<string>().HasMaxLength(16)
                .HasDefaultValue(RepairUrgency.Medium).HasSentinel(RepairUrgency.Medium);
            entity.Property(request => request.Status)
                .HasConversion<string>().HasMaxLength(16).HasDefaultValue(RepairRequestStatus.Open);
            entity.Property(request => request.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasOne(request => request.Client).WithMany()
                .HasForeignKey(request => request.ClientUserId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(request => request.Category).WithMany()
                .HasForeignKey(request => request.CategoryId).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<Booking>(entity =>
        {
            entity.ToTable("Bookings", table => table.HasCheckConstraint("CK_Bookings_Status",
                "[Status] IN (N'Pending', N'Accepted', N'Declined', N'InProgress', N'Completed', N'Cancelled')"));
            entity.Property(booking => booking.Status)
                .HasConversion<string>().HasMaxLength(16).HasDefaultValue(BookingStatus.Pending);
            entity.Property(booking => booking.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasIndex(booking => booking.RepairRequestId);
            entity.HasIndex(booking => booking.RepairRequestId, "UX_Bookings_ActiveRepairRequest")
                .IsUnique().HasFilter("[Status] IN (N'Pending', N'Accepted', N'InProgress')");
            entity.HasOne(booking => booking.RepairRequest).WithMany()
                .HasForeignKey(booking => booking.RepairRequestId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(booking => booking.Technician).WithMany()
                .HasForeignKey(booking => booking.TechnicianUserId).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<Review>(entity =>
        {
            entity.ToTable("Reviews", table => table.HasCheckConstraint("CK_Reviews_Rating", "[Rating] BETWEEN 1 AND 5"));
            entity.Property(review => review.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasOne(review => review.Booking).WithOne()
                .HasForeignKey<Review>(review => review.BookingId).OnDelete(DeleteBehavior.NoAction);
        });
    }
}
