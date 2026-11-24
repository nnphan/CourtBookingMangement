using System.Text.Encodings.Web;
using CourtBookingManagement.Application.Email;
using CourtBookingManagement.Application.StaffManagement.Interfaces;
using CourtBookingManagement.Infrastructure.Email;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CourtBookingManagement.Infrastructure.Auth;

public sealed class SmtpStaffInvitationDelivery(
    IEmailSender emailSender,
    IOptions<StaffInvitationOptions> options,
    ILogger<SmtpStaffInvitationDelivery> logger) : IStaffInvitationDelivery
{
    public bool IsConfigured => options.Value.Smtp.Enabled;

    public async Task<bool> SendAsync(string email, string token, CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            logger.LogWarning("Staff invitation delivery is not configured.");
            return false;
        }

        var invitationOptions = options.Value;
        var link = $"{invitationOptions.ActivationUrl}#token={Uri.EscapeDataString(token)}";
        var escapedLink = HtmlEncoder.Default.Encode(link);
        var expiration = $"This link expires in {invitationOptions.ExpirationHours} hours.";

        return await emailSender.SendAsync(
            new EmailMessage(
                email,
                "Staff account activation",
                $"Use this link to activate your staff account: {link}{Environment.NewLine}{expiration}",
                $"<p>Use this link to activate your staff account:</p>" +
                $"<p><a href=\"{escapedLink}\">Activate staff account</a></p>" +
                $"<p>{expiration}</p>"),
            cancellationToken);
    }
}
