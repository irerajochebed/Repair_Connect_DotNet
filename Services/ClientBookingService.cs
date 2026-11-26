using Microsoft.EntityFrameworkCore;
using RepairConnect.Data;
using RepairConnect.Models;
using RepairConnect.Models.Enums;
using RepairConnect.Models.ViewModels;
using RepairConnect.Services.Common;

namespace RepairConnect.Services;

public sealed class ClientBookingService(RepairConnectDbContext context, IRequestService requests) : IClientBookingService
{
    private const string ActiveBookingMessage = "This request already has an active booking. Cancel it before choosing another technician.";

    public async Task<ServiceResult<RepairRequestVm>> CancelAsync(int clientUserId, int bookingId,
        CancellationToken cancellationToken = default)
    {
        var booking = await context.Bookings.Include(b => b.RepairRequest)
            .SingleOrDefaultAsync(b => b.Id == bookingId, cancellationToken);
        if (booking == null) return ServiceResult<RepairRequestVm>.NotFound("Booking not found.");
        if (booking.RepairRequest.ClientUserId != clientUserId)
            return ServiceResult<RepairRequestVm>.Failure("Only the client who made the request can cancel this booking.",
                ServiceErrorType.Forbidden);
        if (booking.Status is not (BookingStatus.Pending or BookingStatus.Accepted))
            return ServiceResult<RepairRequestVm>.Failure(
                $"A booking that is {booking.Status} cannot be cancelled.", ServiceErrorType.Conflict);

        booking.Status = BookingStatus.Cancelled;
        booking.EndedAt = DateTime.UtcNow;
        if (booking.RepairRequest.Status == RepairRequestStatus.Booked)
            booking.RepairRequest.Status = RepairRequestStatus.Open;
        await context.SaveChangesAsync(cancellationToken);

        return await requests.GetByIdAsync(clientUserId, booking.RepairRequestId, cancellationToken);
    }

    public async Task<ServiceResult<RepairRequestVm>> ChooseTechnicianAsync(int clientUserId, int requestId,
        int technicianUserId, CancellationToken cancellationToken = default)
    {
        var request = await context.RepairRequests.AsNoTracking()
            .Where(r => r.Id == requestId && r.ClientUserId == clientUserId)
            .Select(r => new
            {
                r.Status, r.CategoryId,
                HasActiveBooking = r.Bookings.Any(b => BookingRules.ActiveStatuses.Contains(b.Status))
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (request == null) return ServiceResult<RepairRequestVm>.NotFound("Repair request not found.");
        if (request.Status != RepairRequestStatus.Open)
            return ServiceResult<RepairRequestVm>.Failure(
                $"A request that is {request.Status} cannot be booked again.", ServiceErrorType.Conflict);
        if (request.HasActiveBooking)
            return ServiceResult<RepairRequestVm>.Failure(ActiveBookingMessage, ServiceErrorType.Conflict);
        if (await BookingRules.TechnicianErrorAsync(context, clientUserId, technicianUserId, request.CategoryId,
                cancellationToken) is { } technicianError)
            return ServiceResult<RepairRequestVm>.Failure(technicianError);

        var booking = new Booking
        {
            RepairRequestId = requestId, TechnicianUserId = technicianUserId,
            Status = BookingStatus.Pending, CreatedAt = DateTime.UtcNow
        };
        context.Bookings.Add(booking);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // The unique index allows one active booking per request; another booking won the race.
            context.Entry(booking).State = EntityState.Detached;
            if (await context.Bookings.AnyAsync(b => b.RepairRequestId == requestId &&
                    BookingRules.ActiveStatuses.Contains(b.Status), cancellationToken))
                return ServiceResult<RepairRequestVm>.Failure(ActiveBookingMessage, ServiceErrorType.Conflict);
            throw;
        }

        return await requests.GetByIdAsync(clientUserId, requestId, cancellationToken);
    }
}
