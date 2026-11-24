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
        if (profile != null && profile.VerificationStatus != VerificationStatus.Rejected)
            return ServiceResult<TechnicianApplicationSubmission>.Failure("Your existing application is pending or has already been approved.", ServiceErrorType.Conflict);

        var category = await context.ServiceCategories.SingleOrDefaultAsync(category => category.Id == request.CategoryId && category.IsActive, cancellationToken);
        if (category == null) return ServiceResult<TechnicianApplicationSubmission>.Failure("Choose an active service category.");

        var savedDocument = await storage.SaveAsync(userId, request.Document!, cancellationToken);
        if (!savedDocument.Succeeded)
            return ServiceResult<TechnicianApplicationSubmission>.Failure(savedDocument.Error ?? "The document could not be uploaded.", savedDocument.ErrorType);

        var created = profile == null;
        var previousDocuments = profile?.Documents.Select(document => document.Document).ToList() ?? [];
        try
        {
            if (profile == null)
            {
                profile = new TechnicianProfile { UserId = userId, User = user, Specialization = request.Specialization!, CreatedAt = DateTime.UtcNow };
                context.TechnicianProfiles.Add(profile);
            }
            else
            {
                context.TechnicianDocuments.RemoveRange(profile.Documents);
                profile.Documents.Clear();
            }

            profile.Specialization = request.Specialization!;
            profile.YearsOfExperience = request.YearsOfExperience!.Value;
            profile.Bio = string.IsNullOrWhiteSpace(request.Bio) ? null : request.Bio;
            profile.Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address;
            profile.VerificationStatus = VerificationStatus.Pending;
            profile.AvailabilityStatus = AvailabilityStatus.Offline;
            profile.RejectionReason = null;
            profile.ReviewedByUserId = null;
            profile.ReviewedAt = null;
            user.Roles = (user.Roles | UserRoles.Client) & ~UserRoles.Technician;
            var replacedCategories = profile.Categories.Where(link => link.CategoryId != category.Id).ToList();
            context.TechnicianCategories.RemoveRange(replacedCategories);
            foreach (var link in replacedCategories) profile.Categories.Remove(link);
            if (!profile.Categories.Any(link => link.CategoryId == category.Id))
                profile.Categories.Add(new TechnicianCategory { Technician = profile, CategoryId = category.Id, Category = category });
            profile.Documents.Add(new TechnicianDocument { Technician = profile, Document = savedDocument.Data!, UploadedAt = DateTime.UtcNow });

            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            // Failed database writes must never leave an uploaded private document behind.
            DeleteFile(savedDocument.Data!);
            throw;
        }

        // A rejected application's old evidence remains intact until its replacement is committed.
        foreach (var previous in previousDocuments) DeleteFile(previous);
        return ServiceResult<TechnicianApplicationSubmission>.Success(new(ToVm(profile), created));
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
        VerificationStatus = profile.VerificationStatus,
        AvailabilityStatus = profile.AvailabilityStatus,
        RejectionReason = profile.RejectionReason,
        ReviewedAt = profile.ReviewedAt,
        Categories = profile.Categories.OrderBy(link => link.Category.Name).Select(link => new ApplicationCategoryVm { Id = link.CategoryId, Name = link.Category.Name }).ToList(),
        Documents = profile.Documents.OrderBy(document => document.Id).Select(document => new DocumentVm
        {
            Id = document.Id,
            FileName = $"Qualification{Path.GetExtension(document.Document)}",
            DownloadUrl = $"/api/technician-application/documents/{document.Id}",
            UploadedAt = document.UploadedAt
        }).ToList()
    };
}
