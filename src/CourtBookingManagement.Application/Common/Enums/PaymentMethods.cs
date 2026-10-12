namespace CourtBookingManagement.Application.Common.Enums;

public static class PaymentMethods
{
    public const string Cash = "Cash";
    public const string Momo = "Momo";
    public const string BankTransfer = "BankTransfer";

    public static readonly string[] All =
    [
        Cash,
        Momo,
        BankTransfer
    ];
}