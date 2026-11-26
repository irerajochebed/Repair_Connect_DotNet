using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RepairConnect.Models;

namespace RepairConnect.Data.Configurations;

public class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> builder)
    {
        builder.ToTable("Reviews", table =>
        {
            table.HasCheckConstraint("CK_Reviews_Rating", "[Rating] BETWEEN 1 AND 5");
        });

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Rating)
            .IsRequired();

        builder.Property(r => r.Comment);

        builder.Property(r => r.CreatedAt)
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasIndex(r => r.BookingId)
            .IsUnique();

        builder.HasOne(r => r.Booking)
            .WithOne(b => b.Review)
            .HasForeignKey<Review>(r => r.BookingId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
