using CourtBookingManagement.Application.Common.Constants;
using CourtBookingManagement.Application.Matching.Models.Requests;
using CourtBookingManagement.Application.Matching.Validators;
using Xunit;

namespace CourtBookingManagement.Api.Tests;

public sealed class CreatePlayerMatchRequestValidatorTests
{
    [Theory]
    [InlineData(SkillLevels.Beginner)]
    [InlineData(SkillLevels.Intermediate)]
    [InlineData(SkillLevels.Advanced)]
    [InlineData(SkillLevels.Professional)]
    public async Task Validate_accepts_supported_skill_levels(string skillLevel)
    {
        var validator = new CreatePlayerMatchRequestValidator();

        var result = await validator.ValidateAsync(CreateRequest(skillLevel));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validate_rejects_empty_booking_id()
    {
        var request = CreateRequest(SkillLevels.Beginner);
        request.BookingId = Guid.Empty;
        var validator = new CreatePlayerMatchRequestValidator();

        var result = await validator.ValidateAsync(request);

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreatePlayerMatchRequest.BookingId));
    }

    [Fact]
    public void CurrentPlayers_defaults_to_the_host()
    {
        Assert.Equal(1, new CreatePlayerMatchRequest().CurrentPlayers);
    }

    [Theory]
    [InlineData(1, 4, true)]
    [InlineData(4, 4, true)]
    [InlineData(0, 4, false)]
    [InlineData(-1, 4, false)]
    [InlineData(5, 4, false)]
    public async Task Validate_enforces_current_player_bounds(int currentPlayers, int maxPlayers, bool expectedValid)
    {
        var request = CreateRequest(SkillLevels.Beginner);
        request.CurrentPlayers = currentPlayers;
        request.MaxPlayers = maxPlayers;
        var validator = new CreatePlayerMatchRequestValidator();

        var result = await validator.ValidateAsync(request);

        Assert.Equal(expectedValid, result.IsValid);
        if (!expectedValid)
        {
            Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreatePlayerMatchRequest.CurrentPlayers));
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("beginner")]
    [InlineData("PRO")]
    [InlineData("EXPERT")]
    public async Task Validate_rejects_unsupported_skill_levels(string skillLevel)
    {
        var validator = new CreatePlayerMatchRequestValidator();

        var result = await validator.ValidateAsync(CreateRequest(skillLevel));

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreatePlayerMatchRequest.SkillLevel));
    }

    [Theory]
    [InlineData(SkillLevels.Beginner)]
    [InlineData(SkillLevels.Intermediate)]
    [InlineData(SkillLevels.Advanced)]
    [InlineData(SkillLevels.Professional)]
    public async Task Search_validation_accepts_supported_skill_levels(string skillLevel)
    {
        var validator = new PlayerMatchSearchRequestValidator();

        var result = await validator.ValidateAsync(new PlayerMatchSearchRequest { SkillLevel = skillLevel });

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("beginner")]
    [InlineData("PRO")]
    [InlineData("EXPERT")]
    public async Task Search_validation_rejects_unsupported_skill_levels(string skillLevel)
    {
        var validator = new PlayerMatchSearchRequestValidator();

        var result = await validator.ValidateAsync(new PlayerMatchSearchRequest { SkillLevel = skillLevel });

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(PlayerMatchSearchRequest.SkillLevel));
    }

    private static CreatePlayerMatchRequest CreateRequest(string skillLevel) => new()
    {
        BookingId = Guid.NewGuid(),
        BranchId = Guid.NewGuid(),
        Title = "Weekend badminton",
        MatchDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
        StartTime = new TimeOnly(10, 0),
        EndTime = new TimeOnly(12, 0),
        SkillLevel = skillLevel,
        MaxPlayers = 4,
        CurrentPlayers = 1
    };
}