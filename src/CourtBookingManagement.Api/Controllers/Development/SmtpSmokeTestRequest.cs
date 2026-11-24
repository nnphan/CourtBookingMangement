using System.ComponentModel.DataAnnotations;

namespace CourtBookingManagement.Api.Controllers.Development;

/// <summary>Specifies the approved recipient for a development SMTP smoke test.</summary>
public sealed record SmtpSmokeTestRequest
{
    [Required, EmailAddress, MaxLength(254)]
    public required string Recipient { get; init; }
}
