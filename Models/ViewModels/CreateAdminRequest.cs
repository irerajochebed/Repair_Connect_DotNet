using System.ComponentModel.DataAnnotations;

namespace RepairConnect.Models.ViewModels;

public class CreateAdminRequest
{
    [Required]
    [MaxLength(100)]
    public required string DisplayName { get; set; }

    [Required]
    [EmailAddress]
    [MaxLength(256)]
    public required string Email { get; set; }

    [MaxLength(20)]
    public string? PhoneNumber { get; set; }

    [Required]
    [MinLength(6)]
    public required string Password { get; set; }
}
