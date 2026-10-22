using System.ComponentModel.DataAnnotations;

namespace NhuBinh_2921.Models;

public class VerifyOtpModel
{
    [Required] public string ChallengeId { get; set; } = string.Empty;
    [Required, RegularExpression("^[0-9]{6}$")] public string Code { get; set; } = string.Empty;
}

public class LocationUpdateModel
{
    [Range(-90, 90)] public double Latitude { get; set; }
    [Range(-180, 180)] public double Longitude { get; set; }
    [Range(0, 100)] public int BatteryPercent { get; set; }
    [Range(0, 100)] public double MotionLevel { get; set; }
}

public sealed class ChangePasswordModel
{
    [Required] public string CurrentPassword { get; set; } = string.Empty;
    [Required, MinLength(6)] public string NewPassword { get; set; } = string.Empty;
}
