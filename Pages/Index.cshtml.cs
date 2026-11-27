using Microsoft.AspNetCore.Mvc.RazorPages;
using RepairConnect.Models.ViewModels;
using RepairConnect.Services;

namespace RepairConnect.Pages;

public class IndexModel(IServiceCategoryService catalog, ILogger<IndexModel> logger) : PageModel
{
    public IReadOnlyList<ServiceCategoryVm> Categories { get; private set; } = [];
    public bool CatalogUnavailable { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await catalog.GetActiveAsync(cancellationToken);
            if (result.Succeeded && result.Data is not null)
            {
                Categories = result.Data;
            }
            else
            {
                CatalogUnavailable = true;
                logger.LogWarning("The service catalog could not be loaded.");
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            CatalogUnavailable = true;
            logger.LogWarning(exception, "The service catalog could not be loaded.");
        }
    }
}
