namespace CourtBookingManagement.Application.Auth.Interfaces;

public interface IJwtTokenService
{
    string GenerateAccessToken(Guid userId, string email, IEnumerable<string> roles, IEnumerable<string> permissions);

    string GenerateRefreshToken();

    string HashToken(string token);
}
