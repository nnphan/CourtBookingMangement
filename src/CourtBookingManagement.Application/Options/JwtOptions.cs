using System.ComponentModel.DataAnnotations;

namespace CourtBookingManagement.Application.Options;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required(ErrorMessage = "JWT secret key is required")]
    [MinLength(32, ErrorMessage = "JWT secret key must be at least 32 characters long")]
    public required string SecretKey { get; set; }

    [Required(ErrorMessage = "JWT issuer is required")]
    public required string Issuer { get; set; }

    [Required(ErrorMessage = "JWT audience is required")]
    public required string Audience { get; set; }

    [Range(1, 1440, ErrorMessage = "Access token expiration must be between 1 and 1440 minutes")]
    public int AccessTokenExpirationMinutes { get; set; } = 15;

    [Range(1, 3650, ErrorMessage = "Refresh token expiration must be between 1 and 3650 days")]
    public int RefreshTokenExpirationDays { get; set; } = 30;
}
