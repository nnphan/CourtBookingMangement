using CourtBookingManagement.Domain.Abstractions;

namespace CourtBookingManagement.Domain.Users;

public static class UserErrors
{
    public static Error NotFound(Guid id) => new(
        "User.NotFound",
        $"User '{id}' was not found");

    public static readonly Error EmailIsRequired = new(
        "User.EmailRequired",
        "User email is required");

    public static readonly Error EmailIsTooLong = new(
        "User.EmailTooLong",
        "User email cannot exceed 100 characters");

    public static readonly Error PasswordHashIsRequired = new(
        "User.PasswordHashRequired",
        "User password hash is required");

    public static readonly Error PhoneNumberIsTooLong = new(
        "User.PhoneNumberTooLong",
        "User phone number cannot exceed 20 characters");
}