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
    private readonly IAccountCreationService _accountCreation;

    public UserAdminService(
        RepairConnectDbContext context,
        IAccountCreationService accountCreation)
    {
        _context = context;
        _accountCreation = accountCreation;
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
        if (!Enum.IsDefined(newStatus))
        {
            return ServiceResult.Failure("Invalid account status.", ServiceErrorType.BadRequest);
        }

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

    public async Task<ServiceResult<UserRowVm>> CreateUserAsync(
        CreateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await _accountCreation.CreateAsync(request, cancellationToken);
        if (!result.Succeeded)
        {
            return ServiceResult<UserRowVm>.Failure(result.Error ?? "Account creation failed.", result.ErrorType);
        }
        var newUser = result.Data!;
        var vm = new UserRowVm
        {
            Id = newUser.Id,
            DisplayName = newUser.DisplayName,
            Email = newUser.Email ?? string.Empty,
            PhoneNumber = newUser.PhoneNumber,
            Roles = newUser.Roles,
            Status = newUser.Status,
            CreatedAt = newUser.CreatedAt
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
