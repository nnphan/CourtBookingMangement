using System.Globalization;
using System.Net.Mail;
using System.Text.Encodings.Web;
using CourtBookingManagement.Application.Email;
using CourtBookingManagement.Application.StaffManagement.Interfaces;
using CourtBookingManagement.Application.StaffManagement.Models;

namespace CourtBookingManagement.Infrastructure.Email;

public sealed class StaffInvitationEmailComposer(
    StaffInvitationActivationUrlBuilder activationUrlBuilder) : IStaffInvitationEmailComposer
{
    private static readonly TimeZoneInfo VietnamTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");

    public EmailMessage Compose(StaffInvitationEmailData invitation)
    {
        if (!MailAddress.TryCreate(invitation.Recipient, out _))
        {
            throw new ArgumentException("The staff invitation email address is invalid.", nameof(invitation));
        }

        if (string.IsNullOrWhiteSpace(invitation.DisplayName) ||
            invitation.BranchNames.Count == 0 ||
            invitation.BranchNames.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("The staff invitation details are invalid.", nameof(invitation));
        }

        var activationUrl = activationUrlBuilder.Build(invitation.Token, invitation.Recipient);
        var expiry = TimeZoneInfo.ConvertTime(invitation.ExpiresAt, VietnamTimeZone);
        var expiryText = string.Concat(
            expiry.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture),
            " (UTC",
            expiry.ToString("zzz", CultureInfo.InvariantCulture),
            ", Vietnam time)");
        var branchText = string.Join(
            Environment.NewLine,
            invitation.BranchNames.Select(branchName => $"- {branchName}"));
        var branchHtml = string.Join(
            Environment.NewLine,
            invitation.BranchNames.Select(branchName =>
                $"<li>{HtmlEncoder.Default.Encode(branchName)}</li>"));
        var encodedName = HtmlEncoder.Default.Encode(invitation.DisplayName);
        var encodedExpiry = HtmlEncoder.Default.Encode(expiryText);
        var encodedUrl = HtmlEncoder.Default.Encode(activationUrl);

        var textBody = $"""
            Hello {invitation.DisplayName},

            You have been invited to join the staff team at:
            {branchText}

            Activate your account using this one-time link:
            {activationUrl}

            This link expires on {expiryText} and can only be used once.
            If you were not expecting this invitation, contact the branch owner. Do not share this link.
            """;

        var htmlBody = $"""
            <!doctype html>
            <html lang="en">
            <head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"></head>
            <body style="margin:0;padding:24px;background-color:#f4f6f8;font-family:Arial,Helvetica,sans-serif;color:#202124;">
              <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="max-width:600px;margin:0 auto;background:#ffffff;border-collapse:collapse;">
                <tr><td style="padding:32px;">
                  <h1 style="font-size:22px;margin:0 0 16px;">Activate your Staff account</h1>
                  <p>Hello {encodedName},</p>
                  <p>You have been invited to join the staff team at:</p>
                  <ul>{branchHtml}</ul>
                  <p style="margin:28px 0;">
                    <a href="{encodedUrl}" style="display:inline-block;padding:14px 22px;background:#176b4d;color:#ffffff;text-decoration:none;border-radius:4px;">Activate account</a>
                  </p>
                  <p>If the button does not work, copy and paste this link into your browser:</p>
                  <p style="word-break:break-all;"><a href="{encodedUrl}">{encodedUrl}</a></p>
                  <p>This one-time link expires on <strong>{encodedExpiry}</strong> and can only be used once.</p>
                  <p>If you were not expecting this invitation, contact the branch owner. Do not share this link.</p>
                </td></tr>
              </table>
            </body>
            </html>
            """;

        return new EmailMessage(
            invitation.Recipient,
            "Activate your Staff account",
            textBody,
            htmlBody);
    }
}
