using System.Security.Cryptography;

namespace CourtBookingManagement.Application.Bookings.Security;

public static class BookingHoldToken
{
    public static (string Token, byte[] Hash) Create()
    {
        var tokenBytes = RandomNumberGenerator.GetBytes(32);
        var token = Convert.ToBase64String(tokenBytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        return (token, SHA256.HashData(tokenBytes));
    }

    public static byte[]? Hash(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var base64 = token.Replace('-', '+').Replace('_', '/');
        base64 += new string('=', (4 - base64.Length % 4) % 4);
        try
        {
            var tokenBytes = Convert.FromBase64String(base64);
            return tokenBytes.Length == 32 ? SHA256.HashData(tokenBytes) : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
