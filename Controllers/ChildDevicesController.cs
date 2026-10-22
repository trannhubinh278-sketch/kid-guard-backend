using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NhuBinh_2921.Models;
using System.Collections.Concurrent;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace NhuBinh_2921.Controllers;

public sealed class LogoutRequestRecord
{
    public string RequestId { get; set; } = string.Empty;
    public string ChildUserId { get; set; } = string.Empty;
    public string ChildName { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public string ParentUserId { get; set; } = string.Empty;
    public DateTime RequestedAtUtc { get; set; } = DateTime.UtcNow;
    public bool? IsApproved { get; set; }
}

[Route("api/[controller]")]
[ApiController]
[Authorize(Roles = "Admin,Parent,Child,User")]
public class ChildDevicesController : ControllerBase
{
    private static readonly ConcurrentDictionary<string, LogoutRequestRecord> LogoutRequests = new();
    private readonly ApplicationDbContext _context;
    private readonly UserManager<User> _userManager;

    public ChildDevicesController(ApplicationDbContext context, UserManager<User> userManager)
    {
        _context = context;
        _userManager = userManager;
    }
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
    private bool IsAdmin => User.IsInRole("Admin");
    private bool IsChild => User.IsInRole("Child") || User.IsInRole("User");
    private IQueryable<ChildDevice> VisibleDevices() => IsAdmin
        ? _context.ChildDevices
        : IsChild
            ? _context.ChildDevices.Where(d => d.ChildUserId == UserId)
            : _context.ChildDevices.Where(d => d.ParentUserId == UserId);

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ChildDevice>>> GetAll()
    {
        var devices = await VisibleDevices().AsNoTracking().OrderBy(d => d.ChildName).ToListAsync();
        foreach (var device in devices) ApplyOnlineState(device);
        return Ok(devices);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ChildDevice>> GetById(int id)
    {
        var device = await VisibleDevices().AsNoTracking().FirstOrDefaultAsync(d => d.Id == id);
        if (device == null) return NotFound();
        ApplyOnlineState(device);
        return Ok(device);
    }

    [Authorize(Roles = "Admin,Parent")]
    [HttpPost]
    public async Task<ActionResult<ChildDevice>> Create(ChildDevice device)
    {
        device.Id = 0; device.ParentUserId = UserId; device.PairingCode = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        _context.ChildDevices.Add(device); await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = device.Id }, device);
    }

    [Authorize(Roles = "Admin,Parent")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, ChildDevice input)
    {
        var device = await VisibleDevices().FirstOrDefaultAsync(d => d.Id == id);
        if (device == null) return NotFound();
        device.ChildName = input.ChildName.Trim(); device.DeviceName = input.DeviceName.Trim(); device.SafeZoneName = input.SafeZoneName.Trim();
        device.SafeZoneLatitude = input.SafeZoneLatitude; device.SafeZoneLongitude = input.SafeZoneLongitude; device.SafeZoneRadiusMeters = input.SafeZoneRadiusMeters;
        await _context.SaveChangesAsync(); return NoContent();
    }

    [Authorize(Roles = "Admin,Parent")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var device = await VisibleDevices().FirstOrDefaultAsync(d => d.Id == id);
        if (device == null) return NotFound();
        _context.ChildDevices.Remove(device); await _context.SaveChangesAsync(); return NoContent();
    }

    [HttpPost("{id:int}/location")]
    public async Task<ActionResult<ChildDevice>> UpdateLocation(int id, LocationUpdateModel input)
    {
        var device = await VisibleDevices().FirstOrDefaultAsync(d => d.Id == id);
        if (device == null) return NotFound();
        device.Latitude = input.Latitude; device.Longitude = input.Longitude; device.BatteryPercent = input.BatteryPercent;
        device.MotionLevel = input.MotionLevel; device.IsOnline = true; device.LastLocationAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync(); return Ok(device);
    }

