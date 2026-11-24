using System.ComponentModel.DataAnnotations;

namespace RepairConnect.Models.ViewModels;

public class RejectTechnicianRequest
{
    [Required]
    [MaxLength(2000)]
    public required string Reason { get; set; }
}
