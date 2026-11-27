namespace RepairConnect.Models;

public sealed class TechnicianCategory
{
    // Null uses the original application decision. Added categories have their own review.
    public Enums.VerificationStatus? ReviewStatus { get; set; }
    public string? RejectionReason { get; set; }
    public int TechnicianUserId { get; set; }
    public TechnicianProfile Technician { get; set; } = null!;
    public int CategoryId { get; set; }
    public ServiceCategory Category { get; set; } = null!;
}
