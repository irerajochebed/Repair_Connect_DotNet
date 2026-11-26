using RepairConnect.Models.Enums;

namespace RepairConnect.Models.ViewModels;

public sealed class TechnicianApplicationVm
{
    public int UserId { get; set; }
    public required string Specialization { get; set; }
    public int YearsOfExperience { get; set; }
    public string? Bio { get; set; }
    public string? Address { get; set; }
    public VerificationStatus VerificationStatus { get; set; }
    public AvailabilityStatus AvailabilityStatus { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public List<ApplicationCategoryVm> Categories { get; set; } = [];
    public List<DocumentVm> Documents { get; set; } = [];
}

public sealed class ApplicationCategoryVm
{
    public int Id { get; set; }
    public required string Name { get; set; }
}
