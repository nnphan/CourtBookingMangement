namespace CourtBookingManagement.Application.Payments.Interfaces;

public interface ISePayPaymentCodeGenerator
{
    string Generate(string prefix);
}
