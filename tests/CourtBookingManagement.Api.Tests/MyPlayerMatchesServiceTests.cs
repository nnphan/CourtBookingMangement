using System.Reflection;
using CourtBookingManagement.Application.Matching.Models;
using CourtBookingManagement.Application.Matching.Models.Requests;
using CourtBookingManagement.Application.Matching.Models.Responses;
using CourtBookingManagement.Application.Matching.Repositories;
using CourtBookingManagement.Application.Matching.Services;
using Xunit;

namespace CourtBookingManagement.Api.Tests;

public sealed class MyPlayerMatchesServiceTests
{
    [Fact]
    public async Task Hosted_uses_current_user_and_normalizes_paging()
    {
        var (service, recorder) = CreateService();
        var userId = Guid.NewGuid();

        var result = await service.GetHostedMatchesAsync(
            new GetHostedMatchesRequest { Status = " open ", Page = 0, PageSize = 500 }, userId, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(userId, recorder.UserId);
        var captured = Assert.IsType<GetHostedMatchesRequest>(recorder.Request);
        Assert.Equal("OPEN", captured.Status);
        Assert.Equal(1, captured.Page);
        Assert.Equal(50, captured.PageSize);
    }

    [Fact]
    public async Task Hosted_rejects_unknown_status()
    {
        var (service, recorder) = CreateService();

        var result = await service.GetHostedMatchesAsync(
            new GetHostedMatchesRequest { Status = "BOGUS" }, Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Null(recorder.Request);
    }

    [Fact]
    public async Task Hosted_rejects_empty_user()
    {
        var (service, _) = CreateService();

        var result = await service.GetHostedMatchesAsync(new GetHostedMatchesRequest(), Guid.Empty, CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task Participating_normalizes_filters_and_uses_current_user()
    {
        var (service, recorder) = CreateService();
        var userId = Guid.NewGuid();

        var result = await service.GetParticipatingMatchesAsync(
            new GetParticipatingMatchesRequest { ParticipationStatus = "pending", MatchStatus = "full" }, userId, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(userId, recorder.UserId);
        var captured = Assert.IsType<GetParticipatingMatchesRequest>(recorder.Request);
        Assert.Equal("PENDING", captured.ParticipationStatus);
        Assert.Equal("FULL", captured.MatchStatus);
    }

    [Theory]
    [InlineData("OPEN", null)]
    [InlineData(null, "BOGUS")]
    public async Task Participating_rejects_unknown_statuses(string? participationStatus, string? matchStatus)
    {
        var (service, recorder) = CreateService();

        var result = await service.GetParticipatingMatchesAsync(
            new GetParticipatingMatchesRequest { ParticipationStatus = participationStatus, MatchStatus = matchStatus },
            Guid.NewGuid(),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Null(recorder.Request);
    }

    private static (PlayerMatchService Service, RepositoryProxy Recorder) CreateService()
    {
        var repository = DispatchProxy.Create<IPlayerMatchRepository, RepositoryProxy>();
        var service = new PlayerMatchService(repository, null!, null!, null!, null!);
        return (service, (RepositoryProxy)(object)repository);
    }

    public class RepositoryProxy : DispatchProxy
    {
        public Guid? UserId { get; private set; }

        public object? Request { get; private set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            UserId = (Guid)args![0]!;
            Request = args[1];

            return targetMethod!.Name switch
            {
                nameof(IPlayerMatchRepository.GetHostedMatchesAsync) => Task.FromResult(
                    PagedResult<HostedPlayerMatchResponse>.Create([], 1, 10, 0)),
                nameof(IPlayerMatchRepository.GetParticipatingMatchesAsync) => Task.FromResult(
                    PagedResult<ParticipatingPlayerMatchResponse>.Create([], 1, 10, 0)),
                _ => throw new NotSupportedException()
            };
        }
    }
}
