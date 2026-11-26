using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RepairConnect.Models;
using RepairConnect.Models.Enums;

namespace RepairConnect.Data.Configurations;

public class TechnicianProfileConfiguration : IEntityTypeConfiguration<TechnicianProfile>
{
    public void Configure(EntityTypeBuilder<TechnicianProfile> builder)
    {
        builder.ToTable("TechnicianProfiles", table =>
        {
            table.HasCheckConstraint("CK_TechnicianProfiles_YearsOfExperience", "[YearsOfExperience] >= 0");
            table.HasCheckConstraint("CK_TechnicianProfiles_VerificationStatus",
                "[VerificationStatus] IN (N'Pending', N'Approved', N'Rejected')");
            table.HasCheckConstraint("CK_TechnicianProfiles_AvailabilityStatus",
                "[AvailabilityStatus] IN (N'Available', N'Busy', N'Offline')");
        });

        builder.HasKey(tp => tp.UserId);
        builder.Property(tp => tp.UserId).ValueGeneratedNever();

        builder.Property(tp => tp.Specialization)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(tp => tp.YearsOfExperience)
            .IsRequired();

        builder.Property(tp => tp.Bio);
        builder.Property(tp => tp.ProfilePhoto).HasMaxLength(512);
        builder.Property(tp => tp.Address);

        builder.Property(tp => tp.VerificationStatus)
            .HasConversion<string>()
            .HasMaxLength(16)
            .HasDefaultValue(VerificationStatus.Pending);

        builder.Property(tp => tp.RejectionReason);

        builder.Property(tp => tp.ReviewedAt);

        builder.Property(tp => tp.AvailabilityStatus)
            .HasConversion<string>()
            .HasMaxLength(16)
            .HasDefaultValue(AvailabilityStatus.Available);

        builder.Property(tp => tp.CreatedAt)
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasOne(tp => tp.User)
            .WithOne(u => u.TechnicianProfile)
            .HasForeignKey<TechnicianProfile>(tp => tp.UserId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(tp => tp.ReviewedByUser)
            .WithMany(u => u.ReviewedProfiles)
            .HasForeignKey(tp => tp.ReviewedByUserId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
