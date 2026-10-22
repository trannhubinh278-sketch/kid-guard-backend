using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using NhuBinh_2921.Models;
using NhuBinh_2921.Services;
using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace NhuBinh_2921.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AuthenticateController : ControllerBase
{
    private const string EmailVerificationPendingClaim = "email_verification_pending";
    private sealed record OtpChallenge(
        string UserId, string Code, DateTime ExpiresAtUtc, string Purpose, int FailedAttempts = 0);
    private static readonly ConcurrentDictionary<string, OtpChallenge> OtpChallenges = new();
    private readonly UserManager<User> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;
    private readonly SmsGatewayService _smsGateway;
    private readonly EmailOtpService _emailOtp;

    public AuthenticateController(UserManager<User> userManager, RoleManager<IdentityRole> roleManager,
        IConfiguration configuration, IWebHostEnvironment environment, SmsGatewayService smsGateway,
        EmailOtpService emailOtp)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _configuration = configuration;
        _environment = environment;
        _smsGateway = smsGateway;
        _emailOtp = emailOtp;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegistrationModel model)
    {
        string? role = "Parent";
        if (role == null) return BadRequest(new { status = false, message = "Role chỉ được là Admin, Parent hoặc Child." });
        if (await _userManager.FindByNameAsync(model.Username) != null)
            return BadRequest(new { status = false, message = "Tên đăng nhập đã tồn tại." });
        if (await _userManager.FindByEmailAsync(model.Email) != null)
            return BadRequest(new { status = false, message = "Email đã tồn tại." });

        var normalizedPhone = SmsGatewayService.NormalizePhone(model.PhoneNumber);
        if (await _userManager.Users.AnyAsync(user => user.PhoneNumber == normalizedPhone))
            return BadRequest(new { status = false, message = "Số điện thoại đã được đăng ký." });

        var user = new User
        {
            UserName = model.Username.Trim(),
            Email = model.Email.Trim(),
            PhoneNumber = normalizedPhone,
            Initials = model.Initials?.Trim()
        };
        var result = await _userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
            return BadRequest(new { status = false, message = "Tạo tài khoản thất bại.", errors = result.Errors.Select(e => e.Description) });
        if (!await _roleManager.RoleExistsAsync(role)) await _roleManager.CreateAsync(new IdentityRole(role));
        await _userManager.AddToRoleAsync(user, role);

        var challengeId = Guid.NewGuid().ToString("N");
        var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        await _userManager.AddClaimAsync(user, new Claim(EmailVerificationPendingClaim, "true"));
        OtpChallenges[challengeId] = new OtpChallenge(
            user.Id, code, DateTime.UtcNow.AddMinutes(5), "registration");
        var dispatch = await _emailOtp.SendRegistrationOtpAsync(user.Email, code, HttpContext.RequestAborted);
        var devOtp = !dispatch.Sent ? code : null;

        return Ok(new
        {
            status = true,
            message = dispatch.Message,
            requiresOtp = true,
            challengeId,
            expiresInSeconds = 300,
            delivery = dispatch.Sent ? "email" : "development",
            maskedEmail = EmailOtpService.MaskEmail(user.Email),
            devOtp,
            username = user.UserName
        });
    }

    [Authorize(Roles = "Admin,Parent")]
    [HttpPost("create-child-account")]
    public async Task<IActionResult> CreateChildAccount([FromBody] CreateChildAccountModel model)
    {
        var trimmedUser = model.Username.Trim();
        if (await _userManager.FindByNameAsync(trimmedUser) != null)
            return BadRequest(new { status = false, message = "Tên đăng nhập đã tồn tại." });

        var email = string.IsNullOrWhiteSpace(model.Email)
            ? $"{trimmedUser.ToLowerInvariant()}@kidguard.local"
            : model.Email.Trim();

        if (await _userManager.FindByEmailAsync(email) != null)
            return BadRequest(new { status = false, message = "Email đã tồn tại." });

        var user = new User
        {
            UserName = trimmedUser,
            Email = email,
            Initials = string.IsNullOrWhiteSpace(model.ChildName) ? "CH" : (model.ChildName.Trim().Length > 5 ? model.ChildName.Trim()[..5] : model.ChildName.Trim())
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
            return BadRequest(new { status = false, message = "Tạo tài khoản thất bại.", errors = result.Errors.Select(e => e.Description) });

        if (!await _roleManager.RoleExistsAsync("Child"))
            await _roleManager.CreateAsync(new IdentityRole("Child"));

        await _userManager.AddToRoleAsync(user, "Child");
        return Ok(new
        {
            status = true,
            message = "Tạo tài khoản Trẻ em thành công.",
            username = user.UserName,
            childName = model.ChildName
        });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginModel model, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByNameAsync(model.Username);
        if (user == null || !await _userManager.CheckPasswordAsync(user, model.Password))
            return Unauthorized(new { status = false, message = "Sai tên đăng nhập hoặc mật khẩu." });

        if (await _userManager.IsLockedOutAsync(user))
            return Unauthorized(new { status = false, message = "Tài khoản đã bị khóa. Vui lòng liên hệ quản trị viên." });

        var userClaims = await _userManager.GetClaimsAsync(user);
        if (userClaims.Any(claim => claim.Type == EmailVerificationPendingClaim))
            return Unauthorized(new { status = false, message = "Vui lòng xác thực email đăng ký trước khi đăng nhập." });

        var challengeId = Guid.NewGuid().ToString("N");
        var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        OtpChallenges[challengeId] = new OtpChallenge(
            user.Id, code, DateTime.UtcNow.AddMinutes(5), "login");
        var dispatch = await _smsGateway.SendOtpAsync(user.PhoneNumber, code, cancellationToken);
        var maskedPhone = MaskPhone(user.PhoneNumber);
        var isChild = await _userManager.IsInRoleAsync(user, "Child");
        var devOtp = (!dispatch.Sent && (isChild || string.IsNullOrWhiteSpace(user.PhoneNumber))) ? code : null;
        return Ok(new
        {
            status = true,
            message = dispatch.Message,
            requiresOtp = true,
            challengeId,
            expiresInSeconds = 300,
            delivery = dispatch.Sent ? "sms" : "development",
            maskedPhone,
            devOtp
        });
    }

    [HttpPost("verify-otp")]
    public async Task<IActionResult> VerifyOtp(VerifyOtpModel model)
    {
        if (!OtpChallenges.TryGetValue(model.ChallengeId, out var challenge))
            return Unauthorized(new { status = false, message = "Yêu cầu OTP không tồn tại." });
        if (challenge.ExpiresAtUtc < DateTime.UtcNow)
        {
            OtpChallenges.TryRemove(model.ChallengeId, out _);
            return Unauthorized(new { status = false, message = "OTP đã hết hạn." });
        }
        if (!CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(challenge.Code), Encoding.UTF8.GetBytes(model.Code)))
        {
            var attempts = challenge.FailedAttempts + 1;
            if (attempts >= 5) OtpChallenges.TryRemove(model.ChallengeId, out _);
            else OtpChallenges[model.ChallengeId] = challenge with { FailedAttempts = attempts };
            return Unauthorized(new { status = false, message = attempts >= 5
                ? "OTP sai quá 5 lần. Vui lòng đăng nhập lại."
                : $"OTP không đúng. Còn {5 - attempts} lần thử." });
        }

        OtpChallenges.TryRemove(model.ChallengeId, out _);
        var user = await _userManager.FindByIdAsync(challenge.UserId);
        if (user == null) return Unauthorized(new { status = false, message = "Tài khoản không tồn tại." });
        if (challenge.Purpose == "registration")
        {
            user.EmailConfirmed = true;
            var pendingClaims = (await _userManager.GetClaimsAsync(user))
                .Where(claim => claim.Type == EmailVerificationPendingClaim)
                .ToList();
            foreach (var claim in pendingClaims)
                await _userManager.RemoveClaimAsync(user, claim);
            await _userManager.UpdateAsync(user);
        }
        var roles = await _userManager.GetRolesAsync(user);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.UserName ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
        var successMessage = challenge.Purpose == "registration"
            ? "Đăng ký và xác thực email thành công."
            : "Đăng nhập thành công.";
        return Ok(new { status = true, message = successMessage, token = GenerateToken(claims), roles,
            user = new { user.UserName, user.Email, user.PhoneNumber, user.Initials } });
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordModel model)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = userId == null ? null : await _userManager.FindByIdAsync(userId);
        if (user == null) return Unauthorized(new { message = "Tài khoản không tồn tại." });
        var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
        if (!result.Succeeded)
            return BadRequest(new { message = result.Errors.First().Description });
        return Ok(new { status = true, message = "Đổi mật khẩu thành công." });
    }

    private static string? MaskPhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone) || phone.Length < 6) return null;
        return $"{phone[..3]}****{phone[^3..]}";
    }

    private string GenerateToken(IEnumerable<Claim> claims)
    {
        var settings = _configuration.GetSection("JWTKey");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
            settings["Secret"] ?? throw new InvalidOperationException("Thiếu JWT secret.")));
        var token = new JwtSecurityToken(settings["ValidIssuer"], settings["ValidAudience"], claims,
            expires: DateTime.UtcNow.AddHours(settings.GetValue<double>("TokenExpiryTimeInHour")),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
