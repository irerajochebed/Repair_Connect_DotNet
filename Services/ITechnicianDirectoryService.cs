using RepairConnect.Models;
using RepairConnect.Models.ViewModels;
using RepairConnect.Services.Common;

namespace RepairConnect.Services;

public interface ITechnicianDirectoryService
{
    /// <summary>Approved, active technicians, filtered by text, category and minimum average rating.</summary>
    Task<ServiceResult<PaginatedList<TechnicianDirectoryItemVm>>> SearchAsync(
        string? search, int? categoryId, double? minRating, int page, int pageSize,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<TechnicianPublicProfileVm>> GetProfileAsync(int technicianUserId, CancellationToken cancellationToken = default);

    Task<ServiceResult<PaginatedList<PublicReviewVm>>> GetReviewsAsync(
        int technicianUserId, int page, int pageSize, CancellationToken cancellationToken = default);
}
