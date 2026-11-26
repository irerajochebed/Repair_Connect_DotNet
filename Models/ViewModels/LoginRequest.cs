using System.ComponentModel.DataAnnotations;

namespace RepairConnect.Models.ViewModels;

public class LoginRequest
{
    [Required]
    [EmailAddress]
    [MaxLength(256)]
    public required string Email { get; set; }

    [Required]
    public required string Password { get; set; }

    public bool RememberMe { get; set; }
}
