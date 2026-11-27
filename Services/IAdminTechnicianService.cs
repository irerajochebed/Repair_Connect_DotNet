using RepairConnect.Models;
using RepairConnect.Models.Enums;
using RepairConnect.Models.ViewModels;
using RepairConnect.Services.Common;

namespace RepairConnect.Services;

public interface IAdminTechnicianService
{
    Task<ServiceResult<PaginatedList<TechnicianReviewRowVm>>> GetTechniciansAsync(
        VerificationStatus? status,
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<TechnicianReviewVm>> GetTechnicianByIdAsync(
        int userId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult> ReviewCategoriesAsync(int userId, int[] ids, bool approve, string? reason, CancellationToken ct);

    Task<ServiceResult> ApproveTechnicianAsync(
        int userId,
        int currentAdminId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult> RejectTechnicianAsync(
        int userId,
        string reason,
        int currentAdminId,
        CancellationToken cancellationToken = default);
}
