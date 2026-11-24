using System.Net.Mail;
using CourtBookingManagement.Application.StaffManagement.Interfaces;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace CourtBookingManagement.Infrastructure.Email;

public sealed class StaffInvitationActivationUrlBuilder(
    IOptions<StaffInvitationOptions> options,
    IStaffInvitationTokenService tokens)
{
    public string Build(string token, string email)
    {
        if (!tokens.IsValidToken(token))
        {
            throw new ArgumentException("The staff invitation token is invalid.", nameof(token));
        }

        if (!MailAddress.TryCreate(email, out var mailAddress))
        {
            throw new ArgumentException("The staff invitation email address is invalid.", nameof(email));
        }

        var activationUrl = options.Value.ActivationUrl;
        if (!Uri.TryCreate(activationUrl, UriKind.Absolute, out var baseUri) ||
            !string.IsNullOrEmpty(baseUri.Query) ||
            !string.IsNullOrEmpty(baseUri.Fragment) ||
            !string.IsNullOrEmpty(baseUri.UserInfo))
        {
            throw new InvalidOperationException("The staff activation URL configuration is invalid.");
        }

        var normalizedEmail = mailAddress.Address.Trim().ToLowerInvariant();
        var urlWithToken = QueryHelpers.AddQueryString(activationUrl, "token", token);
        return QueryHelpers.AddQueryString(urlWithToken, "email", normalizedEmail);
    }
}
