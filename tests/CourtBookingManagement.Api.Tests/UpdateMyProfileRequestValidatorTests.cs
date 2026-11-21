using CourtBookingManagement.Application.Profiles.Models;
using CourtBookingManagement.Application.Profiles.Validators;
using Xunit;

namespace CourtBookingManagement.Api.Tests;

public sealed class UpdateMyProfileRequestValidatorTests
{
    private readonly UpdateMyProfileRequestValidator _validator = new();

    [Fact]
    public void ValidDisplayNameAndAvatarRemovalAreAccepted()
    {
        var result = _validator.Validate(new UpdateMyProfileRequest
        {
            DisplayName = "  Nguyen Van An  ",
            RemoveAvatar = true
        });

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("A")]
    [InlineData("  A  ")]
    public void ShortOrBlankDisplayNamesAreRejected(string displayName)
    {
        var result = _validator.Validate(new UpdateMyProfileRequest
        {
            DisplayName = displayName
        });

        Assert.Contains(result.Errors, error => error.PropertyName == "displayName");
    }

    [Fact]
    public void DisplayNameLongerThanCustomerColumnLimitIsRejected()
    {
        var result = _validator.Validate(new UpdateMyProfileRequest
        {
            DisplayName = new string('A', 151)
        });

        Assert.Contains(result.Errors, error => error.PropertyName == "displayName");
    }

    [Fact]
    public void RemoveAvatarCannotBeCombinedWithAvatarFileId()
    {
        var result = _validator.Validate(new UpdateMyProfileRequest
        {
            DisplayName = "Nguyen Van An",
            AvatarFileId = Guid.NewGuid(),
            RemoveAvatar = true
        });

        Assert.Contains(result.Errors, error => error.PropertyName == "avatarFileId");
    }
}
