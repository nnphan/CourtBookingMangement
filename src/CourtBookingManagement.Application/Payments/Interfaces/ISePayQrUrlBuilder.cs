using CourtBookingManagement.Application.Options;

namespace CourtBookingManagement.Application.Payments.Interfaces;

public interface ISePayQrUrlBuilder
{
    string Build(long amount, string paymentCode, SePayOptions options);
}