    [Authorize(Roles = "Child,User")]
    [HttpPost("{id:int}/telemetry-credentials")]
    public async Task<IActionResult> GetTelemetryCredentials(int id)
    {
        var device = await _context.ChildDevices.FirstOrDefaultAsync(d => d.Id == id && d.ChildUserId == UserId);
        if (device == null) return NotFound();
        if (string.IsNullOrWhiteSpace(device.TelemetryToken))
        {
            device.TelemetryToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
            await _context.SaveChangesAsync();
        }
        return Ok(new { deviceId = device.Id, telemetryToken = device.TelemetryToken });
    }

    [AllowAnonymous]
    [HttpPost("{id:int}/telemetry")]
    public async Task<ActionResult<ChildDevice>> UpdateTelemetry(
        int id,
        TelemetryUpdateModel input,
        [FromHeader(Name = "X-Device-Token")] string? token)
    {
        var device = await _context.ChildDevices.FirstOrDefaultAsync(d => d.Id == id);
        if (device == null) return NotFound();

        var isAuthorized = (!string.IsNullOrWhiteSpace(token) && SecureEquals(device.TelemetryToken, token))
            || (!string.IsNullOrWhiteSpace(UserId) && (device.ChildUserId == UserId || device.ParentUserId == UserId || IsAdmin));

        if (!isAuthorized)
            return Unauthorized(new { message = "Khóa telemetry không hợp lệ." });

        device.Latitude = input.Latitude;
        device.Longitude = input.Longitude;
        device.BatteryPercent = input.BatteryPercent;
        device.MotionLevel = input.MotionLevel;
        device.NoiseLevelDb = input.NoiseLevelDb;
        device.IsOnline = true;
        device.LastLocationAtUtc = DateTime.UtcNow;
        device.LastTelemetryAtUtc = DateTime.UtcNow;

        if (device.SafeZoneLatitude != 0 && device.SafeZoneLongitude != 0 && input.Latitude != 0 && input.Longitude != 0)
        {
            var distance = DistanceMeters(device.SafeZoneLatitude, device.SafeZoneLongitude, input.Latitude, input.Longitude);
            var isInside = distance <= device.SafeZoneRadiusMeters;
            if (device.IsInsideSafeZone && !isInside)
            {
                device.IsInsideSafeZone = false;
                _context.SafetyAlerts.Add(new SafetyAlert
                {
                    ChildDeviceId = device.Id,
                    Type = "SafeZone",
                    Message = $"{device.ChildName} đã rời khỏi vùng an toàn {device.SafeZoneName} ({distance:F0}m).",
                    Latitude = input.Latitude,
                    Longitude = input.Longitude,
                    CreatedAtUtc = DateTime.UtcNow
                });
            }
            else if (!device.IsInsideSafeZone && isInside)
            {
                device.IsInsideSafeZone = true;
            }
        }

        if (input.NoiseLevelDb >= 85)
        {
            var recentAlert = await _context.SafetyAlerts
                .AnyAsync(a => a.ChildDeviceId == device.Id && a.Type == "Noise" && a.CreatedAtUtc >= DateTime.UtcNow.AddMinutes(-5));
            if (!recentAlert)
            {
                _context.SafetyAlerts.Add(new SafetyAlert
                {
                    ChildDeviceId = device.Id,
                    Type = "Noise",
                    Message = $"Phát hiện mức tiếng ồn cao ({input.NoiseLevelDb:F1} dB) tại vị trí của {device.ChildName}.",
                    Latitude = input.Latitude,
                    Longitude = input.Longitude,
                    CreatedAtUtc = DateTime.UtcNow
                });
            }
        }

        await _context.SaveChangesAsync();
        return Ok(device);
    }

