using RepairConnect.Models;
using RepairConnect.Models.Enums;
using RepairConnect.Models.ViewModels;
using RepairConnect.Services.Common;

namespace RepairConnect.Services;

public interface IUserAdminService
{
    Task<ServiceResult<PaginatedList<UserRowVm>>> GetUsersAsync(
        UserRoles? role,
        UserStatus? status,
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<ServiceResult> UpdateUserStatusAsync(
        int targetUserId,
        UserStatus newStatus,
        int currentAdminId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<UserRowVm>> CreateAdminAsync(
        CreateAdminRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult> SoftDeleteUserAsync(
        int targetUserId,
        int currentAdminId,
        CancellationToken cancellationToken = default);
}
