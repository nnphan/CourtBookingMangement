using System.Net;
using System.Net.Mail;
using CourtBookingManagement.Application.StaffManagement.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CourtBookingManagement.Infrastructure.Auth;

public sealed class SmtpStaffInvitationDelivery(
    IConfiguration configuration,
    ILogger<SmtpStaffInvitationDelivery> logger) : IStaffInvitationDelivery
{
    private string? Host => configuration["StaffInvitations:Smtp:Host"];
    private string? Sender => configuration["StaffInvitations:Smtp:Sender"];
    private string? Username => configuration["StaffInvitations:Smtp:Username"];
    private string? Password => configuration["StaffInvitations:Smtp:Password"];
    private string? ActivationUrl => configuration["StaffInvitations:ActivationUrl"];

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Host) &&
        MailAddress.TryCreate(Sender, out _) &&
        Uri.TryCreate(ActivationUrl, UriKind.Absolute, out var url) &&
        url.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(url.Fragment) &&
        string.IsNullOrEmpty(url.Query) &&
        (string.IsNullOrWhiteSpace(Username) == string.IsNullOrWhiteSpace(Password)) &&
        int.TryParse(configuration["StaffInvitations:Smtp:Port"], out var port) && port is > 0 and <= 65535;

    public async Task<bool> SendAsync(string email, string token, CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            logger.LogWarning("Staff invitation delivery is not configured.");
            return false;
        }

        using var message = new MailMessage(Sender!, email)
        {
            Subject = "Staff account activation",
            Body = $"Use this link to activate your staff account: {ActivationUrl}#token={token}\n" +
                   "This link expires in 24 hours."
        };
        using var client = new SmtpClient(Host!, int.Parse(configuration["StaffInvitations:Smtp:Port"]!))
        {
            EnableSsl = true,
            Timeout = 15000,
            UseDefaultCredentials = false
        };
        if (!string.IsNullOrWhiteSpace(Username))
        {
            client.Credentials = new NetworkCredential(Username, Password);
        }

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(15));
            await client.SendMailAsync(message, timeout.Token);
            return true;
        }
        catch (SmtpException)
        {
            logger.LogWarning("Staff invitation SMTP delivery failed.");
            return false;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Staff invitation SMTP delivery timed out.");
            return false;
        }
    }
}
