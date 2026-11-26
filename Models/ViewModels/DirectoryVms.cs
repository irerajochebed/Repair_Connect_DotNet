using RepairConnect.Models.Enums;

namespace RepairConnect.Models.ViewModels;

public sealed record TechnicianCategoryVm(int Id, string Name);

public sealed record TechnicianDirectoryItemVm(
    int UserId,
    string DisplayName,
    string Specialization,
    int YearsOfExperience,
    string? Address,
    string? ProfilePhoto,
    AvailabilityStatus AvailabilityStatus,
    IReadOnlyList<TechnicianCategoryVm> Categories,
    double? AverageRating,
    int ReviewCount);

public sealed record TechnicianPublicProfileVm(
    int UserId,
    string DisplayName,
    string Specialization,
    int YearsOfExperience,
    string? Bio,
    string? Address,
    string? ProfilePhoto,
    AvailabilityStatus AvailabilityStatus,
    IReadOnlyList<TechnicianCategoryVm> Categories,
    double? AverageRating,
    int ReviewCount,
    int CompletedJobs,
    IReadOnlyList<PublicReviewVm> RecentReviews);

public sealed record PublicReviewVm(int Id, byte Rating, string? Comment, string ClientName, DateTime CreatedAt);
