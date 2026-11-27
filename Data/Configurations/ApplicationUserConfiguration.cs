using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RepairConnect.Models;
using RepairConnect.Models.Enums;

namespace RepairConnect.Data.Configurations;

public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.ToTable("Users", table =>
        {
            table.HasCheckConstraint("CK_Users_Roles", "[Roles] BETWEEN 0 AND 7");
            table.HasCheckConstraint("CK_Users_Status", "[Status] IN (N'Active', N'Inactive')");
        });

        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).UseIdentityColumn();

        builder.Property(u => u.Email)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(u => u.NormalizedEmail)
            .HasMaxLength(256)
            .IsRequired();

        builder.HasIndex(u => u.NormalizedEmail)
            .IsUnique();

        builder.Property(u => u.PasswordHash)
            .HasColumnName("Password")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(u => u.DisplayName)
            .HasColumnName("Username")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(u => u.PhoneNumber)
            .HasColumnName("Phone")
            .HasMaxLength(20);

        builder.Property(u => u.Roles)
            .IsRequired();

        builder.Property(u => u.Status)
            .HasConversion<string>()
            .HasMaxLength(16)
            .HasDefaultValue(UserStatus.Active);

        builder.Property(u => u.Address);

        builder.Property(u => u.CreatedAt)
            .HasDefaultValueSql("SYSUTCDATETIME()");

        // Ignore unused IdentityUser properties so Users table strictly matches schema
        builder.Ignore(u => u.UserName);
        builder.Ignore(u => u.NormalizedUserName);
        builder.Ignore(u => u.EmailConfirmed);
        builder.Ignore(u => u.PhoneNumberConfirmed);
        builder.Ignore(u => u.TwoFactorEnabled);
        builder.Ignore(u => u.LockoutEnd);
        builder.Ignore(u => u.LockoutEnabled);
        builder.Ignore(u => u.AccessFailedCount);
        builder.Ignore(u => u.ConcurrencyStamp);
        builder.Ignore(u => u.SecurityStamp);
    }
}
