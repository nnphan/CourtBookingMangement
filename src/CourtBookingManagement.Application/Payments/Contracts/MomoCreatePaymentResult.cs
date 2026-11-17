namespace CourtBookingManagement.Application.Payments.Contracts;

public sealed record MomoCreatePaymentResult(
    string OrderId,
    string RequestId,
    long Amount,
    string OrderInfo,
    string RedirectUrl,
    string IpnUrl,
    string ExtraData,
    string Signature);
