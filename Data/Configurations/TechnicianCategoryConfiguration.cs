using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RepairConnect.Models;

namespace RepairConnect.Data.Configurations;

public class TechnicianCategoryConfiguration : IEntityTypeConfiguration<TechnicianCategory>
{
    public void Configure(EntityTypeBuilder<TechnicianCategory> builder)
    {
        builder.ToTable("TechnicianCategories");

        builder.HasKey(tc => new { tc.TechnicianUserId, tc.CategoryId });

        builder.HasOne(tc => tc.Technician)
            .WithMany(tp => tp.Categories)
            .HasForeignKey(tc => tc.TechnicianUserId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(tc => tc.Category)
            .WithMany(sc => sc.TechnicianCategories)
            .HasForeignKey(tc => tc.CategoryId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
