using System.ComponentModel.DataAnnotations;

namespace CourtBookingManagement.Application.Options;

public sealed class MomoOptions
{
    public const string SectionName = "Momo";

    public string Environment { get; set; } = "Sandbox";
    public string PartnerCode { get; set; } = string.Empty;
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string CreateEndpoint { get; set; } = "https://test-payment.momo.vn/v2/gateway/api/create";
    public string QueryEndpoint { get; set; } = "https://test-payment.momo.vn/v2/gateway/api/query";
    public string IpnUrl { get; set; } = string.Empty;
    public string RedirectUrl { get; set; } = string.Empty;

    [Range(1, 60)]
    public int RequestTimeoutSeconds { get; set; } = 15;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(PartnerCode)
        && !string.IsNullOrWhiteSpace(AccessKey)
        && !string.IsNullOrWhiteSpace(SecretKey)
        && Uri.TryCreate(CreateEndpoint, UriKind.Absolute, out _)
        && Uri.TryCreate(QueryEndpoint, UriKind.Absolute, out _)
        && Uri.TryCreate(IpnUrl, UriKind.Absolute, out _)
        && Uri.TryCreate(RedirectUrl, UriKind.Absolute, out _);
}
