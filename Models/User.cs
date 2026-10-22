using Microsoft.AspNetCore.Identity;

namespace NhuBinh_2921.Models;

public class User : IdentityUser
{
    public string? Initials { get; set; }
}
