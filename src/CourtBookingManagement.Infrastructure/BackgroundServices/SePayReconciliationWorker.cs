using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using CourtBookingManagement.Application.Options;
using CourtBookingManagement.Application.Payments.Contracts;
using CourtBookingManagement.Application.Payments.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CourtBookingManagement.Infrastructure.BackgroundServices;

public sealed class SePayReconciliationWorker(
    IServiceScopeFactory scopes,
    IHttpClientFactory clients,
    IOptions<SePayOptions> options,
    ILogger<SePayReconciliationWorker> logger) : BackgroundService
{
    public const string ClientName = "SePayTransactions";
    private const int MaxPagesPerSweep = 20;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.ReconciliationEnabled)
        {
            if (!string.IsNullOrWhiteSpace(settings.ApiToken) || settings.ApiBankAccountId.HasValue)
                logger.LogWarning(
                    "SePay reconciliation is disabled. PaymentConfigured: {PaymentConfigured}, ApiTokenConfigured: {ApiTokenConfigured}, ApiBankAccountIdConfigured: {ApiBankAccountIdConfigured}. Verify that ApiBaseUrl matches the configured SePay environment.",
                    settings.IsConfigured,
                    !string.IsNullOrWhiteSpace(settings.ApiToken),
                    settings.ApiBankAccountId is Guid id && id != Guid.Empty);
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(settings.ReconciliationIntervalSeconds));
        do
        {
            try
            {
                await SweepAsync(settings, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogWarning("SePay reconciliation will retry after {ErrorType}.", exception.GetType().Name);
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task SweepAsync(SePayOptions settings, CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var processor = scope.ServiceProvider.GetRequiredService<SePayWebhookService>();
        using var client = clients.CreateClient(ClientName);
        client.Timeout = TimeSpan.FromSeconds(settings.RequestTimeoutSeconds);
        var vietnam = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
        var now = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, vietnam);
        var from = Uri.EscapeDataString(FormatDate(now.AddHours(-settings.ReconciliationLookbackHours)));
        var to = Uri.EscapeDataString(FormatDate(now));
        var prefix = Uri.EscapeDataString(settings.PaymentCodePrefix);
        var endpoint = settings.ApiBaseUrl.TrimEnd('/') + "/transactions"
            + $"?bank_account_id={settings.ApiBankAccountId:D}&transfer_type=in"
            + $"&transaction_content={prefix}&transaction_date_from={from}&transaction_date_to={to}"
            + "&per_page=100";

        for (var page = 1; page <= MaxPagesPerSweep; page++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, endpoint + $"&page={page}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiToken);
            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("SePay transaction API returned HTTP {StatusCode}.", (int)response.StatusCode);
                if (response.StatusCode == HttpStatusCode.TooManyRequests
                    && response.Headers.RetryAfter?.Delta is { } delay && delay > TimeSpan.Zero)
                    await Task.Delay(delay, cancellationToken);
                return;
            }

            using var document = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(cancellationToken));
            var root = document.RootElement;
            if (Text(root, "status") != "success"
                || !root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
                throw new JsonException("SePay transactions response shape is invalid.");

            foreach (var item in data.EnumerateArray())
            {
                var transaction = ReadTransaction(item, settings.ApiBankAccountId!.Value);
                if (transaction is not null)
                    await processor.ApplyReconciledTransactionAsync(transaction, item.GetRawText(), cancellationToken);
            }

            if (!root.TryGetProperty("meta", out var meta) || !meta.TryGetProperty("pagination", out var pagination)
                || !pagination.TryGetProperty("has_more", out var hasMore)
                || hasMore.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                throw new JsonException("SePay pagination metadata is invalid.");
            if (!hasMore.GetBoolean())
                return;

            await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
        }

        logger.LogWarning("SePay reconciliation reached its page limit. Reduce the lookback or review provider volume.");
    }

    private static SePayWebhookRequest? ReadTransaction(JsonElement row, Guid bankAccountId)
    {
        if (row.ValueKind != JsonValueKind.Object
            || !Guid.TryParse(Text(row, "id"), out var id) || id == Guid.Empty
            || !Guid.TryParse(Text(row, "bank_account_id"), out var account) || account != bankAccountId
            || Text(row, "transfer_type") != "in"
            || !row.TryGetProperty("amount_in", out var amount) || amount.ValueKind != JsonValueKind.Number
            || !amount.TryGetInt64(out var received) || received <= 0)
            return null;

        return new SePayWebhookRequest
        {
            ApiTransactionId = id,
            Gateway = Text(row, "bank_brand_name"),
            AccountNumber = Text(row, "account_number"),
            TransactionDate = Text(row, "transaction_date"),
            TransferType = "in",
            TransferAmount = received,
            Code = Text(row, "code"),
            Content = Text(row, "transaction_content"),
            ReferenceCode = Text(row, "reference_number"),
        };
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() : null;

    private static string FormatDate(DateTimeOffset value) =>
        value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
}
