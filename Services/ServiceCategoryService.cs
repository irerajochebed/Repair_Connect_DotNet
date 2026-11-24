using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using RepairConnect.Data;
using RepairConnect.Models;
using RepairConnect.Models.ViewModels;
using RepairConnect.Services.Common;

namespace RepairConnect.Services;

public sealed class ServiceCategoryService(RepairConnectDbContext context) : IServiceCategoryService
{
    private const string DuplicateMessage = "A service category with this name already exists.";
    private const string ReferencedMessage = "This service category is used by repair requests or technicians. Deactivate it instead of deleting it.";
    private static readonly Expression<Func<ServiceCategory, ServiceCategoryVm>> Projection =
        category => new ServiceCategoryVm(category.Id, category.Name, category.Description, category.IsActive);

    public async Task<ServiceResult<IReadOnlyList<ServiceCategoryVm>>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        var categories = await context.ServiceCategories.AsNoTracking()
            .Where(category => category.IsActive).OrderBy(category => category.Name).ThenBy(category => category.Id)
            .Select(Projection).ToListAsync(cancellationToken);
        return ServiceResult<IReadOnlyList<ServiceCategoryVm>>.Success(categories);
    }

    public async Task<ServiceResult<PaginatedList<ServiceCategoryVm>>> GetCategoriesAsync(
        string? search, bool? isActive, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        if (page < 1 || pageSize < 1 || pageSize > 50 || page > int.MaxValue / pageSize)
            return ServiceResult<PaginatedList<ServiceCategoryVm>>.Failure("Page must be at least 1 and pageSize must be between 1 and 50.");

        var query = context.ServiceCategories.AsNoTracking();
        if (isActive.HasValue) query = query.Where(category => category.IsActive == isActive.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(category => category.Name.Contains(term) ||
                (category.Description != null && category.Description.Contains(term)));
        }

        var count = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(category => category.Name).ThenBy(category => category.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).Select(Projection).ToListAsync(cancellationToken);
        return ServiceResult<PaginatedList<ServiceCategoryVm>>.Success(new(items, count, page, pageSize));
    }

    public async Task<ServiceResult<ServiceCategoryVm>> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var category = await context.ServiceCategories.AsNoTracking().Where(category => category.Id == id)
            .Select(Projection).SingleOrDefaultAsync(cancellationToken);
        return category == null ? ServiceResult<ServiceCategoryVm>.NotFound("Service category not found.")
            : ServiceResult<ServiceCategoryVm>.Success(category);
    }

    public async Task<ServiceResult<ServiceCategoryVm>> CreateAsync(SaveServiceCategoryRequest request, CancellationToken cancellationToken = default)
    {
        if (ValidationError(request) is { } error) return ServiceResult<ServiceCategoryVm>.Failure(error);
        var name = request.Name!.Trim();
        if (await NameExistsAsync(name, null, cancellationToken)) return Duplicate();
        var category = new ServiceCategory
        {
            Name = name, Description = CleanDescription(request.Description), IsActive = request.IsActive!.Value
        };
        context.ServiceCategories.Add(category);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            context.Entry(category).State = EntityState.Detached;
            if (await NameExistsAsync(name, null, cancellationToken)) return Duplicate();
            throw;
        }
        return ServiceResult<ServiceCategoryVm>.Success(ToVm(category));
    }

    public async Task<ServiceResult<ServiceCategoryVm>> UpdateAsync(int id, SaveServiceCategoryRequest request, CancellationToken cancellationToken = default)
    {
        if (ValidationError(request) is { } error) return ServiceResult<ServiceCategoryVm>.Failure(error);
        var category = await context.ServiceCategories.SingleOrDefaultAsync(category => category.Id == id, cancellationToken);
        if (category == null) return ServiceResult<ServiceCategoryVm>.NotFound("Service category not found.");
        var name = request.Name!.Trim();
        if (await NameExistsAsync(name, id, cancellationToken)) return Duplicate();
        category.Name = name;
        category.Description = CleanDescription(request.Description);
        category.IsActive = request.IsActive!.Value;
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            context.Entry(category).State = EntityState.Detached;
            return ServiceResult<ServiceCategoryVm>.NotFound("Service category no longer exists.");
        }
        catch (DbUpdateException)
        {
            context.Entry(category).State = EntityState.Detached;
            if (await NameExistsAsync(name, id, cancellationToken)) return Duplicate();
            throw;
        }
        return ServiceResult<ServiceCategoryVm>.Success(ToVm(category));
    }

    public async Task<ServiceResult> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var category = await context.ServiceCategories.SingleOrDefaultAsync(category => category.Id == id, cancellationToken);
        if (category == null) return ServiceResult.NotFound("Service category not found.");
        if (await IsReferencedAsync(id, cancellationToken)) return Referenced();
        context.ServiceCategories.Remove(category);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            context.Entry(category).State = EntityState.Detached;
            return ServiceResult.NotFound("Service category no longer exists.");
        }
        catch (DbUpdateException)
        {
            context.Entry(category).State = EntityState.Detached;
            // A relation may have been added between the initial check and the delete.
            if (await IsReferencedAsync(id, cancellationToken)) return Referenced();
            throw;
        }
        return ServiceResult.Success();
    }

    private Task<bool> NameExistsAsync(string name, int? exceptId, CancellationToken cancellationToken)
    {
        var normalizedName = name.ToUpperInvariant();
        var query = context.ServiceCategories.AsNoTracking();
        if (exceptId.HasValue) query = query.Where(category => category.Id != exceptId.Value);
        return query.AnyAsync(category => category.Name.Trim().ToUpper() == normalizedName, cancellationToken);
    }

    private async Task<bool> IsReferencedAsync(int id, CancellationToken cancellationToken) =>
        await context.RepairRequests.AnyAsync(request => request.CategoryId == id, cancellationToken) ||
        await context.TechnicianCategories.AnyAsync(category => category.CategoryId == id, cancellationToken);

    private static string? ValidationError(SaveServiceCategoryRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 100)
            return "A service name between 1 and 100 characters is required.";
        if (request.Description?.Length > 1000) return "Description must not exceed 1000 characters.";
        if (!request.IsActive.HasValue) return "Active status is required.";
        return null;
    }

    private static string? CleanDescription(string? description) =>
        string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    private static ServiceCategoryVm ToVm(ServiceCategory category) => new(category.Id, category.Name, category.Description, category.IsActive);
    private static ServiceResult<ServiceCategoryVm> Duplicate() => ServiceResult<ServiceCategoryVm>.Failure(DuplicateMessage, ServiceErrorType.Conflict);
    private static ServiceResult Referenced() => ServiceResult.Failure(ReferencedMessage, ServiceErrorType.Conflict);
}
