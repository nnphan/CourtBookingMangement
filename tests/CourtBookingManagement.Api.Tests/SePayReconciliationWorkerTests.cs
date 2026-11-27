using System.Net;
using System.Text;
using CourtBookingManagement.Application.Bookings.Interfaces;
using CourtBookingManagement.Application.Options;
using CourtBookingManagement.Application.Payments.Contracts;
using CourtBookingManagement.Application.Payments.Interfaces;
using CourtBookingManagement.Application.Payments.Services;
using CourtBookingManagement.Infrastructure.BackgroundServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace CourtBookingManagement.Api.Tests;

public sealed class SePayReconciliationWorkerTests
{
    [Fact]
    public async Task Sweep_UsesProviderApiAndPassesOnlyTheMatchingIncomingTransfer()
    {
        var bankId = Guid.NewGuid();
        var transactionId = Guid.NewGuid();
        var settings = new SePayOptions
        {
            ApiBankAccountId = bankId,
            ApiToken = "test-token",
            WebhookApiKey = "example-key",
            BankCode = "MB",
            BankAccountNumber = "00001234",
            AccountHolderName = "Test Account",
        };
        var options = Options.Create(settings);
        var captured = new TaskCompletionSource<SePayWebhookRequest>(TaskCreationOptions.RunContinuationsAsynchronously);
        var repository = new Mock<IPaymentRepository>(MockBehavior.Strict);
        repository.Setup(x => x.ProcessSePayWebhookAsync(
                It.IsAny<SePayWebhookRequest>(), "BCM226A9044D3", It.IsAny<DateTimeOffset?>(), true,
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<SePayWebhookRequest, string?, DateTimeOffset?, bool, string, CancellationToken>(
                (request, _, date, _, _, _) =>
                {
                    Assert.NotNull(date);
                    captured.TrySetResult(request);
                })
            .ReturnsAsync(new SePayWebhookProcessingOutcome(false, null, []));
        await using var serviceProvider = new ServiceCollection()
            .AddScoped<SePayWebhookService>(_ => new SePayWebhookService(
                repository.Object, Mock.Of<ISePayWebhookAuthenticator>(MockBehavior.Strict),
                Mock.Of<IBookingHoldCache>(), options, NullLogger<SePayWebhookService>.Instance))
            .BuildServiceProvider();
        var handler = new ProviderHandler(bankId, transactionId);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient(SePayReconciliationWorker.ClientName))
            .Returns(() => new HttpClient(handler));
        using var worker = new SePayReconciliationWorker(
            serviceProvider.GetRequiredService<IServiceScopeFactory>(), factory.Object, options,
            NullLogger<SePayReconciliationWorker>.Instance);

        await worker.StartAsync(default);
        try
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var transfer = await captured.Task.WaitAsync(deadline.Token);
            Assert.Equal(transactionId, transfer.ApiTransactionId);
            Assert.Equal(20_000m, transfer.TransferAmount);
            Assert.Equal("FT123456", transfer.ReferenceCode);
            Assert.Equal("Bearer test-token", handler.Authorization);
            Assert.Contains("bank_account_id=" + bankId, handler.Url);
            Assert.Contains("transaction_content=BCM", handler.Url);
        }
        finally
        {
            await worker.StopAsync(default);
        }

        repository.Verify(x => x.ProcessSePayWebhookAsync(
            It.IsAny<SePayWebhookRequest>(), It.IsAny<string>(), It.IsAny<DateTimeOffset?>(),
            It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Reconciliation_DoesNotSendTokenToAnotherHostOrEnvironment()
    {
        var settings = new SePayOptions
        {
            ApiBankAccountId = Guid.NewGuid(), ApiToken = "test-token", WebhookApiKey = "example-key",
            BankCode = "MB", BankAccountNumber = "00001234", AccountHolderName = "Test Account",
        };
        Assert.True(settings.ReconciliationEnabled);
        settings.ApiBaseUrl = "https://example.test/v2";
        Assert.False(settings.ReconciliationEnabled);
        settings.ApiBaseUrl = "https://userapi.sepay.vn/v2";
        Assert.False(settings.ReconciliationEnabled);
    }

    private sealed class ProviderHandler(Guid bankId, Guid transactionId) : HttpMessageHandler
    {
        public string? Authorization { get; private set; }
        public string Url { get; private set; } = string.Empty;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Authorization = request.Headers.Authorization?.ToString();
            Url = request.RequestUri!.AbsoluteUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent($$"""
                    {"status":"success","data":[
                        {"id":"{{Guid.NewGuid()}}","bank_account_id":"{{Guid.NewGuid()}}","transfer_type":"in","amount_in":20000},
                        {"id":"{{transactionId}}","bank_account_id":"{{bankId}}","account_number":"00001234",
                         "transfer_type":"in","amount_in":20000,"transaction_date":"2026-11-27 15:22:00",
                         "reference_number":"FT123456","code":"BCM226","transaction_content":"BK BCM226A9044D3"}
                    ],"meta":{"pagination":{"has_more":false} } }
                    """, Encoding.UTF8, "application/json"),
            });
        }
    }
}

