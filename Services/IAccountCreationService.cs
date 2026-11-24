using RepairConnect.Models;
using RepairConnect.Models.ViewModels;
using RepairConnect.Services.Common;

namespace RepairConnect.Services;

public interface IAccountCreationService
{
    Task<ServiceResult<ApplicationUser>> CreateAsync(AccountCreationRequest request, CancellationToken cancellationToken = default);
}
