namespace CourtBookingManagement.Application.Payments.Contracts;

public sealed record MomoIpnResponse(int ResultCode, string Message);
