namespace CourtBookingManagement.Application.Auth.DTOs;

public sealed class LoginByPhoneNumberRequest
{
    public string PhoneNumber { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;
}