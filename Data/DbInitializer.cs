using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RepairConnect.Models;
using RepairConnect.Models.Enums;

namespace RepairConnect.Data;

// Creates only an explicitly configured administrator; categories are managed through the application.
public static class DbInitializer
{
    public static async Task SeedAsync(
        RepairConnectDbContext context,
        IConfiguration configuration,
        IPasswordHasher<ApplicationUser> passwordHasher,
        ILogger logger)
    {
        var email = configuration["SeedAdmin:Email"]?.Trim();
        var password = configuration["SeedAdmin:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password))
        {
            return;
        }

        if (!new EmailAddressAttribute().IsValid(email) || email.Length > 256 || password.Length < 6)
        {
            logger.LogWarning("Administrator configuration is invalid; no administrator was created.");
            return;
        }

        var normalizedEmail = email.ToUpperInvariant();
        if (await context.Users.AnyAsync(user => user.NormalizedEmail == normalizedEmail))
        {
            return;
        }

        var displayName = configuration["SeedAdmin:DisplayName"]?.Trim();
        if (string.IsNullOrWhiteSpace(displayName)) displayName = "Administrator";
        if (displayName.Length > 100)
        {
            logger.LogWarning("Administrator display name is invalid; no administrator was created.");
            return;
        }

        var admin = new ApplicationUser
        {
            Email = email,
            NormalizedEmail = normalizedEmail,
            DisplayName = displayName,
            Roles = UserRoles.Admin,
            Status = UserStatus.Active,
            CreatedAt = DateTime.UtcNow
        };
        admin.PasswordHash = passwordHasher.HashPassword(admin, password);
        context.Users.Add(admin);
        await context.SaveChangesAsync();
        logger.LogInformation("Configured administrator account created.");
    }
}
