using System.ComponentModel.DataAnnotations;
using RepairConnect.Models.Enums;

namespace RepairConnect.Models.ViewModels;

public sealed class CreateRepairRequest
{
    [Required]
    public int? CategoryId { get; set; }

    [Required]
    public int? TechnicianUserId { get; set; }

    [Required, MaxLength(150)]
    public string? Title { get; set; }

    [Required, MaxLength(4000)]
    public string? Description { get; set; }

    [Required, MaxLength(300)]
    public string? ServiceLocation { get; set; }

    public RepairUrgency Urgency { get; set; } = RepairUrgency.Medium;

    public DateOnly? PreferredDate { get; set; }
}

public sealed class ChooseTechnicianRequest
{
    [Required]
    public int? TechnicianUserId { get; set; }
}

public sealed class CreateReviewRequest
{
    [Required, Range(1, 5)]
    public int? Rating { get; set; }

    [MaxLength(1000)]
    public string? Comment { get; set; }
}

public sealed record BookingVm(
    int Id,
    int TechnicianUserId,
    string TechnicianName,
    BookingStatus Status,
    DateTime? ScheduledDate,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    bool HasReview);

public sealed record RepairRequestVm(
    int Id,
    string Title,
    string Description,
    string ServiceLocation,
    int CategoryId,
    string CategoryName,
    RepairUrgency Urgency,
    DateOnly? PreferredDate,
    RepairRequestStatus Status,
    DateTime CreatedAt,
    IReadOnlyList<BookingVm> Bookings);
