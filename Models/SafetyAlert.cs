using System.ComponentModel.DataAnnotations;

namespace NhuBinh_2921.Models;

public sealed class SafetyAlert
{
    public int Id { get; set; }
    public int ChildDeviceId { get; set; }
    [Required, MaxLength(30)] public string Type { get; set; } = "SOS";
    [Required, MaxLength(300)] public string Message { get; set; } = string.Empty;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public bool IsRead { get; set; }
}

public sealed class PairDeviceModel
{
    [Required] public string PairingCode { get; set; } = string.Empty;
}

public sealed class SosModel
{
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    [MaxLength(300)] public string? Message { get; set; }
}

public sealed class ConfirmChildPairingModel
{
    [Required] public string Token { get; set; } = string.Empty;
    [Range(1, int.MaxValue)] public int DeviceId { get; set; }
}

public sealed class CreateAndPairModel
{
    [Required] public string Token { get; set; } = string.Empty;
    [Required] [MaxLength(100)] public string ChildName { get; set; } = string.Empty;
    [Required] [MaxLength(100)] public string DeviceName { get; set; } = string.Empty;
    [MaxLength(100)] public string? SafeZoneName { get; set; }
    public double? SafeZoneLatitude { get; set; }
    public double? SafeZoneLongitude { get; set; }
    public double SafeZoneRadiusMeters { get; set; } = 500;
}

public sealed class TelemetryUpdateModel
{
    [Range(-90, 90)] public double Latitude { get; set; }
    [Range(-180, 180)] public double Longitude { get; set; }
    [Range(0, 100)] public int BatteryPercent { get; set; }
    [Range(0, 500)] public double MotionLevel { get; set; }
    [Range(0, 160)] public double NoiseLevelDb { get; set; }
}

public sealed class CreateChildAccountModel
{
    [Required] public string Username { get; set; } = string.Empty;
    [Required] public string Password { get; set; } = string.Empty;
    public string? ChildName { get; set; }
    public string? Email { get; set; }
}

public sealed class LogoutResponseModel
{
    [Required] public string RequestId { get; set; } = string.Empty;
    public bool Approve { get; set; }
}

public sealed class VerifyParentPasswordModel
{
    [Required] public string Password { get; set; } = string.Empty;
}
