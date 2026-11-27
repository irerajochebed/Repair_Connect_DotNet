using System.ComponentModel.DataAnnotations;
using System.Data;
using Microsoft.EntityFrameworkCore;
using RepairConnect.Data;
using RepairConnect.Models;
using RepairConnect.Models.Enums;
using RepairConnect.Models.ViewModels;
using RepairConnect.Services.Common;

namespace RepairConnect.Services;

public sealed class TechnicianApplicationService(
    RepairConnectDbContext context,
    ITechnicianDocumentStorage storage,
    ILogger<TechnicianApplicationService> logger) : ITechnicianApplicationService
{
    public async Task<ServiceResult<TechnicianApplicationVm>> GetAsync(int userId, CancellationToken cancellationToken = default)
    {
        var profile = await Profiles().AsNoTracking().SingleOrDefaultAsync(profile => profile.UserId == userId, cancellationToken);
        return profile == null ? ServiceResult<TechnicianApplicationVm>.NotFound("No technician application has been submitted.")
            : ServiceResult<TechnicianApplicationVm>.Success(ToVm(profile));
    }

    public async Task<ServiceResult<TechnicianApplicationSubmission>> SubmitAsync(int userId, TechnicianApplicationRequest request, CancellationToken cancellationToken = default)
    {
        var errors = new List<ValidationResult>();
        if (!Validator.TryValidateObject(request, new ValidationContext(request), errors, validateAllProperties: true))
            return ServiceResult<TechnicianApplicationSubmission>.Failure(errors[0].ErrorMessage ?? "Check the application details.");

        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var user = await context.Users.SingleOrDefaultAsync(user => user.Id == userId, cancellationToken);
        if (user == null || user.Status != UserStatus.Active || (user.Roles & UserRoles.Client) == 0)
            return ServiceResult<TechnicianApplicationSubmission>.Failure("A signed-in client account is required.", ServiceErrorType.Forbidden);

        var profile = await Profiles().SingleOrDefaultAsync(profile => profile.UserId == userId, cancellationToken);
        if (profile?.Status == VerificationStatus.Pending)
            return ServiceResult<TechnicianApplicationSubmission>.Failure("Your application is already pending.", ServiceErrorType.Conflict);

        var ids = request.CategoryIds.ToHashSet();
        if (request.CategoryId.HasValue) ids.Add(request.CategoryId.Value);
        if (ids.Count == 0)
            return ServiceResult<TechnicianApplicationSubmission>.Failure("Choose at least one service category.");
        var categories = await context.ServiceCategories.Where(c => ids.Contains(c.Id) && c.IsActive).ToListAsync(cancellationToken);
        if (categories.Count != ids.Count)
            return ServiceResult<TechnicianApplicationSubmission>.Failure("Choose active service categories.");

        if (profile?.Status == VerificationStatus.Approved)
            return ServiceResult<TechnicianApplicationSubmission>.Failure("Use Add categories to request another service.", ServiceErrorType.Conflict);

        string? uploadedPath = null;
        if (request.Document != null)
        {
            var upload = await storage.SaveAsync(userId, request.Document, cancellationToken);
            if (!upload.Succeeded)
                return ServiceResult<TechnicianApplicationSubmission>.Failure(upload.Error ?? "The document could not be uploaded.", upload.ErrorType);
            uploadedPath = upload.Data;
        }

        var created = profile == null;
        List<string> previousDocuments = [];
        try
        {
            if (profile == null)
            {
                profile = new TechnicianProfile { UserId = userId, User = user, Specialization = request.Specialization!, CreatedAt = DateTime.UtcNow };
                context.TechnicianProfiles.Add(profile);
            }
            else if (uploadedPath != null)
            {
                previousDocuments = profile.Documents.Select(d => d.Document).ToList();
                context.TechnicianDocuments.RemoveRange(profile.Documents);
                profile.Documents.Clear();
            }

            profile.Specialization = request.Specialization!;
            profile.YearsOfExperience = request.YearsOfExperience!.Value;
            profile.Bio = string.IsNullOrWhiteSpace(request.Bio) ? null : request.Bio;
            profile.Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address;
            profile.Status = VerificationStatus.Pending;
            profile.AvailabilityStatus = AvailabilityStatus.Offline;
            profile.RejectionReason = null;
            profile.ReviewedByUserId = null;
            profile.ReviewedAt = null;
            user.Roles |= UserRoles.Client;
            var replacedCategories = profile.Categories.Where(link => !ids.Contains(link.CategoryId)).ToList();
            context.TechnicianCategories.RemoveRange(replacedCategories);
            foreach (var link in replacedCategories) profile.Categories.Remove(link);
            foreach (var link in profile.Categories) { link.ReviewStatus = null; link.RejectionReason = null; }
            var retained = profile.Categories.Select(link => link.CategoryId).ToHashSet();
            foreach (var category in categories.Where(c => !retained.Contains(c.Id)))
                profile.Categories.Add(new TechnicianCategory { Technician = profile, CategoryId = category.Id, Category = category });
            if (uploadedPath != null)
                profile.Documents.Add(new TechnicianDocument { Technician = profile, Document = uploadedPath, UploadedAt = DateTime.UtcNow });

            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            // Failed database writes must never leave an uploaded private document behind.
            if (uploadedPath != null) DeleteFile(uploadedPath);
            throw;
        }

        // A rejected application's old evidence remains intact until its replacement is committed.
        foreach (var previous in previousDocuments) DeleteFile(previous);
        return ServiceResult<TechnicianApplicationSubmission>.Success(new(ToVm(profile), created));
    }

    public async Task<ServiceResult<TechnicianApplicationVm>> AddCategoriesAsync(int userId, int[] ids, CancellationToken ct)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var profile = await Profiles().SingleOrDefaultAsync(p => p.UserId == userId && p.Status == VerificationStatus.Approved &&
            p.User.Status == UserStatus.Active, ct);
        if (profile == null) return ServiceResult<TechnicianApplicationVm>.Failure("An approved technician account is required.", ServiceErrorType.Forbidden);
        var requested = ids.Distinct().ToArray();
        var categories = await context.ServiceCategories.Where(c => requested.Contains(c.Id) && c.IsActive).ToListAsync(ct);
        if (categories.Count != requested.Length) return ServiceResult<TechnicianApplicationVm>.Failure("Choose active service categories.");
        var additions = categories.Where(c => !profile.Categories.Any(link => link.CategoryId == c.Id &&
            link.ReviewStatus != VerificationStatus.Rejected)).ToList();
        if (additions.Count == 0)
            return ServiceResult<TechnicianApplicationVm>.Failure("Choose a new service category.", ServiceErrorType.Conflict);
        foreach (var category in additions)
        {
            var link = profile.Categories.SingleOrDefault(c => c.CategoryId == category.Id);
            if (link == null)
            {
                link = new TechnicianCategory { Technician = profile, Category = category };
                profile.Categories.Add(link);
            }
            link.ReviewStatus = VerificationStatus.Pending;
            link.RejectionReason = null;
        }
        await context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return ServiceResult<TechnicianApplicationVm>.Success(ToVm(profile));
    }

    public async Task<ServiceResult<StoredTechnicianDocument>> GetDocumentAsync(int documentId, int userId, bool isAdmin, CancellationToken cancellationToken = default)
    {
        var document = await context.TechnicianDocuments.AsNoTracking()
            .SingleOrDefaultAsync(document => document.Id == documentId && (isAdmin || document.TechnicianUserId == userId), cancellationToken);
        if (document == null) return ServiceResult<StoredTechnicianDocument>.NotFound("Document not found.");
        var stored = await storage.OpenAsync(document.Document, cancellationToken);
        return stored == null ? ServiceResult<StoredTechnicianDocument>.NotFound("Document not found.")
            : ServiceResult<StoredTechnicianDocument>.Success(stored);
    }

    private IQueryable<TechnicianProfile> Profiles() => context.TechnicianProfiles
        .Include(profile => profile.Categories).ThenInclude(link => link.Category)
        .Include(profile => profile.Documents);

    private void DeleteFile(string document)
    {
        try { storage.Delete(document); }
        catch (Exception exception) { logger.LogWarning(exception, "An unused technician document could not be removed."); }
    }

    private static TechnicianApplicationVm ToVm(TechnicianProfile profile) => new()
    {
        UserId = profile.UserId,
        Specialization = profile.Specialization,
        YearsOfExperience = profile.YearsOfExperience,
        Bio = profile.Bio,
        Address = profile.Address,
        Status = profile.Status,
        AvailabilityStatus = profile.AvailabilityStatus,
        RejectionReason = profile.RejectionReason,
        ReviewedAt = profile.ReviewedAt,
        Categories = profile.Categories.OrderBy(link => link.Category.Name).Select(link => new ApplicationCategoryVm { Id = link.CategoryId, Name = link.Category.Name, Status = link.ReviewStatus ?? profile.Status, RejectionReason = link.RejectionReason }).ToList(),
        Documents = profile.Documents.OrderBy(document => document.Id).Select(document => new DocumentVm
        {
            Id = document.Id,
            FileName = $"Qualification{Path.GetExtension(document.Document)}",
            DownloadUrl = $"/api/technician-application/documents/{document.Id}",
            UploadedAt = document.UploadedAt
        }).ToList()
    };
}
