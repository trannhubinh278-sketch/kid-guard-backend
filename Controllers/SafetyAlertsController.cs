using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NhuBinh_2921.Models;
using System.Security.Claims;

namespace NhuBinh_2921.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize(Roles = "Admin,Parent,Child,User")]
public sealed class SafetyAlertsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
    private bool IsAdmin => User.IsInRole("Admin");
    private bool IsChild => User.IsInRole("Child") || User.IsInRole("User");

    public SafetyAlertsController(ApplicationDbContext context) => _context = context;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var visibleDevices = IsAdmin
            ? _context.ChildDevices.Select(d => d.Id)
            : IsChild
                ? _context.ChildDevices.Where(d => d.ChildUserId == UserId).Select(d => d.Id)
                : _context.ChildDevices.Where(d => d.ParentUserId == UserId).Select(d => d.Id);
        var alerts = await _context.SafetyAlerts.AsNoTracking()
            .Where(alert => visibleDevices.Contains(alert.ChildDeviceId))
            .OrderByDescending(alert => alert.CreatedAtUtc)
            .Take(100)
            .ToListAsync();
        return Ok(alerts);
    }

    [Authorize(Roles = "Child,User")]
    [HttpPost("device/{deviceId:int}/sos")]
    public async Task<IActionResult> SendSos(int deviceId, SosModel model)
    {
        var device = await _context.ChildDevices.FirstOrDefaultAsync(
            item => item.Id == deviceId && item.ChildUserId == UserId);
        if (device == null) return NotFound(new { message = "Thiết bị chưa được ghép với tài khoản này." });

        if (model.Latitude.HasValue && model.Longitude.HasValue)
        {
            device.Latitude = model.Latitude;
            device.Longitude = model.Longitude;
            device.LastLocationAtUtc = DateTime.UtcNow;
            device.IsOnline = true;
        }
        var alert = new SafetyAlert
        {
            ChildDeviceId = device.Id,
            Type = "SOS",
            Message = string.IsNullOrWhiteSpace(model.Message)
                ? $"{device.ChildName} cần trợ giúp khẩn cấp."
                : model.Message.Trim(),
            Latitude = model.Latitude,
            Longitude = model.Longitude
        };
        _context.SafetyAlerts.Add(alert);
        await _context.SaveChangesAsync();
        return Ok(new { status = true, message = "Đã gửi cảnh báo SOS đến phụ huynh.", alertId = alert.Id });
    }

    [Authorize(Roles = "Admin,Parent")]
    [HttpPatch("{id:int}/read")]
    public async Task<IActionResult> MarkRead(int id)
    {
        var alert = await _context.SafetyAlerts.FindAsync(id);
        if (alert == null) return NotFound();
        if (!IsAdmin)
        {
            var ownsDevice = await _context.ChildDevices.AnyAsync(
                device => device.Id == alert.ChildDeviceId && device.ParentUserId == UserId);
            if (!ownsDevice) return Forbid();
        }
        alert.IsRead = true;
        await _context.SaveChangesAsync();
        return NoContent();
    }
}
