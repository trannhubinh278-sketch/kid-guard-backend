using System.Net;
using System.Net.Mail;

namespace NhuBinh_2921.Services;

public sealed record EmailDispatchResult(bool Sent, string Message);

public sealed class EmailOtpService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailOtpService> _logger;

    public EmailOtpService(IConfiguration configuration, ILogger<EmailOtpService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<EmailDispatchResult> SendRegistrationOtpAsync(
        string email, string code, CancellationToken cancellationToken = default)
    {
        var host = GetSetting("Host", "KIDGUARD_EMAIL_HOST");
        var username = GetSetting("Username", "KIDGUARD_EMAIL_USERNAME");
        var password = GetSetting("Password", "KIDGUARD_EMAIL_PASSWORD");
        var fromEmail = GetSetting("FromEmail", "KIDGUARD_EMAIL_FROM") ?? username;

        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(username) ||
            string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(fromEmail))
            return new(false, "Email chưa được cấu hình; đang dùng OTP phát triển.");

        var portText = GetSetting("Port", "KIDGUARD_EMAIL_PORT");
        var port = int.TryParse(portText, out var configuredPort) ? configuredPort : 587;
        var enableSslText = GetSetting("EnableSsl", "KIDGUARD_EMAIL_ENABLE_SSL");
        var enableSsl = !bool.TryParse(enableSslText, out var configuredSsl) || configuredSsl;
        var fromName = GetSetting("FromName", "KIDGUARD_EMAIL_FROM_NAME") ?? "Kid Guard";

        try
        {
            using var message = new MailMessage
            {
                From = new MailAddress(fromEmail, fromName),
                Subject = "Mã xác thực đăng ký Kid Guard",
                Body = $"Mã OTP đăng ký Kid Guard của bạn là: {code}. Mã có hiệu lực trong 5 phút.",
                IsBodyHtml = false
            };
            message.To.Add(new MailAddress(email));

            using var client = new SmtpClient(host, port)
            {
                EnableSsl = enableSsl,
                Credentials = new NetworkCredential(username, password)
            };
            await client.SendMailAsync(message, cancellationToken);
            return new(true, $"Mã OTP đã được gửi đến email {MaskEmail(email)}.");
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Could not send registration OTP email to {Email}.", MaskEmail(email));
            return new(false, "Không gửi được email; đang dùng OTP phát triển.");
        }
    }

    public static string MaskEmail(string email)
    {
        var parts = email.Split('@', 2);
        if (parts.Length != 2 || parts[0].Length < 2) return "***";
        return $"{parts[0][0]}***{parts[0][^1]}@{parts[1]}";
    }

    private string? GetSetting(string key, string environmentName) =>
        Environment.GetEnvironmentVariable(environmentName)
        ?? _configuration[$"Email:{key}"];
}
