using System.Security.Cryptography;
using System.Text;
using CourtBookingManagement.Application.StaffManagement.Interfaces;

namespace CourtBookingManagement.Infrastructure.Auth;

public sealed class StaffInvitationTokenService : IStaffInvitationTokenService
{
    private const int TokenByteLength = 32;
    private const int EncodedTokenLength = 43;

    public string GenerateToken() => EncodeToken(RandomNumberGenerator.GetBytes(TokenByteLength));

    public bool IsValidToken(string token)
    {
        if (string.IsNullOrEmpty(token) ||
            token.Length != EncodedTokenLength ||
            token.Any(character => !IsBase64UrlCharacter(character)))
        {
            return false;
        }

        try
        {
            var bytes = Convert.FromBase64String(
                token.Replace('-', '+').Replace('_', '/') + "=");
            return bytes.Length == TokenByteLength &&
                string.Equals(EncodeToken(bytes), token, StringComparison.Ordinal);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public string HashToken(string token)
    {
        if (!IsValidToken(token))
        {
            throw new ArgumentException("The staff invitation token is invalid.", nameof(token));
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }

    private static string EncodeToken(byte[] bytes) =>
        Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static bool IsBase64UrlCharacter(char character) =>
        character is >= 'A' and <= 'Z' or
            >= 'a' and <= 'z' or
            >= '0' and <= '9' or '-' or '_';
}
