using System.Security.Cryptography;
using CourtBookingManagement.Application.Payments.Interfaces;

namespace CourtBookingManagement.Application.Payments.Services;

public sealed class SePayPaymentCodeGenerator : ISePayPaymentCodeGenerator
{
    public string Generate(string prefix) =>
        $"{prefix}{RandomNumberGenerator.GetHexString(10)}";
}
