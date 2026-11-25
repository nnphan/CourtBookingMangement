using CourtBookingManagement.Api.Realtime;
using CourtBookingManagement.Application.Auth.Interfaces;
using Microsoft.AspNetCore.DataProtection;
using Xunit;

namespace CourtBookingManagement.CourtSelection.Tests;

public sealed class CourtSelectionIdentityTests
{
    [Fact]
    public void GuestCredentialIsBoundToItsBrowserSession()
    {
        var protection = new EphemeralDataProtectionProvider();
        var identity = new CourtSelectionIdentity(protection, new CurrentUser(null));
        var session = Guid.NewGuid();
        var issued = identity.Issue(session);

        Assert.Null(identity.Resolve(session, null));
        Assert.Null(identity.Resolve(session, "forged"));
        Assert.Null(identity.Resolve(Guid.NewGuid(), issued.Token));
        var guest = identity.Resolve(session, issued.Token);
        Assert.NotNull(guest);
        Assert.Null(guest.UserId);
        Assert.Equal(guest, identity.Resolve(session, issued.Token));

        var userId = Guid.NewGuid();
        var authenticated = new CourtSelectionIdentity(protection, new CurrentUser(userId));
        Assert.Equal(userId, authenticated.Resolve(session, issued.Token)?.SubjectId);
    }

    [Fact]
    public void SignalRGroupIncludesTheNormalizedBranchAndBusinessDate()
    {
        var branch = Guid.Parse("dccb4f01-d78a-4114-aa88-e98b030c5659");
        Assert.Equal("court-status:dccb4f01d78a4114aa88e98b030c5659:2026-11-25",
            CourtStatusHub.Group(branch, new DateOnly(2026, 11, 25)));
    }

    private sealed record CurrentUser(Guid? UserId) : ICurrentUserService
    {
        public bool IsAuthenticated => UserId.HasValue;
        public string? Email => null;
        public IReadOnlyCollection<string> Roles => [];
        public IReadOnlyCollection<string> Permissions => [];
    }
}
