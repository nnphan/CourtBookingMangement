using System.Security.Cryptography;
using CourtBookingManagement.Application.Auth.Interfaces;
using CourtBookingManagement.Application.Bookings.DTOs;
using Microsoft.AspNetCore.DataProtection;

namespace CourtBookingManagement.Api.Realtime;

public sealed class CourtSelectionIdentity(IDataProtectionProvider protection, ICurrentUserService currentUser)
{
    public const string GuestHeader = "X-Court-Selection-Guest";
    private readonly ITimeLimitedDataProtector _guest = protection
        .CreateProtector("CourtBooking.CourtSelectionGuest.v1").ToTimeLimitedDataProtector();

    public GuestCredential Issue(Guid session)
    {
        var expiresAt = DateTimeOffset.UtcNow.AddDays(1);
        var token = _guest.Protect($"{session:N}:{Guid.NewGuid():N}", expiresAt);
        return new(token, expiresAt);
    }

    public CourtSelectionOwner? Resolve(Guid? session, string? token)
    {
        if (currentUser.IsAuthenticated)
            return currentUser.UserId is Guid user && user != Guid.Empty ? new(user, user) : null;
        if (session is null || string.IsNullOrWhiteSpace(token) || token.Length > 1024) return null;

        try
        {
            var payload = _guest.Unprotect(token);
            if (payload.Length == 65 && payload.StartsWith($"{session:N}:", StringComparison.Ordinal)
                && Guid.TryParseExact(payload[33..], "N", out var guest))
                return new(guest, null);
        }
        catch (CryptographicException)
        {
            // Expired or invalid guest credential.
        }
        return null;
    }

    public sealed record GuestCredential(string Token, DateTimeOffset ExpiresAt);
}
