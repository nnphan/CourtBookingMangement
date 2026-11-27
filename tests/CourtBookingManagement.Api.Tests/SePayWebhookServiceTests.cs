using System.Text.Json;
using CourtBookingManagement.Application.Bookings.Interfaces;
using CourtBookingManagement.Application.Options;
using CourtBookingManagement.Application.Payments.Contracts;
using CourtBookingManagement.Application.Payments.Interfaces;
using CourtBookingManagement.Application.Payments.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace CourtBookingManagement.Api.Tests;

public sealed class SePayWebhookServiceTests
{
    [Theory]
    [InlineData("BCM226", "Thanh toan BCM226A9044D3", "BCM226A9044D3")]
    [InlineData("", "bcm226a9044d3.", "BCM226A9044D3")]
    [InlineData(null, "Thanh toan BCM226A9044D3", "BCM226A9044D3")]
    [InlineData(" BCM226A9044D3 ", null, "BCM226A9044D3")]
    [InlineData(null, "BCM226A9044D3 / bcm226a9044d3", "BCM226A9044D3")]
    [InlineData(null, "BCM226A9044D3 / BCM1234567890", null)]
    [InlineData("BCM1234567890", "Thanh toan BCM226A9044D3", null)]
    [InlineData("BCM226", "XBCM226A9044D3Z", null)]
    public async Task ProcessAsync_MatchesOnlyAnUnambiguousFullPaymentCode(
        string? code, string? content, string? expected)
    {
        var repository = new Mock<IPaymentRepository>(MockBehavior.Strict);
        repository.Setup(x => x.ProcessSePayWebhookAsync(
                It.IsAny<SePayWebhookRequest>(), expected, It.IsAny<DateTimeOffset?>(),
                true, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SePayWebhookProcessingOutcome(false, null, []))
            .Verifiable();
        var authenticator = new Mock<ISePayWebhookAuthenticator>();
        authenticator.Setup(x => x.IsAuthenticated(It.IsAny<ReadOnlyMemory<byte>>(), null, null, null))
            .Returns(true);
        var service = new SePayWebhookService(
            repository.Object, authenticator.Object, Mock.Of<IBookingHoldCache>(),
            Options.Create(new SePayOptions { BankAccountNumber = "00000001" }),
            NullLogger<SePayWebhookService>.Instance);
        var body = JsonSerializer.SerializeToUtf8Bytes(new SePayWebhookRequest
        {
            Id = 1,
            AccountNumber = "00000001",
            Code = code,
            Content = content,
            TransferType = "in",
            TransferAmount = 20000
        });

        var result = await service.ProcessAsync(body, null, null, null, default);

        Assert.True(result.IsSuccess);
        repository.Verify();
    }
}
