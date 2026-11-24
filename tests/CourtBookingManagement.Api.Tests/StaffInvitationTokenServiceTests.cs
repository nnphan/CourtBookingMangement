using System.Security.Cryptography;
using System.Text;
using CourtBookingManagement.Infrastructure.Auth;
using Xunit;

namespace CourtBookingManagement.Api.Tests;

public sealed class StaffInvitationTokenServiceTests
{
    private readonly StaffInvitationTokenService _service = new();

    [Fact]
    public void GenerateTokenReturnsCanonicalBase64UrlForThirtyTwoRandomBytes()
    {
        var token = _service.GenerateToken();

        Assert.Equal(43, token.Length);
        Assert.Matches("^[A-Za-z0-9_-]{43}$", token);
        Assert.True(_service.IsValidToken(token));
    }

    [Theory]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA+/")]
    public void IsValidTokenRejectsNonCanonicalValues(string token)
    {
        Assert.False(_service.IsValidToken(token));
    }

    [Fact]
    public void HashTokenStoresSha256OfExactTokenTextAsHex()
    {
        var token = _service.GenerateToken();
        var expectedHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

        Assert.Equal(expectedHash, _service.HashToken(token));
        Assert.Equal(64, _service.HashToken(token).Length);
    }

    [Fact]
    public void HashTokenPreservesCaseSensitivity()
    {
        var token = _service.GenerateToken();
        var caseVariant = (token[0] == 'A' ? "B" : "A") + token[1..];

        Assert.True(_service.IsValidToken(caseVariant));
        Assert.NotEqual(_service.HashToken(token), _service.HashToken(caseVariant));
    }

    [Fact]
    public void HashTokenRejectsMalformedInput()
    {
        Assert.Throws<ArgumentException>(() => _service.HashToken("not-a-token"));
    }
}
