using Microsoft.EntityFrameworkCore;
using RepairConnect.Data;
using RepairConnect.Models;
using RepairConnect.Models.Enums;
using RepairConnect.Models.ViewModels;
using RepairConnect.Services.Common;

namespace RepairConnect.Services;

public sealed class ReviewService(RepairConnectDbContext context) : IReviewService
{
    private const string DuplicateMessage = "This booking has already been reviewed.";

    public async Task<ServiceResult<PublicReviewVm>> CreateAsync(int clientUserId, int bookingId,
        CreateReviewRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Rating is not (>= 1 and <= 5))
            return ServiceResult<PublicReviewVm>.Failure("Rating must be a whole number from 1 to 5.");
        var comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim();
        if (comment?.Length > 1000) return ServiceResult<PublicReviewVm>.Failure("Comment must not exceed 1000 characters.");

        var booking = await context.Bookings.AsNoTracking()
            .Where(b => b.Id == bookingId)
            .Select(b => new
            {
                b.Status, b.RepairRequest.ClientUserId, ClientName = b.RepairRequest.Client.DisplayName,
                HasReview = b.Review != null
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (booking == null) return ServiceResult<PublicReviewVm>.NotFound("Booking not found.");
        if (booking.ClientUserId != clientUserId)
            return ServiceResult<PublicReviewVm>.Failure("Only the client who made the request can review this booking.",
                ServiceErrorType.Forbidden);
        if (booking.Status != BookingStatus.Completed)
            return ServiceResult<PublicReviewVm>.Failure("You can review a booking only after the work is completed.",
                ServiceErrorType.Conflict);
        if (booking.HasReview) return Duplicate();

        var review = new Review
        {
            BookingId = bookingId, Rating = (byte)request.Rating.Value, Comment = comment, CreatedAt = DateTime.UtcNow
        };
        context.Reviews.Add(review);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // The unique index on BookingId stops a second review saved at the same moment.
            context.Entry(review).State = EntityState.Detached;
            if (await context.Reviews.AnyAsync(r => r.BookingId == bookingId, cancellationToken)) return Duplicate();
            throw;
        }

        return ServiceResult<PublicReviewVm>.Success(
            new(review.Id, review.Rating, review.Comment, booking.ClientName, review.CreatedAt));
    }

    public async Task<(double? Average, int Count)> GetRatingAsync(int technicianUserId,
        CancellationToken cancellationToken = default)
    {
        var ratings = context.Reviews.AsNoTracking().Where(r => r.Booking.TechnicianUserId == technicianUserId);
        var count = await ratings.CountAsync(cancellationToken);
        var average = await ratings.AverageAsync(r => (double?)r.Rating, cancellationToken);
        return (average.HasValue ? Math.Round(average.Value, 1) : null, count);
    }

    private static ServiceResult<PublicReviewVm> Duplicate() =>
        ServiceResult<PublicReviewVm>.Failure(DuplicateMessage, ServiceErrorType.Conflict);
}
