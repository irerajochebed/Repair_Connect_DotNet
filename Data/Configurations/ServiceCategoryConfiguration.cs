using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RepairConnect.Models;

namespace RepairConnect.Data.Configurations;

public class ServiceCategoryConfiguration : IEntityTypeConfiguration<ServiceCategory>
{
    public void Configure(EntityTypeBuilder<ServiceCategory> builder)
    {
        builder.ToTable("ServiceCategories");

        builder.HasKey(sc => sc.Id);

        builder.Property(sc => sc.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(sc => sc.Name)
            .IsUnique();

        builder.Property(sc => sc.Description);

        builder.Property(sc => sc.IsActive)
            .HasDefaultValue(true);
    }
}
