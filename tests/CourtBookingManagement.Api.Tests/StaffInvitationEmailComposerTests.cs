using CourtBookingManagement.Application.StaffManagement.Models;
using CourtBookingManagement.Infrastructure.Auth;
using CourtBookingManagement.Infrastructure.Email;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Xunit;

namespace CourtBookingManagement.Api.Tests;

public sealed class StaffInvitationEmailComposerTests
{
    [Fact]
    public void ActivationUrlEncodesTokenAndNormalizedEmailAsQueryParameters()
    {
        var tokenService = new StaffInvitationTokenService();
        var token = tokenService.GenerateToken();
        var builder = CreateUrlBuilder(tokenService);

        var url = builder.Build(token, "Staff+test@Example.com");
        var query = QueryHelpers.ParseQuery(new Uri(url).Query);

        Assert.Equal(token, query["token"].ToString());
        Assert.Equal("staff+test@example.com", query["email"].ToString());
        Assert.Equal(2, query.Count);
        Assert.Empty(new Uri(url).Fragment);
    }

    [Fact]
    public void ComposeEscapesUserContentAndIncludesVietnamExpiryAndPlainTextFallback()
    {
        var tokenService = new StaffInvitationTokenService();
        var token = tokenService.GenerateToken();
        var composer = new StaffInvitationEmailComposer(CreateUrlBuilder(tokenService));
        var invitation = new StaffInvitationEmailData(
            "staff+test@example.com",
            token,
            "Alex <script>alert(1)</script>",
            ["North & West", "<img src=x onerror=alert(1)>"],
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

        var message = composer.Compose(invitation);

        Assert.Contains("Alex &lt;script&gt;alert(1)&lt;/script&gt;", message.HtmlBody);
        Assert.Contains("North &amp; West", message.HtmlBody);
        Assert.Contains("&lt;img src=x onerror=alert(1)&gt;", message.HtmlBody);
        Assert.DoesNotContain("<script>", message.HtmlBody);
        Assert.DoesNotContain("<img src=x", message.HtmlBody);
        Assert.Contains("01/01/2026 07:00 (UTC+07:00, Vietnam time)", message.TextBody);
        Assert.Contains("North & West", message.TextBody);
        Assert.Contains("can only be used once", message.TextBody);
        Assert.Contains("contact the branch owner", message.TextBody);
        Assert.Contains($"token={token}", message.TextBody);
    }

    [Fact]
    public void ActivationUrlBuilderRejectsInvalidToken()
    {
        var builder = CreateUrlBuilder(new StaffInvitationTokenService());

        Assert.Throws<ArgumentException>(() => builder.Build("invalid", "staff@example.com"));
    }

    private static StaffInvitationActivationUrlBuilder CreateUrlBuilder(
        StaffInvitationTokenService tokenService) =>
        new(Options.Create(new StaffInvitationOptions
        {
            ActivationUrl = "https://example.com/staff/activate"
        }), tokenService);
}
