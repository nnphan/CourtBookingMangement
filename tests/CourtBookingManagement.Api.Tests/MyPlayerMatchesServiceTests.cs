using System.Reflection;
using CourtBookingManagement.Application.Matching.Models;
using CourtBookingManagement.Application.Matching.Models.Requests;
using CourtBookingManagement.Application.Matching.Models.Responses;
using CourtBookingManagement.Application.Matching.Repositories;
using CourtBookingManagement.Application.Matching.Services;
using CourtBookingManagement.Domain.Abstractions;
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
    public async Task Hosted_propagates_repository_cancellation()
    {
        var (service, recorder) = CreateService();
        recorder.ExceptionToThrow = new OperationCanceledException();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.GetHostedMatchesAsync(new GetHostedMatchesRequest(), Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task Join_requests_uses_current_host_and_normalizes_status()
    {
        var (service, recorder) = CreateService();
        var matchId = Guid.NewGuid();
        var hostId = Guid.NewGuid();
        recorder.HostId = hostId;

        var result = await service.GetJoinRequestsAsync(
            matchId,
            new GetPlayerMatchJoinRequestsRequest { Status = " pending " },
            hostId,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
        Assert.Equal(matchId, recorder.MatchId);
        Assert.Equal("PENDING", recorder.Status);
    }

    [Fact]
    public async Task Join_requests_returns_all_statuses_when_status_is_omitted()
    {
        var (service, recorder) = CreateService();
        recorder.HostId = Guid.NewGuid();

        var result = await service.GetJoinRequestsAsync(
            Guid.NewGuid(),
            new GetPlayerMatchJoinRequestsRequest(),
            recorder.HostId.Value,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(recorder.Status);
    }

    [Fact]
    public async Task Join_requests_rejects_non_host_without_loading_requests()
    {
        var (service, recorder) = CreateService();
        recorder.HostId = Guid.NewGuid();

        var result = await service.GetJoinRequestsAsync(
            Guid.NewGuid(),
            new GetPlayerMatchJoinRequestsRequest(),
            Guid.NewGuid(),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("PLAYER_MATCH.NOT_HOST", result.Error.Code);
        Assert.False(recorder.JoinRequestsLoaded);
    }

    [Fact]
    public async Task Join_requests_returns_not_found_for_missing_match()
    {
        var (service, recorder) = CreateService();

        var result = await service.GetJoinRequestsAsync(
            Guid.NewGuid(),
            new GetPlayerMatchJoinRequestsRequest(),
            Guid.NewGuid(),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("PLAYER_MATCH.NOT_FOUND", result.Error.Code);
        Assert.False(recorder.JoinRequestsLoaded);
    }

    [Fact]
    public async Task Join_requests_rejects_unknown_status_before_database_access()
    {
        var (service, recorder) = CreateService();

        var result = await service.GetJoinRequestsAsync(
            Guid.NewGuid(),
            new GetPlayerMatchJoinRequestsRequest { Status = "UNKNOWN" },
            Guid.NewGuid(),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("PLAYER_MATCH.INVALID_REQUEST_STATUS", result.Error.Code);
        Assert.Null(recorder.MatchId);
    }

    [Fact]
    public async Task Join_requests_propagates_repository_cancellation()
    {
        var (service, recorder) = CreateService();
        recorder.HostId = Guid.NewGuid();
        recorder.ExceptionToThrow = new OperationCanceledException();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.GetJoinRequestsAsync(
                Guid.NewGuid(),
                new GetPlayerMatchJoinRequestsRequest(),
                recorder.HostId.Value,
                CancellationToken.None));
    }

    [Fact]
    public void FromException_does_not_expose_internal_exception_message()
    {
        var error = Error.FromException(new InvalidOperationException("database credentials"));

        Assert.Equal("An unexpected error occurred.", error.Description);
    }

    [Fact]
    public async Task Join_rejects_the_host_as_a_requesting_participant()
    {
        var (service, recorder) = CreateService();
        var hostId = Guid.NewGuid();
        var match = new PlayerMatchDetailResponse
        {
            Id = Guid.NewGuid(),
            CreatedBy = hostId,
            MatchDate = DateOnly.FromDateTime(DateTime.UtcNow),
            StartTime = TimeOnly.MaxValue,
            Status = "OPEN",
            CurrentPlayers = 1,
            MaxPlayers = 4
        };
        recorder.Match = match;

        var result = await service.JoinMatchAsync(match.Id, hostId, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("PLAYER_MATCH.HOST_ALREADY_PARTICIPANT", result.Error.Code);
    }

    [Fact]
    public async Task Join_rejects_matches_whose_start_time_has_passed_today()
    {
        var (service, recorder) = CreateService();
        var utcNow = DateTime.UtcNow;
        var match = new PlayerMatchDetailResponse
        {
            Id = Guid.NewGuid(),
            CreatedBy = Guid.NewGuid(),
            MatchDate = DateOnly.FromDateTime(utcNow),
            StartTime = TimeOnly.MinValue,
            Status = "OPEN",
            CurrentPlayers = 1,
            MaxPlayers = 4
        };
        recorder.Match = match;

        var result = await service.JoinMatchAsync(match.Id, Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("PLAYER_MATCH.MATCH_EXPIRED", result.Error.Code);
    }

    [Fact]
    public async Task Approve_rejects_a_non_host_before_opening_a_transaction()
    {
        var (service, recorder) = CreateService();
        var match = new PlayerMatchDetailResponse
        {
            Id = Guid.NewGuid(),
            CreatedBy = Guid.NewGuid(),
            MatchDate = DateOnly.FromDateTime(DateTime.UtcNow),
            StartTime = TimeOnly.MaxValue,
            Status = "OPEN",
            CurrentPlayers = 1,
            MaxPlayers = 4
        };
        recorder.Match = match;

        var result = await service.ApproveJoinRequestAsync(
            match.Id,
            Guid.NewGuid(),
            Guid.NewGuid(),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("PLAYER_MATCH.NOT_HOST", result.Error.Code);
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

        public Exception? ExceptionToThrow { get; set; }

        public PlayerMatchDetailResponse? Match { get; set; }

        public Guid? HostId { get; set; }

        public Guid? MatchId { get; private set; }

        public string? Status { get; private set; }

        public bool JoinRequestsLoaded { get; private set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod!.Name == nameof(IPlayerMatchRepository.GetMatchHostIdAsync))
            {
                MatchId = (Guid)args![0]!;
                return Task.FromResult(HostId);
            }

            if (targetMethod.Name == nameof(IPlayerMatchRepository.GetJoinRequestsAsync))
            {
                MatchId = (Guid)args![0]!;
                Status = (string?)args[1];
                JoinRequestsLoaded = true;
                if (ExceptionToThrow is not null)
                {
                    throw ExceptionToThrow;
                }

                return Task.FromResult<IReadOnlyList<PlayerMatchJoinRequestResponse>>([]);
            }

            UserId = (Guid)args![0]!;
            Request = args[1];

            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            return targetMethod!.Name switch
            {
                nameof(IPlayerMatchRepository.GetMatchByIdAsync) => Task.FromResult(Match),
                nameof(IPlayerMatchRepository.GetHostedMatchesAsync) => Task.FromResult(
                    PagedResult<HostedPlayerMatchResponse>.Create([], 1, 10, 0)),
                nameof(IPlayerMatchRepository.GetParticipatingMatchesAsync) => Task.FromResult(
                    PagedResult<ParticipatingPlayerMatchResponse>.Create([], 1, 10, 0)),
                _ => throw new NotSupportedException()
            };
        }
    }
}
