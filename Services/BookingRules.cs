using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using RepairConnect.Data;
using RepairConnect.Models;
using RepairConnect.Models.Enums;
using RepairConnect.Models.ViewModels;

namespace RepairConnect.Services;

// Rules shared by request creation and client booking changes.
internal static class BookingRules
{
    public static readonly BookingStatus[] ActiveStatuses =
        [BookingStatus.Pending, BookingStatus.Accepted, BookingStatus.InProgress];

    public static readonly Expression<Func<RepairRequest, RepairRequestVm>> RequestProjection = request =>
        new RepairRequestVm(request.Id, request.Title, request.Description, request.ServiceLocation,
            request.CategoryId, request.Category.Name, request.Urgency, request.PreferredDate, request.Status,
            request.CreatedAt,
            request.Bookings.OrderByDescending(b => b.CreatedAt).ThenByDescending(b => b.Id)
                .Select(b => new BookingVm(b.Id, b.TechnicianUserId, b.Technician.User.DisplayName, b.Status,
                    b.ScheduledDate, b.CreatedAt, b.CompletedAt, b.Review != null))
                .ToList());

    /// <summary>Returns an error message when the client may not book this technician for this category.</summary>
    public static async Task<string?> TechnicianErrorAsync(RepairConnectDbContext context, int clientUserId,
        int technicianUserId, int categoryId, CancellationToken cancellationToken)
    {
        if (technicianUserId == clientUserId) return "You cannot book yourself for your own repair request.";

        var technician = await context.TechnicianProfiles.AsNoTracking()
            .Where(t => t.UserId == technicianUserId)
            .Select(t => new
            {
                t.VerificationStatus, t.AvailabilityStatus, t.User.Status, t.User.Roles,
                OffersCategory = t.Categories.Any(c => c.CategoryId == categoryId)
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (technician == null || technician.VerificationStatus != VerificationStatus.Approved ||
            technician.Status != UserStatus.Active || (technician.Roles & UserRoles.Technician) == 0)
            return "This technician is not available for booking.";
        if (technician.AvailabilityStatus == AvailabilityStatus.Offline)
            return "This technician is offline and is not taking bookings.";
        if (!technician.OffersCategory) return "This technician does not offer the selected service.";
        return null;
    }
}
