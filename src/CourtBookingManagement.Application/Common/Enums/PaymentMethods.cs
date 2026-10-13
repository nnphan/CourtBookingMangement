namespace CourtBookingManagement.Application.Common.Enums;

public static class PaymentMethods
{
    public const string Cash = "CASH";
    public const string Momo = "MOMO";
    public const string BankTransfer = "BANK_TRANSFER";

    public static readonly string[] All =
    [
        Cash,
        Momo,
        BankTransfer
    ];
}