    [Authorize(Roles = "Child,User")]
    [HttpPost("request-logout")]
    public async Task<IActionResult> RequestLogout()
    {
        var device = await _context.ChildDevices.FirstOrDefaultAsync(d => d.ChildUserId == UserId);
        if (device == null || string.IsNullOrWhiteSpace(device.ParentUserId))
            return Ok(new { canLogout = true, message = "Thiết bị chưa được ghép với phụ huynh." });

        var requestId = Guid.NewGuid().ToString("N");
        var req = new LogoutRequestRecord
        {
            RequestId = requestId,
            ChildUserId = UserId,
            ChildName = device.ChildName,
            DeviceName = device.DeviceName,
            ParentUserId = device.ParentUserId,
            RequestedAtUtc = DateTime.UtcNow
        };
        LogoutRequests[requestId] = req;

        _context.SafetyAlerts.Add(new SafetyAlert
        {
            ChildDeviceId = device.Id,
            Type = "LogoutRequest",
            Message = $"{device.ChildName} đang yêu cầu đăng xuất khỏi KidGuard trên {device.DeviceName}.",
            CreatedAtUtc = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        return Ok(new { canLogout = false, requiresApproval = true, requestId });
    }

    [Authorize(Roles = "Child,User")]
    [HttpGet("logout-request-status/{requestId}")]
    public IActionResult GetLogoutStatus(string requestId)
    {
        if (!LogoutRequests.TryGetValue(requestId, out var req))
            return NotFound(new { message = "Yêu cầu không tồn tại." });

        return Ok(new
        {
            requestId,
            isApproved = req.IsApproved == true,
            isRejected = req.IsApproved == false,
            isPending = req.IsApproved == null
        });
    }

    [Authorize(Roles = "Admin,Parent")]
    [HttpGet("pending-logout-requests")]
    public IActionResult GetPendingLogoutRequests()
    {
        var list = LogoutRequests.Values
            .Where(r => (IsAdmin || r.ParentUserId == UserId) && r.IsApproved == null && r.RequestedAtUtc >= DateTime.UtcNow.AddMinutes(-10))
            .OrderByDescending(r => r.RequestedAtUtc)
            .ToList();
        return Ok(list);
    }

    [Authorize(Roles = "Admin,Parent")]
    [HttpPost("respond-logout")]
    public IActionResult RespondLogout(LogoutResponseModel model)
    {
        if (!LogoutRequests.TryGetValue(model.RequestId, out var req))
            return NotFound(new { message = "Yêu cầu không tồn tại hoặc đã hết hạn." });

        if (!IsAdmin && req.ParentUserId != UserId)
            return Forbid();

        req.IsApproved = model.Approve;
        return Ok(new { status = true, message = model.Approve ? "Đã đồng ý cho trẻ đăng xuất." : "Đã từ chối yêu cầu đăng xuất." });
    }

    [Authorize(Roles = "Child,User")]
    [HttpPost("verify-parent-pin")]
    public async Task<IActionResult> VerifyParentPin([FromBody] VerifyParentPasswordModel model)
    {
        var device = await _context.ChildDevices.FirstOrDefaultAsync(d => d.ChildUserId == UserId);
        if (device == null || string.IsNullOrWhiteSpace(device.ParentUserId))
            return Ok(new { valid = true });

        var parent = await _userManager.FindByIdAsync(device.ParentUserId);
        if (parent == null)
            return NotFound(new { message = "Không tìm thấy tài khoản phụ huynh." });

        var valid = await _userManager.CheckPasswordAsync(parent, model.Password);
        if (!valid)
            return BadRequest(new { valid = false, message = "Mật khẩu phụ huynh không chính xác." });

        return Ok(new { valid = true, message = "Xác nhận phụ huynh thành công." });
    }

    private static bool SecureEquals(string expected, string? actual)
    {
        if (actual == null) return false;
        var left = Encoding.UTF8.GetBytes(expected);
        var right = Encoding.UTF8.GetBytes(actual);
        return left.Length == right.Length && CryptographicOperations.FixedTimeEquals(left, right);
    }

    private static void ApplyOnlineState(ChildDevice device) =>
        device.IsOnline = device.LastTelemetryAtUtc >= DateTime.UtcNow.AddMinutes(-2);

    private static double DistanceMeters(double lat1, double lon1, double lat2, double lon2)
    {
        const double r = 6371000; double Rad(double v) => v * Math.PI / 180;
        var dLat = Rad(lat2 - lat1); var dLon = Rad(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) + Math.Cos(Rad(lat1)) * Math.Cos(Rad(lat2)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return r * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }
}
