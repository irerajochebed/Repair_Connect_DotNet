namespace RepairConnect.Models;

public sealed class RepairRequest
{
    public int Id { get; set; }
    public int ClientUserId { get; set; }
    public User Client { get; set; } = null!;
    public int CategoryId { get; set; }
    public ServiceCategory Category { get; set; } = null!;
    public required string Title { get; set; }
    public required string Description { get; set; }
    public required string ServiceLocation { get; set; }
    public RepairUrgency Urgency { get; set; } = RepairUrgency.Medium;
    public DateOnly? PreferredDate { get; set; }
    public RepairRequestStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
}

public enum RepairUrgency
{
    Low,
    Medium,
    High
}

public enum RepairRequestStatus
{
    Open,
    Booked,
    Completed,
    Cancelled
}
