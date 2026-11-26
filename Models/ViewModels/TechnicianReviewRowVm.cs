using RepairConnect.Models.Enums;

namespace RepairConnect.Models.ViewModels;

public class TechnicianReviewRowVm
{
    public int UserId { get; set; }
    public required string DisplayName { get; set; }
    public required string Specialization { get; set; }
    public int YearsOfExperience { get; set; }
    public VerificationStatus VerificationStatus { get; set; }
    public int DocumentCount { get; set; }
    public DateTime CreatedAt { get; set; }
}
