using RepairConnect.Models;
using RepairConnect.Models.ViewModels;
using RepairConnect.Services.Common;

namespace RepairConnect.Services;

public interface IRequestService
{
    /// <summary>Saves the repair request and its Pending booking together, or neither.</summary>
    Task<ServiceResult<RepairRequestVm>> CreateAsync(int clientUserId, CreateRepairRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PaginatedList<RepairRequestVm>>> GetMineAsync(int clientUserId, int page, int pageSize,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<RepairRequestVm>> GetByIdAsync(int clientUserId, int requestId,
        CancellationToken cancellationToken = default);
}
