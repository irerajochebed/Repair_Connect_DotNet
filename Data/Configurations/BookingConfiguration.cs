using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RepairConnect.Models;
using RepairConnect.Models.Enums;

namespace RepairConnect.Data.Configurations;

public class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> builder)
    {
        builder.ToTable("Bookings", table =>
        {
            table.HasCheckConstraint("CK_Bookings_Status",
                "[Status] IN (N'Pending', N'Accepted', N'Declined', N'InProgress', N'Completed', N'Cancelled')");
        });

        builder.HasKey(b => b.Id);

        builder.Property(b => b.Status)
            .HasConversion<string>()
            .HasMaxLength(16)
            .HasDefaultValue(BookingStatus.Pending);

        builder.Property(b => b.ScheduledDate);
        builder.Property(b => b.AssignedAt);
        builder.Property(b => b.EndedAt);
        builder.Property(b => b.CompletedAt);

        builder.Property(b => b.CreatedAt)
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasIndex(b => b.RepairRequestId);
        builder.HasIndex(b => b.RepairRequestId, "UX_Bookings_ActiveRepairRequest")
            .IsUnique()
            .HasFilter("[Status] IN (N'Pending', N'Accepted', N'InProgress')");

        builder.HasOne(b => b.RepairRequest)
            .WithMany(rr => rr.Bookings)
            .HasForeignKey(b => b.RepairRequestId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(b => b.Technician)
            .WithMany(tp => tp.Bookings)
            .HasForeignKey(b => b.TechnicianUserId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
