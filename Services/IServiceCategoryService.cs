using RepairConnect.Models;
using RepairConnect.Models.ViewModels;
using RepairConnect.Services.Common;

namespace RepairConnect.Services;

public interface IServiceCategoryService
{
    Task<ServiceResult<IReadOnlyList<ServiceCategoryVm>>> GetActiveAsync(CancellationToken cancellationToken = default);
    Task<ServiceResult<PaginatedList<ServiceCategoryVm>>> GetCategoriesAsync(
        string? search, bool? isActive, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<ServiceResult<ServiceCategoryVm>> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<ServiceResult<ServiceCategoryVm>> CreateAsync(SaveServiceCategoryRequest request, CancellationToken cancellationToken = default);
    Task<ServiceResult<ServiceCategoryVm>> UpdateAsync(int id, SaveServiceCategoryRequest request, CancellationToken cancellationToken = default);
    Task<ServiceResult> DeleteAsync(int id, CancellationToken cancellationToken = default);
}
