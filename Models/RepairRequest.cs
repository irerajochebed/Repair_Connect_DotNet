using RepairConnect.Models.Enums;

namespace RepairConnect.Models;

public sealed class RepairRequest
{
    public int Id { get; set; }
    public int ClientUserId { get; set; }
    public ApplicationUser Client { get; set; } = null!;
    public int CategoryId { get; set; }
    public ServiceCategory Category { get; set; } = null!;
    public required string Title { get; set; }
    public required string Description { get; set; }
    public required string ServiceLocation { get; set; }
    public RepairUrgency Urgency { get; set; } = RepairUrgency.Medium;
    public DateOnly? PreferredDate { get; set; }
    public RepairRequestStatus Status { get; set; } = RepairRequestStatus.Open;
    public DateTime CreatedAt { get; set; }

    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}
