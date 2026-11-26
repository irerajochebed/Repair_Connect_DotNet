using System.ComponentModel.DataAnnotations;

namespace RepairConnect.Models.ViewModels;

public sealed class TechnicianApplicationRequest
{
    private string? _specialization;
    private string? _bio;
    private string? _address;

    [Required, MaxLength(100)]
    public string? Specialization { get => _specialization; set => _specialization = value?.Trim(); }

    [Required, Range(0, 60)]
    public int? YearsOfExperience { get; set; }

    [Required, Range(1, int.MaxValue)]
    public int? CategoryId { get; set; }

    [MaxLength(2000)]
    public string? Bio { get => _bio; set => _bio = value?.Trim(); }

    [MaxLength(300)]
    public string? Address { get => _address; set => _address = value?.Trim(); }

    [Required]
    public IFormFile? Document { get; set; }
}
