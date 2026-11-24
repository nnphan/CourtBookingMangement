using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Hosting;

namespace CourtBookingManagement.Infrastructure.Email;

public sealed class StaffInvitationOptions
{
    public const string SectionName = "StaffInvitations";

    public string ActivationUrl { get; set; } = string.Empty;
    public int ExpirationHours { get; set; } = 24;
    public int ResendCooldownMinutes { get; set; } = 5;
    public int MaxResendsPerWindow { get; set; } = 3;
    public int ResendWindowMinutes { get; set; } = 15;
    public SmtpOptions Smtp { get; set; } = new();

    public bool IsValidConfiguration(IHostEnvironment environment)
    {
        if (!Smtp.Enabled)
        {
            return true;
        }

        var activationUrlIsValid =
            Uri.TryCreate(ActivationUrl, UriKind.Absolute, out var activationUrl) &&
            string.IsNullOrEmpty(activationUrl.Query) &&
            string.IsNullOrEmpty(activationUrl.Fragment) &&
            string.IsNullOrEmpty(activationUrl.UserInfo) &&
            (activationUrl.Scheme == Uri.UriSchemeHttps ||
             (environment.IsDevelopment() &&
              activationUrl.Scheme == Uri.UriSchemeHttp &&
              IsLoopback(activationUrl.Host)));

        return activationUrlIsValid &&
            !string.IsNullOrWhiteSpace(Smtp.Host) &&
            Uri.CheckHostName(Smtp.Host) != UriHostNameType.Unknown &&
            Smtp.Port == 587 &&
            Smtp.Security == SmtpSecurityMode.StartTls &&
            MailAddress.TryCreate(Smtp.Sender, out _) &&
            !string.IsNullOrWhiteSpace(Smtp.FromName) &&
            Smtp.FromName.Length <= 100 &&
            !string.IsNullOrWhiteSpace(Smtp.Username) &&
            !string.IsNullOrWhiteSpace(Smtp.Password) &&
            Smtp.TimeoutSeconds is >= 5 and <= 60 &&
            ExpirationHours is >= 1 and <= 168 &&
            ResendCooldownMinutes is >= 1 and <= 1440 &&
            MaxResendsPerWindow is >= 1 and <= 100 &&
            ResendWindowMinutes is >= 1 and <= 1440;
    }

    private static bool IsLoopback(string host) =>
        string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) ||
        (IPAddress.TryParse(host, out var address) && IPAddress.IsLoopback(address));
}

public sealed class SmtpOptions
{
    public bool Enabled { get; set; }
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public SmtpSecurityMode Security { get; set; } = SmtpSecurityMode.StartTls;
    public string Sender { get; set; } = string.Empty;
    public string FromName { get; set; } = "Badminton Court Management";
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 15;
}

public enum SmtpSecurityMode
{
    StartTls
}
