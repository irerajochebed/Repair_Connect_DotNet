namespace RepairConnect.Models;

public sealed class TechnicianProfile
{
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public string? Bio { get; set; }
    public string? ProfilePhoto { get; set; }
    public required string Specialization { get; set; }
    public int YearsOfExperience { get; set; }
    public string? Address { get; set; }
    public VerificationStatus VerificationStatus { get; set; }
    public string? RejectionReason { get; set; }
    public int? ReviewedByUserId { get; set; }
    public User? ReviewedByUser { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public AvailabilityStatus AvailabilityStatus { get; set; }
    public DateTime CreatedAt { get; set; }
}

public enum VerificationStatus
{
    Pending,
    Approved,
    Rejected
}

public enum AvailabilityStatus
{
    Available,
    Busy,
    Offline
}
