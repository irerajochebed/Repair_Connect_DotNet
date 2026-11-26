using Microsoft.EntityFrameworkCore;
using RepairConnect.Data;
using RepairConnect.Models;
using RepairConnect.Models.Enums;
using RepairConnect.Models.ViewModels;
using RepairConnect.Services.Common;

namespace RepairConnect.Services;

public sealed class TechnicianDirectoryService(RepairConnectDbContext context) : ITechnicianDirectoryService
{
    private const int RecentReviewCount = 5;

    public async Task<ServiceResult<PaginatedList<TechnicianDirectoryItemVm>>> SearchAsync(
        string? search, int? categoryId, double? minRating, int page, int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (PagingError(page, pageSize) is { } pagingError)
            return ServiceResult<PaginatedList<TechnicianDirectoryItemVm>>.Failure(pagingError);
        if (minRating is < 1 or > 5)
            return ServiceResult<PaginatedList<TechnicianDirectoryItemVm>>.Failure("Minimum rating must be between 1 and 5.");

        var query = Listed();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(t => t.User.DisplayName.Contains(term) || t.Specialization.Contains(term) ||
                (t.Bio != null && t.Bio.Contains(term)) || (t.Address != null && t.Address.Contains(term)));
        }
        if (categoryId.HasValue)
            query = query.Where(t => t.Categories.Any(c => c.CategoryId == categoryId.Value));
        if (minRating.HasValue)
            query = query.Where(t => t.Bookings.Where(b => b.Review != null)
                .Average(b => (double?)b.Review!.Rating) >= minRating.Value);

        var count = await query.CountAsync(cancellationToken);
        var rows = await query
            .Select(t => new
            {
                t.UserId, t.User.DisplayName, t.Specialization, t.YearsOfExperience, t.Address, t.ProfilePhoto,
                t.AvailabilityStatus,
                Categories = t.Categories.Where(c => c.Category.IsActive)
                    .OrderBy(c => c.Category.Name)
                    .Select(c => new TechnicianCategoryVm(c.CategoryId, c.Category.Name)).ToList(),
                AverageRating = t.Bookings.Where(b => b.Review != null).Average(b => (double?)b.Review!.Rating),
                ReviewCount = t.Bookings.Count(b => b.Review != null)
            })
            .OrderByDescending(t => t.AverageRating).ThenByDescending(t => t.ReviewCount)
            .ThenBy(t => t.DisplayName).ThenBy(t => t.UserId)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = rows.Select(t => new TechnicianDirectoryItemVm(t.UserId, t.DisplayName, t.Specialization,
            t.YearsOfExperience, t.Address, t.ProfilePhoto, t.AvailabilityStatus, t.Categories,
            Round(t.AverageRating), t.ReviewCount)).ToList();
        return ServiceResult<PaginatedList<TechnicianDirectoryItemVm>>.Success(new(items, count, page, pageSize));
    }

    public async Task<ServiceResult<TechnicianPublicProfileVm>> GetProfileAsync(
        int technicianUserId, CancellationToken cancellationToken = default)
    {
        var row = await Listed().Where(t => t.UserId == technicianUserId)
            .Select(t => new
            {
                t.UserId, t.User.DisplayName, t.Specialization, t.YearsOfExperience, t.Bio, t.Address, t.ProfilePhoto,
                t.AvailabilityStatus,
                Categories = t.Categories.Where(c => c.Category.IsActive)
                    .OrderBy(c => c.Category.Name)
                    .Select(c => new TechnicianCategoryVm(c.CategoryId, c.Category.Name)).ToList(),
                AverageRating = t.Bookings.Where(b => b.Review != null).Average(b => (double?)b.Review!.Rating),
                ReviewCount = t.Bookings.Count(b => b.Review != null),
                CompletedJobs = t.Bookings.Count(b => b.Status == BookingStatus.Completed)
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (row == null) return ServiceResult<TechnicianPublicProfileVm>.NotFound("Technician not found.");

        var recent = await ReviewsOf(technicianUserId).Take(RecentReviewCount).ToListAsync(cancellationToken);
        return ServiceResult<TechnicianPublicProfileVm>.Success(new(row.UserId, row.DisplayName, row.Specialization,
            row.YearsOfExperience, row.Bio, row.Address, row.ProfilePhoto, row.AvailabilityStatus, row.Categories,
            Round(row.AverageRating), row.ReviewCount, row.CompletedJobs, recent));
    }

    public async Task<ServiceResult<PaginatedList<PublicReviewVm>>> GetReviewsAsync(
        int technicianUserId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        if (PagingError(page, pageSize) is { } pagingError)
            return ServiceResult<PaginatedList<PublicReviewVm>>.Failure(pagingError);
        if (!await Listed().AnyAsync(t => t.UserId == technicianUserId, cancellationToken))
            return ServiceResult<PaginatedList<PublicReviewVm>>.NotFound("Technician not found.");

        var query = ReviewsOf(technicianUserId);
        var count = await context.Reviews.CountAsync(r => r.Booking.TechnicianUserId == technicianUserId, cancellationToken);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return ServiceResult<PaginatedList<PublicReviewVm>>.Success(new(items, count, page, pageSize));
    }

    // Only approved technicians whose account is active appear publicly.
    private IQueryable<TechnicianProfile> Listed() => context.TechnicianProfiles.AsNoTracking()
        .Where(t => t.VerificationStatus == VerificationStatus.Approved && t.User.Status == UserStatus.Active &&
                    (t.User.Roles & UserRoles.Technician) != 0);

    private IQueryable<PublicReviewVm> ReviewsOf(int technicianUserId) => context.Reviews.AsNoTracking()
        .Where(r => r.Booking.TechnicianUserId == technicianUserId)
        .OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id)
        .Select(r => new PublicReviewVm(r.Id, r.Rating, r.Comment, r.Booking.RepairRequest.Client.DisplayName, r.CreatedAt));

    private static double? Round(double? value) => value.HasValue ? Math.Round(value.Value, 1) : null;

    private static string? PagingError(int page, int pageSize) =>
        page < 1 || pageSize < 1 || pageSize > 50 || page > int.MaxValue / pageSize
            ? "Page must be at least 1 and pageSize must be between 1 and 50."
            : null;
}
