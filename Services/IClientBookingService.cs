using RepairConnect.Models.ViewModels;
using RepairConnect.Services.Common;

namespace RepairConnect.Services;

public interface IClientBookingService
{
    /// <summary>The request owner cancels a Pending or Accepted booking; the request reopens.</summary>
    Task<ServiceResult<RepairRequestVm>> CancelAsync(int clientUserId, int bookingId,
        CancellationToken cancellationToken = default);

    /// <summary>Books another technician for an open request that has no active booking.</summary>
    Task<ServiceResult<RepairRequestVm>> ChooseTechnicianAsync(int clientUserId, int requestId, int technicianUserId,
        CancellationToken cancellationToken = default);
}
