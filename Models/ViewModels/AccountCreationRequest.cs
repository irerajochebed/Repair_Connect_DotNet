using System.ComponentModel.DataAnnotations;

namespace RepairConnect.Models.ViewModels;

public abstract class AccountCreationRequest
{
    private string? _displayName;
    private string? _email;
    private string? _phoneNumber;

    [Required]
    [MaxLength(100)]
    public string? DisplayName { get => _displayName; set => _displayName = value?.Trim(); }

    [Required]
    [EmailAddress]
    [MaxLength(256)]
    public string? Email { get => _email; set => _email = value?.Trim(); }

    [MaxLength(20)]
    public string? PhoneNumber { get => _phoneNumber; set => _phoneNumber = value?.Trim(); }

    [Required]
    [MinLength(8)]
    [MaxLength(128)]
    public string? Password { get; set; }

}
