using RepairConnect.Models.Enums;

namespace RepairConnect.Models.ViewModels;

public class TechnicianReviewVm
{
    public int UserId { get; set; }
    public required string DisplayName { get; set; }
    public required string Email { get; set; }
    public string? PhoneNumber { get; set; }
    public required string Specialization { get; set; }
    public string? Bio { get; set; }
    public int YearsOfExperience { get; set; }
    public string? Address { get; set; }
    public VerificationStatus VerificationStatus { get; set; }
    public string? RejectionReason { get; set; }
    public string? ReviewedByName { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public List<string> Categories { get; set; } = new();
    public List<DocumentVm> Documents { get; set; } = new();
}
