using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;

namespace NhuBinh_2921.Services;

public sealed class SmsGatewayOptions
{
    public string? Url { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
}

public sealed record SmsDispatchResult(bool Sent, string Message);

public sealed class SmsGatewayService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SmsGatewayService> _logger;

    public SmsGatewayService(HttpClient httpClient, IConfiguration configuration,
        ILogger<SmsGatewayService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<SmsDispatchResult> SendOtpAsync(string? phone, string code,
        CancellationToken cancellationToken = default)
    {
        var configuredUrl = GetSetting("Url", "ENGLISH7_SMS_GATEWAY_URL");
        var username = GetSetting("Username", "ENGLISH7_SMS_GATEWAY_USERNAME");
        var password = GetSetting("Password", "ENGLISH7_SMS_GATEWAY_PASSWORD");
        if (string.IsNullOrWhiteSpace(phone) ||
            string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return new(false, "SIM Gateway chưa được cấu hình; đang dùng OTP phát triển.");

        var target = NormalizePhone(phone);
        var localPhone = target.StartsWith("+84", StringComparison.Ordinal)
            ? $"0{target[3..]}"
            : target;

        var candidateUrls = new List<string>();
        if (!string.IsNullOrWhiteSpace(configuredUrl))
            candidateUrls.Add(configuredUrl.TrimEnd('/'));
        candidateUrls.Add("http://127.0.0.1:8080");
        candidateUrls.Add("http://10.35.92.145:8080");

        var distinctUrls = candidateUrls.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));

        foreach (var baseUrl in distinctUrls)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/message");
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
                request.Content = JsonContent.Create(new
                {
                    message = $"Ma OTP KidGuard cua ban la: {code}. Ma co hieu luc trong 5 phut.",
                    phoneNumbers = new[] { localPhone }
                });

                using var response = await _httpClient.SendAsync(request, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("SIM Gateway ({BaseUrl}) accepted an OTP message request for {Phone}.", baseUrl, localPhone);
                    return new(true, $"Mã OTP đã được gửi qua SMS đến số {localPhone}.");
                }

                _logger.LogWarning("SIM Gateway ({BaseUrl}) returned HTTP {StatusCode}.", baseUrl, (int)response.StatusCode);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Could not reach SIM Gateway at {BaseUrl}.", baseUrl);
            }
        }

        return new(false, "Không kết nối được SIM Gateway; đang dùng OTP phát triển.");
    }

    public static string NormalizePhone(string phone)
    {
        var cleaned = new string(phone.Where(c => char.IsDigit(c) || c == '+').ToArray());
        if (cleaned.StartsWith('+')) return cleaned;
        if (cleaned.StartsWith('0')) return $"+84{cleaned[1..]}";
        if (cleaned.StartsWith("84", StringComparison.Ordinal)) return $"+{cleaned}";
        return $"+84{cleaned}";
    }

    private string? GetSetting(string key, string environmentName) =>
        Environment.GetEnvironmentVariable(environmentName)
        ?? _configuration[$"SmsGateway:{key}"];
}
