using RepairConnect.Models.ViewModels;
using RepairConnect.Services.Common;

namespace RepairConnect.Services;

public interface IAdminDashboardService
{
    Task<ServiceResult<AdminDashboardVm>> GetDashboardMetricsAsync(CancellationToken cancellationToken = default);
}
