using RepairConnect.Models.ViewModels;
using RepairConnect.Services.Common;

namespace RepairConnect.Services;

public interface ITechnicianApplicationService
{
    Task<ServiceResult<TechnicianApplicationVm>> GetAsync(int userId, CancellationToken cancellationToken = default);
    Task<ServiceResult<TechnicianApplicationSubmission>> SubmitAsync(int userId, TechnicianApplicationRequest request, CancellationToken cancellationToken = default);
    Task<ServiceResult<StoredTechnicianDocument>> GetDocumentAsync(int documentId, int userId, bool isAdmin, CancellationToken cancellationToken = default);
}

public sealed record TechnicianApplicationSubmission(TechnicianApplicationVm Application, bool Created);
