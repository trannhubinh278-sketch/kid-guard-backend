using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace NhuBinh_2921.Models;

public class ChildDevice
{
    public int Id { get; set; }
    [Required, MaxLength(100)] public string ChildName { get; set; } = string.Empty;
    [Required, MaxLength(100)] public string DeviceName { get; set; } = string.Empty;
    [Required, MaxLength(32)] public string PairingCode { get; set; } = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
    [Required] public string ParentUserId { get; set; } = string.Empty;
    public string? ChildUserId { get; set; }
    public DateTime? PairedAtUtc { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public DateTime? LastLocationAtUtc { get; set; }
    public int BatteryPercent { get; set; } = 100;
    public bool IsOnline { get; set; }
    public bool IsInsideSafeZone { get; set; } = true;
    public double MotionLevel { get; set; }
    public double NoiseLevelDb { get; set; }
    public DateTime? LastTelemetryAtUtc { get; set; }
    [JsonIgnore, MaxLength(64)] public string? TelemetryToken { get; set; }
    [MaxLength(200)] public string SafeZoneName { get; set; } = "Nhà";
    public double SafeZoneLatitude { get; set; } = 10.7769;
    public double SafeZoneLongitude { get; set; } = 106.7009;
    public double SafeZoneRadiusMeters { get; set; } = 500;
}
