using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NhuBinh_2921.Models;
using System.Collections.Concurrent;
using System.Security.Claims;
using System.Security.Cryptography;

namespace NhuBinh_2921.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize(Roles = "Admin,Parent,Child,User")]
public sealed class ChildPairingController : ControllerBase
{
    private sealed record PairingSession(string ChildUserId, DateTime ExpiresAtUtc);
    private static readonly ConcurrentDictionary<string, PairingSession> Sessions = new();
    private readonly ApplicationDbContext _context;
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
    private bool IsAdmin => User.IsInRole("Admin");

    public ChildPairingController(ApplicationDbContext context) => _context = context;

    [Authorize(Roles = "Child,User")]
    [HttpPost("session")]
    public async Task<IActionResult> CreateSession()
    {
        foreach (var item in Sessions.Where(item => item.Value.ChildUserId == UserId).ToList())
            Sessions.TryRemove(item.Key, out _);

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(8));
        var expiresAt = DateTime.UtcNow.AddMinutes(10);
        Sessions[token] = new PairingSession(UserId, expiresAt);
        return Ok(new
        {
            token,
            qrData = $"KIDGUARD_PAIR:{token}",
            expiresAtUtc = expiresAt,
            expiresInSeconds = 600
        });
    }

    [Authorize(Roles = "Child,User,Admin,Parent")]
    [HttpPost("unpair")]
    public async Task<IActionResult> Unpair()
    {
        var devices = await _context.ChildDevices
            .Where(d => d.ChildUserId == UserId || (User.IsInRole("Parent") && d.ParentUserId == UserId))
            .ToListAsync();
        foreach (var d in devices)
        {
            d.ChildUserId = null;
            d.PairedAtUtc = null;
        }
        await _context.SaveChangesAsync();
        return Ok(new { status = true, message = "Đã hủy ghép nối." });
    }

    [Authorize(Roles = "Child,User")]
    [HttpGet("status")]
    public async Task<IActionResult> Status()
    {
        var device = await _context.ChildDevices.AsNoTracking()
            .FirstOrDefaultAsync(item => item.ChildUserId == UserId);
        return Ok(new { paired = device != null, device });
    }

    [Authorize(Roles = "Admin,Parent")]
    [HttpPost("confirm")]
    public async Task<IActionResult> Confirm(ConfirmChildPairingModel model)
    {
        var token = model.Token.Trim().ToUpperInvariant();
        if (!Sessions.TryGetValue(token, out var session))
            return NotFound(new { message = "Mã QR không tồn tại hoặc đã được sử dụng." });
        if (session.ExpiresAtUtc < DateTime.UtcNow)
        {
            Sessions.TryRemove(token, out _);
            return BadRequest(new { message = "Mã QR đã hết hạn. Hãy tạo mã mới trên thiết bị Child." });
        }

        var device = await _context.ChildDevices.FirstOrDefaultAsync(item => item.Id == model.DeviceId);
        if (device == null) return NotFound(new { message = "Không tìm thấy thiết bị." });
        if (!IsAdmin && device.ParentUserId != UserId) return Forbid();
        if (!string.IsNullOrWhiteSpace(device.ChildUserId) && device.ChildUserId != session.ChildUserId)
            return Conflict(new { message = "Thiết bị đã được ghép với Child/User khác." });

        var oldDevices = await _context.ChildDevices
            .Where(item => item.ChildUserId == session.ChildUserId && item.Id != device.Id)
            .ToListAsync();
        foreach (var oldDev in oldDevices)
        {
            oldDev.ChildUserId = null;
            oldDev.PairedAtUtc = null;
        }

        device.ChildUserId = session.ChildUserId;
        device.PairedAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        Sessions.TryRemove(token, out _);
        return Ok(new { status = true, message = "Ghép Child/User thành công.", device });
    }

    [Authorize(Roles = "Admin,Parent")]
    [HttpPost("create-and-pair")]
    public async Task<IActionResult> CreateAndPair(CreateAndPairModel model)
    {
        var raw = model.Token.Trim();
        const string prefix = "KIDGUARD_PAIR:";
        if (raw.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            raw = raw[prefix.Length..];
        var token = raw.Replace("-", "").Trim().ToUpperInvariant();

        if (!Sessions.TryGetValue(token, out var session))
            return NotFound(new { message = "Mã ghép nối không hợp lệ hoặc đã hết hạn." });
        if (session.ExpiresAtUtc < DateTime.UtcNow)
        {
            Sessions.TryRemove(token, out _);
            return BadRequest(new { message = "Mã ghép nối đã hết hạn. Hãy tạo mã mới trên máy trẻ." });
        }

        var oldDevices = await _context.ChildDevices
            .Where(item => item.ChildUserId == session.ChildUserId)
            .ToListAsync();
        foreach (var oldDev in oldDevices)
        {
            oldDev.ChildUserId = null;
            oldDev.PairedAtUtc = null;
        }

        var device = new ChildDevice
        {
            ParentUserId = UserId,
            ChildUserId = session.ChildUserId,
            ChildName = model.ChildName.Trim(),
            DeviceName = model.DeviceName.Trim(),
            PairingCode = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(),
            SafeZoneName = string.IsNullOrWhiteSpace(model.SafeZoneName) ? "Khu vực an toàn" : model.SafeZoneName.Trim(),
            SafeZoneLatitude = model.SafeZoneLatitude ?? 10.7769,
            SafeZoneLongitude = model.SafeZoneLongitude ?? 106.7009,
            SafeZoneRadiusMeters = model.SafeZoneRadiusMeters > 0 ? model.SafeZoneRadiusMeters : 500,
            PairedAtUtc = DateTime.UtcNow,
            IsOnline = true,
            BatteryPercent = 100,
        };

        _context.ChildDevices.Add(device);
        await _context.SaveChangesAsync();
        Sessions.TryRemove(token, out _);

        return Ok(new { status = true, message = "Đã thêm thiết bị và ghép nối thành công.", device });
    }
}
