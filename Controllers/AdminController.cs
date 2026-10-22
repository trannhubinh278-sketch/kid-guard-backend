using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NhuBinh_2921.Models;
using NhuBinh_2921.Services;
using System.Security.Claims;

namespace NhuBinh_2921.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize(Roles = "Admin")]
public sealed class AdminController : ControllerBase
{
    private static readonly string[] AllowedRoles = ["Admin", "Parent", "Child", "User"];
    private readonly UserManager<User> _userManager;
    private readonly ApplicationDbContext _context;

    public AdminController(UserManager<User> userManager, ApplicationDbContext context)
    {
        _userManager = userManager;
        _context = context;
    }

    [HttpGet("summary")]
    public async Task<IActionResult> Summary()
    {
        var users = await _userManager.Users.AsNoTracking().ToListAsync();
        var roleCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var user in users)
        {
            foreach (var role in await _userManager.GetRolesAsync(user))
                roleCounts[role] = roleCounts.GetValueOrDefault(role) + 1;
        }

        var devices = await _context.ChildDevices.AsNoTracking().ToListAsync();
        return Ok(new
        {
            totalUsers = users.Count,
            admins = roleCounts.GetValueOrDefault("Admin"),
            parents = roleCounts.GetValueOrDefault("Parent"),
            childUsers = roleCounts.GetValueOrDefault("Child") + roleCounts.GetValueOrDefault("User"),
            totalDevices = devices.Count,
            onlineDevices = devices.Count(device => device.LastTelemetryAtUtc >= DateTime.UtcNow.AddMinutes(-15)),
            offlineDevices = devices.Count(device => device.LastTelemetryAtUtc < DateTime.UtcNow.AddMinutes(-15) || device.LastTelemetryAtUtc == null),
            outsideSafeZone = 0,
            lowBattery = devices.Count(device => device.BatteryPercent < 20)
        });
    }

    [HttpGet("users")]
    public async Task<IActionResult> Users()
    {
        var users = await _userManager.Users.AsNoTracking().OrderBy(user => user.UserName).ToListAsync();
        var result = new List<object>();
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            result.Add(new
            {
                user.Id,
                username = user.UserName,
                user.Email,
                user.PhoneNumber,
                user.Initials,
                role = roles.FirstOrDefault() ?? "User",
                isActive = !(await _userManager.IsLockedOutAsync(user)),
                deviceCount = await _context.ChildDevices.CountAsync(device => device.ParentUserId == user.Id)
            });
        }
        return Ok(result);
    }

    [HttpPost("users")]
    public async Task<IActionResult> CreateUser(AdminCreateUserModel model)
    {
        var role = NormalizeRole(model.Role);
        if (role == null) return BadRequest(new { message = "Vai trò không hợp lệ." });
        if (await _userManager.FindByNameAsync(model.Username.Trim()) != null)
            return BadRequest(new { message = "Tên đăng nhập đã tồn tại." });
        if (await _userManager.FindByEmailAsync(model.Email.Trim()) != null)
            return BadRequest(new { message = "Email đã tồn tại." });

        var phone = string.IsNullOrWhiteSpace(model.PhoneNumber)
            ? null : SmsGatewayService.NormalizePhone(model.PhoneNumber);
        if (phone != null && await _userManager.Users.AnyAsync(user => user.PhoneNumber == phone))
            return BadRequest(new { message = "Số điện thoại đã tồn tại." });

        var user = new User
        {
            UserName = model.Username.Trim(),
            Email = model.Email.Trim(),
            PhoneNumber = phone,
            Initials = model.Initials?.Trim(),
            LockoutEnabled = true
        };
        var created = await _userManager.CreateAsync(user, model.Password);
        if (!created.Succeeded)
            return BadRequest(new { message = created.Errors.First().Description });
        await _userManager.AddToRoleAsync(user, role);
        return Ok(new { status = true, message = "Đã tạo tài khoản." });
    }

    [HttpPut("users/{id}")]
    public async Task<IActionResult> UpdateUser(string id, AdminUpdateUserModel model)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound(new { message = "Không tìm thấy tài khoản." });
        var role = NormalizeRole(model.Role);
        if (role == null) return BadRequest(new { message = "Vai trò không hợp lệ." });

        var emailOwner = await _userManager.FindByEmailAsync(model.Email.Trim());
        if (emailOwner != null && emailOwner.Id != id)
            return BadRequest(new { message = "Email đã tồn tại." });
        var phone = string.IsNullOrWhiteSpace(model.PhoneNumber)
            ? null : SmsGatewayService.NormalizePhone(model.PhoneNumber);
        if (phone != null && await _userManager.Users.AnyAsync(item => item.Id != id && item.PhoneNumber == phone))
            return BadRequest(new { message = "Số điện thoại đã tồn tại." });

        user.Email = model.Email.Trim();
        user.PhoneNumber = phone;
        user.Initials = model.Initials?.Trim();
        var updated = await _userManager.UpdateAsync(user);
        if (!updated.Succeeded) return BadRequest(new { message = updated.Errors.First().Description });
        var currentRoles = await _userManager.GetRolesAsync(user);
        if (!currentRoles.Contains(role))
        {
            if (currentRoles.Count > 0) await _userManager.RemoveFromRolesAsync(user, currentRoles);
            await _userManager.AddToRoleAsync(user, role);
        }
        return Ok(new { status = true, message = "Đã cập nhật tài khoản." });
    }

    [HttpPatch("users/{id}/status")]
    public async Task<IActionResult> SetStatus(string id, AdminUserStatusModel model)
    {
        if (id == User.FindFirstValue(ClaimTypes.NameIdentifier))
            return BadRequest(new { message = "Không thể tự khóa tài khoản đang đăng nhập." });
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound(new { message = "Không tìm thấy tài khoản." });
        user.LockoutEnabled = true;
        user.LockoutEnd = model.IsActive ? null : DateTimeOffset.MaxValue;
        await _userManager.UpdateAsync(user);
        return Ok(new { status = true, message = model.IsActive ? "Đã mở khóa tài khoản." : "Đã khóa tài khoản." });
    }

    [HttpPost("users/{id}/reset-password")]
    public async Task<IActionResult> ResetPassword(string id, AdminResetPasswordModel model)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound(new { message = "Không tìm thấy tài khoản." });
        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, model.NewPassword);
        if (!result.Succeeded) return BadRequest(new { message = result.Errors.First().Description });
        return Ok(new { status = true, message = "Đã đặt lại mật khẩu." });
    }

    [HttpDelete("users/{id}")]
    public async Task<IActionResult> DeleteUser(string id)
    {
        if (id == User.FindFirstValue(ClaimTypes.NameIdentifier))
            return BadRequest(new { message = "Không thể tự xóa tài khoản đang đăng nhập." });
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound(new { message = "Không tìm thấy tài khoản." });
        if (await _context.ChildDevices.AnyAsync(device => device.ParentUserId == id))
            return BadRequest(new { message = "Hãy xóa thiết bị của tài khoản trước." });
        await _userManager.DeleteAsync(user);
        return NoContent();
    }

    private static string? NormalizeRole(string role) => AllowedRoles
        .FirstOrDefault(item => item.Equals(role.Trim(), StringComparison.OrdinalIgnoreCase));
}
