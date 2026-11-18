using System.Globalization;
using CourtBookingManagement.Application.Options;
using CourtBookingManagement.Application.Payments.Interfaces;

namespace CourtBookingManagement.Application.Payments.Services;

public sealed class SePayQrUrlBuilder : ISePayQrUrlBuilder
{
    public string Build(long amount, string paymentCode, SePayOptions options)
    {
        var query = new[]
        {
            ("acc", options.BankAccountNumber),
            ("bank", options.BankCode),
            ("amount", amount.ToString(CultureInfo.InvariantCulture)),
            ("des", paymentCode),
            ("template", options.QrTemplate),
            ("accountName", options.AccountHolderName)
        };
        var builder = new UriBuilder(options.QrBaseUrl);
        var existingQuery = builder.Query.TrimStart('?');
        var paymentQuery = string.Join("&", query.Select(pair =>
            $"{Uri.EscapeDataString(pair.Item1)}={Uri.EscapeDataString(pair.Item2)}"));
        builder.Query = string.IsNullOrEmpty(existingQuery)
            ? paymentQuery
            : $"{existingQuery}&{paymentQuery}";
        return builder.Uri.AbsoluteUri;
    }
}
