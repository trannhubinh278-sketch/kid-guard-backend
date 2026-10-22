using System.ComponentModel.DataAnnotations;

namespace NhuBinh_2921.Models;

public sealed class AdminCreateUserModel
{
    [Required] public string Username { get; set; } = string.Empty;
    [Required, EmailAddress] public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    [Required, MinLength(6)] public string Password { get; set; } = string.Empty;
    [Required] public string Role { get; set; } = "Parent";
    [MaxLength(5)] public string? Initials { get; set; }
}

public sealed class AdminUpdateUserModel
{
    [Required, EmailAddress] public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    [Required] public string Role { get; set; } = "Parent";
    [MaxLength(5)] public string? Initials { get; set; }
}

public sealed class AdminUserStatusModel
{
    public bool IsActive { get; set; }
}

public sealed class AdminResetPasswordModel
{
    [Required, MinLength(6)] public string NewPassword { get; set; } = string.Empty;
}
