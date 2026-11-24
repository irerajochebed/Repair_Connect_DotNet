using System.Data;
using Microsoft.EntityFrameworkCore;
using RepairConnect.Data;
using RepairConnect.Models;
using RepairConnect.Models.Enums;
using RepairConnect.Models.ViewModels;
using RepairConnect.Services.Common;

namespace RepairConnect.Services;

public class AdminTechnicianService : IAdminTechnicianService
{
    private readonly RepairConnectDbContext _context;

    public AdminTechnicianService(RepairConnectDbContext context)
    {
        _context = context;
    }

    public async Task<ServiceResult<PaginatedList<TechnicianReviewRowVm>>> GetTechniciansAsync(
        VerificationStatus? status,
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 50) pageSize = 50;

        var targetStatus = status ?? VerificationStatus.Pending;

        var query = _context.TechnicianProfiles
            .Include(tp => tp.User)
            .Include(tp => tp.Documents)
            .Where(tp => tp.VerificationStatus == targetStatus);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var trimmed = search.Trim();
            query = query.Where(tp =>
                tp.User.DisplayName.Contains(trimmed) ||
                tp.Specialization.Contains(trimmed));
        }

        query = query.OrderByDescending(tp => tp.CreatedAt);

        var count = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(tp => new TechnicianReviewRowVm
            {
                UserId = tp.UserId,
                DisplayName = tp.User.DisplayName,
                Specialization = tp.Specialization,
                YearsOfExperience = tp.YearsOfExperience,
                VerificationStatus = tp.VerificationStatus,
                DocumentCount = tp.Documents.Count,
                CreatedAt = tp.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var paginatedList = new PaginatedList<TechnicianReviewRowVm>(items, count, page, pageSize);
        return ServiceResult<PaginatedList<TechnicianReviewRowVm>>.Success(paginatedList);
    }

    public async Task<ServiceResult<TechnicianReviewVm>> GetTechnicianByIdAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        var profile = await _context.TechnicianProfiles
            .Include(tp => tp.User)
            .Include(tp => tp.ReviewedByUser)
            .Include(tp => tp.Documents)
            .Include(tp => tp.Categories)
                .ThenInclude(tc => tc.Category)
            .FirstOrDefaultAsync(tp => tp.UserId == userId, cancellationToken);

        if (profile == null)
        {
            return ServiceResult<TechnicianReviewVm>.NotFound("Technician profile not found.");
        }

        var vm = new TechnicianReviewVm
        {
            UserId = profile.UserId,
            DisplayName = profile.User.DisplayName,
            Email = profile.User.Email ?? string.Empty,
            PhoneNumber = profile.User.PhoneNumber,
            Specialization = profile.Specialization,
            Bio = profile.Bio,
            YearsOfExperience = profile.YearsOfExperience,
            Address = profile.Address ?? profile.User.Address,
            VerificationStatus = profile.VerificationStatus,
            RejectionReason = profile.RejectionReason,
            ReviewedByName = profile.ReviewedByUser?.DisplayName,
            ReviewedAt = profile.ReviewedAt,
            Categories = profile.Categories.Select(c => c.Category.Name).ToList(),
            Documents = profile.Documents.Select(d => new DocumentVm
            {
                Id = d.Id,
                FileName = $"Qualification{System.IO.Path.GetExtension(d.Document)}",
                DownloadUrl = $"/api/technician-application/documents/{d.Id}",
                UploadedAt = d.UploadedAt
            }).ToList()
        };

        return ServiceResult<TechnicianReviewVm>.Success(vm);
    }

    public async Task<ServiceResult> ApproveTechnicianAsync(
        int userId,
        int currentAdminId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var profile = await _context.TechnicianProfiles
            .Include(tp => tp.User)
            .Include(tp => tp.Documents)
            .FirstOrDefaultAsync(tp => tp.UserId == userId, cancellationToken);

        if (profile == null)
        {
            return ServiceResult.NotFound("Technician profile not found.");
        }

        if (profile.Documents.Count == 0)
        {
            return ServiceResult.Failure(
                "Technician cannot be approved without uploaded documents.",
                ServiceErrorType.BadRequest);
        }

        if (profile.User.Status != UserStatus.Active)
        {
            return ServiceResult.Failure("Activate the applicant's account before approving it.");
        }

        profile.VerificationStatus = VerificationStatus.Approved;
        profile.User.Roles |= UserRoles.Client | UserRoles.Technician;
        profile.ReviewedByUserId = currentAdminId;
        profile.ReviewedAt = DateTime.UtcNow;
        profile.RejectionReason = null;

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ServiceResult.Success();
    }

    public async Task<ServiceResult> RejectTechnicianAsync(
        int userId,
        string reason,
        int currentAdminId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 2000)
        {
            return ServiceResult.Failure(
                "A rejection reason of at most 2000 characters is required.",
                ServiceErrorType.BadRequest);
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var profile = await _context.TechnicianProfiles
            .Include(tp => tp.User)
            .FirstOrDefaultAsync(tp => tp.UserId == userId, cancellationToken);

        if (profile == null)
        {
            return ServiceResult.NotFound("Technician profile not found.");
        }

        profile.VerificationStatus = VerificationStatus.Rejected;
        profile.AvailabilityStatus = AvailabilityStatus.Offline;
        profile.User.Roles = (profile.User.Roles | UserRoles.Client) & ~UserRoles.Technician;
        profile.RejectionReason = reason.Trim();
        profile.ReviewedByUserId = currentAdminId;
        profile.ReviewedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ServiceResult.Success();
    }
}
