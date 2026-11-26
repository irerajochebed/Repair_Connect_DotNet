using Microsoft.EntityFrameworkCore;
using RepairConnect.Data;
using RepairConnect.Models;
using RepairConnect.Models.Enums;
using RepairConnect.Models.ViewModels;
using RepairConnect.Services.Common;

namespace RepairConnect.Services;

public sealed class RequestService(RepairConnectDbContext context) : IRequestService
{
    public async Task<ServiceResult<RepairRequestVm>> CreateAsync(int clientUserId, CreateRepairRequest request,
        CancellationToken cancellationToken = default)
    {
        if (ValidationError(request) is { } error) return ServiceResult<RepairRequestVm>.Failure(error);
        var categoryId = request.CategoryId!.Value;
        var technicianUserId = request.TechnicianUserId!.Value;

        if (!await context.ServiceCategories.AnyAsync(c => c.Id == categoryId && c.IsActive, cancellationToken))
            return ServiceResult<RepairRequestVm>.Failure("Choose an available service category.");
        if (await BookingRules.TechnicianErrorAsync(context, clientUserId, technicianUserId, categoryId, cancellationToken)
            is { } technicianError)
            return ServiceResult<RepairRequestVm>.Failure(technicianError);

        var now = DateTime.UtcNow;
        var repairRequest = new RepairRequest
        {
            ClientUserId = clientUserId,
            CategoryId = categoryId,
            Title = request.Title!.Trim(),
            Description = request.Description!.Trim(),
            ServiceLocation = request.ServiceLocation!.Trim(),
            Urgency = request.Urgency,
            PreferredDate = request.PreferredDate,
            Status = RepairRequestStatus.Open,
            CreatedAt = now
        };
        repairRequest.Bookings.Add(new Booking
        {
            TechnicianUserId = technicianUserId,
            Status = BookingStatus.Pending,
            CreatedAt = now
        });

        // One transaction: if the booking cannot be saved, the request is not saved either.
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        context.RepairRequests.Add(repairRequest);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await GetByIdAsync(clientUserId, repairRequest.Id, cancellationToken);
    }

    public async Task<ServiceResult<PaginatedList<RepairRequestVm>>> GetMineAsync(int clientUserId, int page,
        int pageSize, CancellationToken cancellationToken = default)
    {
        if (page < 1 || pageSize < 1 || pageSize > 50 || page > int.MaxValue / pageSize)
            return ServiceResult<PaginatedList<RepairRequestVm>>.Failure(
                "Page must be at least 1 and pageSize must be between 1 and 50.");

        var query = context.RepairRequests.AsNoTracking().Where(r => r.ClientUserId == clientUserId);
        var count = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(BookingRules.RequestProjection).ToListAsync(cancellationToken);
        return ServiceResult<PaginatedList<RepairRequestVm>>.Success(new(items, count, page, pageSize));
    }

    public async Task<ServiceResult<RepairRequestVm>> GetByIdAsync(int clientUserId, int requestId,
        CancellationToken cancellationToken = default)
    {
        // Another client's request is reported as not found, so request IDs reveal nothing.
        var request = await context.RepairRequests.AsNoTracking()
            .Where(r => r.Id == requestId && r.ClientUserId == clientUserId)
            .Select(BookingRules.RequestProjection).SingleOrDefaultAsync(cancellationToken);
        return request == null
            ? ServiceResult<RepairRequestVm>.NotFound("Repair request not found.")
            : ServiceResult<RepairRequestVm>.Success(request);
    }

    private static string? ValidationError(CreateRepairRequest request)
    {
        if (request.CategoryId is null) return "Choose a service category.";
        if (request.TechnicianUserId is null) return "Choose a technician.";
        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Trim().Length > 150)
            return "A title between 1 and 150 characters is required.";
        if (string.IsNullOrWhiteSpace(request.Description) || request.Description.Trim().Length > 4000)
            return "A description between 1 and 4000 characters is required.";
        if (string.IsNullOrWhiteSpace(request.ServiceLocation) || request.ServiceLocation.Trim().Length > 300)
            return "A service location between 1 and 300 characters is required.";
        if (!Enum.IsDefined(request.Urgency)) return "Urgency must be Low, Medium or High.";
        if (request.PreferredDate is { } date && date < DateOnly.FromDateTime(DateTime.UtcNow.Date))
            return "The preferred date cannot be in the past.";
        return null;
    }
}
