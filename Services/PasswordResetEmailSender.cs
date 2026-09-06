using System.Net;
using System.Net.Mail;

namespace CTSHIPDashboard.Services;

public interface IPasswordResetEmailSender
{
    Task<bool> SendPasswordResetAsync(string email, string callbackUrl, CancellationToken cancellationToken = default);
}

public sealed class PasswordResetEmailSender : IPasswordResetEmailSender
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<PasswordResetEmailSender> _logger;

    public PasswordResetEmailSender(IConfiguration configuration, ILogger<PasswordResetEmailSender> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<bool> SendPasswordResetAsync(string email, string callbackUrl, CancellationToken cancellationToken = default)
    {
        string? host = _configuration["Email:Smtp:Host"];
        string? fromEmail = _configuration["Email:Smtp:FromEmail"];

        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(fromEmail))
        {
            _logger.LogWarning("Password reset email was not sent because Email:Smtp:Host or Email:Smtp:FromEmail is not configured. Reset link: {ResetLink}", callbackUrl);
            return false;
        }

        int port = _configuration.GetValue("Email:Smtp:Port", 587);
        bool enableSsl = _configuration.GetValue("Email:Smtp:EnableSsl", true);
        string? fromName = _configuration["Email:Smtp:FromName"];
        string? userName = _configuration["Email:Smtp:UserName"];
        string? password = _configuration["Email:Smtp:Password"];

        using var message = new MailMessage
        {
            From = new MailAddress(fromEmail, string.IsNullOrWhiteSpace(fromName) ? "CTSHIP Dashboard" : fromName),
            Subject = "Reset your CTSHIP Dashboard password",
            Body = $"<p>Use the secure link below to reset your CTSHIP Dashboard password.</p><p><a href=\"{callbackUrl}\">Reset password</a></p><p>If you did not request this change, you can ignore this email.</p>",
            IsBodyHtml = true
        };
        message.To.Add(email);

        using var client = new SmtpClient(host, port)
        {
            EnableSsl = enableSsl
        };

        if (!string.IsNullOrWhiteSpace(userName))
        {
            client.Credentials = new NetworkCredential(userName, password);
        }

        try
        {
            await client.SendMailAsync(message, cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Password reset email could not be sent to {Email}.", email);
            return false;
        }
    }
}
