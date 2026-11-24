using CourtBookingManagement.Infrastructure.Email;
using Microsoft.Extensions.Hosting;
using Moq;
using Xunit;

namespace CourtBookingManagement.Api.Tests;

public sealed class StaffInvitationOptionsTests
{
    [Fact]
    public void DisabledSmtpDoesNotRequireRelaySettings()
    {
        var options = new StaffInvitationOptions();

        Assert.True(options.IsValidConfiguration(CreateEnvironment(Environments.Production)));
    }

    [Theory]
    [InlineData("Development", "http://localhost:5173/staff/activate", true)]
    [InlineData("Development", "http://example.com/staff/activate", false)]
    [InlineData("Production", "https://example.com/staff/activate", true)]
    [InlineData("Production", "http://localhost:5173/staff/activate", false)]
    public void EnabledSmtpValidatesActivationUrlForEnvironment(
        string environmentName,
        string activationUrl,
        bool expected)
    {
        var options = CreateValidOptions(activationUrl);

        Assert.Equal(expected, options.IsValidConfiguration(CreateEnvironment(environmentName)));
    }

    [Fact]
    public void EnabledSmtpRejectsMissingCredentials()
    {
        var options = CreateValidOptions("https://example.com/staff/activate");
        options.Smtp.Password = string.Empty;

        Assert.False(options.IsValidConfiguration(CreateEnvironment(Environments.Production)));
    }

    [Fact]
    public void EnabledSmtpRejectsNonStartTlsPort()
    {
        var options = CreateValidOptions("https://example.com/staff/activate");
        options.Smtp.Port = 465;

        Assert.False(options.IsValidConfiguration(CreateEnvironment(Environments.Production)));
    }

    private static StaffInvitationOptions CreateValidOptions(string activationUrl) => new()
    {
        ActivationUrl = activationUrl,
        Smtp = new SmtpOptions
        {
            Enabled = true,
            Host = "smtp.example.com",
            Port = 587,
            Security = SmtpSecurityMode.StartTls,
            Sender = "staff@example.com",
            Username = "relay-user",
            Password = "relay-password"
        }
    };

    private static IHostEnvironment CreateEnvironment(string environmentName)
    {
        var environment = new Mock<IHostEnvironment>();
        environment.Setup(value => value.EnvironmentName).Returns(environmentName);
        return environment.Object;
    }
}
