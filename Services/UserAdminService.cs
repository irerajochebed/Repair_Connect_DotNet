using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RepairConnect.Data;
using RepairConnect.Models;
using RepairConnect.Models.Enums;
using RepairConnect.Models.ViewModels;
using RepairConnect.Services.Common;

namespace RepairConnect.Services;

public class UserAdminService : IUserAdminService
{
    private readonly RepairConnectDbContext _context;
    private readonly IPasswordHasher<ApplicationUser> _passwordHasher;

    public UserAdminService(
        RepairConnectDbContext context,
        IPasswordHasher<ApplicationUser> passwordHasher)
    {
        _context = context;
        _passwordHasher = passwordHasher;
    }

    public async Task<ServiceResult<PaginatedList<UserRowVm>>> GetUsersAsync(
        UserRoles? role,
        UserStatus? status,
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 50) pageSize = 50;

        var query = _context.Users.AsQueryable();

        if (role.HasValue && role.Value != UserRoles.None)
        {
            query = query.Where(u => (u.Roles & role.Value) == role.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(u => u.Status == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var trimmed = search.Trim();
            query = query.Where(u =>
                u.DisplayName.Contains(trimmed) ||
                (u.Email != null && u.Email.Contains(trimmed)) ||
                (u.PhoneNumber != null && u.PhoneNumber.Contains(trimmed)));
        }

        query = query.OrderByDescending(u => u.CreatedAt);

        var count = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new UserRowVm
            {
                Id = u.Id,
                DisplayName = u.DisplayName,
                Email = u.Email ?? string.Empty,
                PhoneNumber = u.PhoneNumber,
                Roles = u.Roles,
                Status = u.Status,
                CreatedAt = u.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var paginatedList = new PaginatedList<UserRowVm>(items, count, page, pageSize);
        return ServiceResult<PaginatedList<UserRowVm>>.Success(paginatedList);
    }

    public async Task<ServiceResult> UpdateUserStatusAsync(
        int targetUserId,
        UserStatus newStatus,
        int currentAdminId,
        CancellationToken cancellationToken = default)
    {
        if (targetUserId == currentAdminId && newStatus == UserStatus.Inactive)
        {
            return ServiceResult.Failure(
                "An administrator cannot deactivate their own account.",
                ServiceErrorType.BadRequest);
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == targetUserId, cancellationToken);
        if (user == null)
        {
            return ServiceResult.NotFound("User not found.");
        }

        user.Status = newStatus;
        await _context.SaveChangesAsync(cancellationToken);
        return ServiceResult.Success();
    }

    public async Task<ServiceResult<UserRowVm>> CreateAdminAsync(
        CreateAdminRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = request.Email.Trim().ToUpperInvariant();
        var exists = await _context.Users.AnyAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);
        if (exists)
        {
            return ServiceResult<UserRowVm>.Failure(
                "A user with this email already exists.",
                ServiceErrorType.Conflict);
        }

        var newAdmin = new ApplicationUser
        {
            DisplayName = request.DisplayName.Trim(),
            Email = request.Email.Trim(),
            NormalizedEmail = normalizedEmail,
            PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim(),
            Roles = UserRoles.Admin,
            Status = UserStatus.Active,
            CreatedAt = DateTime.UtcNow
        };

        newAdmin.PasswordHash = _passwordHasher.HashPassword(newAdmin, request.Password);

        _context.Users.Add(newAdmin);
        await _context.SaveChangesAsync(cancellationToken);

        var vm = new UserRowVm
        {
            Id = newAdmin.Id,
            DisplayName = newAdmin.DisplayName,
            Email = newAdmin.Email,
            PhoneNumber = newAdmin.PhoneNumber,
            Roles = newAdmin.Roles,
            Status = newAdmin.Status,
            CreatedAt = newAdmin.CreatedAt
        };

        return ServiceResult<UserRowVm>.Success(vm);
    }

    public async Task<ServiceResult> SoftDeleteUserAsync(
        int targetUserId,
        int currentAdminId,
        CancellationToken cancellationToken = default)
    {
        if (targetUserId == currentAdminId)
        {
            return ServiceResult.Failure(
                "An administrator cannot delete their own account.",
                ServiceErrorType.BadRequest);
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == targetUserId, cancellationToken);
        if (user == null)
        {
            return ServiceResult.NotFound("User not found.");
        }

        user.Status = UserStatus.Inactive;
        await _context.SaveChangesAsync(cancellationToken);
        return ServiceResult.Success();
    }
}
