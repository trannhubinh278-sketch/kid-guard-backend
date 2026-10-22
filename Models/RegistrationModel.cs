using System.ComponentModel.DataAnnotations;

namespace NhuBinh_2921.Models;

public class RegistrationModel
{
    [Required]
    public string Username { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, MinLength(9), MaxLength(20)]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required, MinLength(6)]
    public string Password { get; set; } = string.Empty;

    [MaxLength(5)]
    public string? Initials { get; set; }

    public string? Role { get; set; }
}
