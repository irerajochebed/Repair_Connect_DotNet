using RepairConnect.Models.ViewModels;
using RepairConnect.Services.Common;

namespace RepairConnect.Services;

public interface IReviewService
{
    /// <summary>The request owner reviews a Completed booking, once.</summary>
    Task<ServiceResult<PublicReviewVm>> CreateAsync(int clientUserId, int bookingId, CreateReviewRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Average rating (rounded to 1 decimal) and review count for a technician; null average when unreviewed.</summary>
    Task<(double? Average, int Count)> GetRatingAsync(int technicianUserId, CancellationToken cancellationToken = default);
}
