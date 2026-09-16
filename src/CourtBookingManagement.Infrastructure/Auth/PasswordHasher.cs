using CourtBookingManagement.Application.Auth.Interfaces;

namespace CourtBookingManagement.Infrastructure.Auth;

public static class PasswordHasher
{
    public static string HashPassword(string password) => BCrypt.Net.BCrypt.HashPassword(password);

    public static bool VerifyPassword(string password, string passwordHash) => BCrypt.Net.BCrypt.Verify(password, passwordHash);
}
