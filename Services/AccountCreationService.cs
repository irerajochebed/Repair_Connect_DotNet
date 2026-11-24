using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RepairConnect.Data;
using RepairConnect.Models;
using RepairConnect.Models.Enums;
using RepairConnect.Models.ViewModels;
using RepairConnect.Services.Common;

namespace RepairConnect.Services;

public sealed class AccountCreationService(RepairConnectDbContext context, IPasswordHasher<ApplicationUser> passwordHasher)
    : IAccountCreationService
{
    public async Task<ServiceResult<ApplicationUser>> CreateAsync(AccountCreationRequest request, CancellationToken cancellationToken = default)
    {
        var errors = new List<ValidationResult>();
        if (!Validator.TryValidateObject(request, new ValidationContext(request), errors, validateAllProperties: true))
            return ServiceResult<ApplicationUser>.Failure(errors[0].ErrorMessage ?? "Check the account details.");

        var normalizedEmail = request.Email!.ToUpperInvariant();
        if (await EmailExistsAsync(normalizedEmail, cancellationToken)) return Duplicate();
        var user = new ApplicationUser
        {
            DisplayName = request.DisplayName!,
            Email = request.Email,
            NormalizedEmail = normalizedEmail,
            PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber,
            Roles = UserRoles.Client,
            Status = UserStatus.Active,
            CreatedAt = DateTime.UtcNow
        };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password!);

        context.Users.Add(user);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            context.Entry(user).State = EntityState.Detached;
            // The database unique index settles duplicate-email races after the initial lookup.
            if (await EmailExistsAsync(normalizedEmail, cancellationToken)) return Duplicate();
            throw;
        }
        return ServiceResult<ApplicationUser>.Success(user);
    }

    private Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        context.Users.AsNoTracking().AnyAsync(user => user.NormalizedEmail == normalizedEmail, cancellationToken);

    private static ServiceResult<ApplicationUser> Duplicate() => ServiceResult<ApplicationUser>.Failure(
        "A user with this email already exists.", ServiceErrorType.Conflict);
}
