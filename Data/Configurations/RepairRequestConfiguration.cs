using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RepairConnect.Models;
using RepairConnect.Models.Enums;

namespace RepairConnect.Data.Configurations;

public class RepairRequestConfiguration : IEntityTypeConfiguration<RepairRequest>
{
    public void Configure(EntityTypeBuilder<RepairRequest> builder)
    {
        builder.ToTable("RepairRequests", table =>
        {
            table.HasCheckConstraint("CK_RepairRequests_Status",
                "[Status] IN (N'Open', N'Booked', N'Completed', N'Cancelled')");
            table.HasCheckConstraint("CK_RepairRequests_Urgency",
                "[Urgency] IN (N'Low', N'Medium', N'High')");
        });

        builder.HasKey(rr => rr.Id);

        builder.Property(rr => rr.Title)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(rr => rr.Description)
            .IsRequired();

        builder.Property(rr => rr.ServiceLocation)
            .IsRequired();

        builder.Property(rr => rr.Urgency)
            .HasConversion<string>()
            .HasMaxLength(16)
            .HasDefaultValue(RepairUrgency.Medium)
            .HasSentinel(RepairUrgency.Medium);

        builder.Property(rr => rr.PreferredDate);

        builder.Property(rr => rr.Status)
            .HasConversion<string>()
            .HasMaxLength(16)
            .HasDefaultValue(RepairRequestStatus.Open);

        builder.Property(rr => rr.CreatedAt)
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasOne(rr => rr.Client)
            .WithMany(u => u.RepairRequests)
            .HasForeignKey(rr => rr.ClientUserId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(rr => rr.Category)
            .WithMany(sc => sc.RepairRequests)
            .HasForeignKey(rr => rr.CategoryId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
