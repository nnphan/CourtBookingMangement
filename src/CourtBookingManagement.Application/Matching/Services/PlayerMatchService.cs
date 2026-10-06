using CourtBookingManagement.Application.Matching.Models;
using CourtBookingManagement.Application.Matching.Models.Requests;
using CourtBookingManagement.Application.Matching.Models.Responses;
using CourtBookingManagement.Application.Matching.Repositories;
using CourtBookingManagement.Domain.Abstractions;

namespace CourtBookingManagement.Application.Matching.Services;

public sealed class PlayerMatchService(IPlayerMatchRepository repository) : IPlayerMatchService
{
    private static readonly Error InvalidMatchId = new("PLAYER_MATCH.INVALID_ID", "A valid player match id is required.");

    private static readonly Error MatchNotFound = new("PLAYER_MATCH.NOT_FOUND", "The player match was not found.");

    public async Task<Result<PagedResult<PlayerMatchResponse>>> GetMatchesAsync(
        PlayerMatchSearchRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return Result.Failure<PagedResult<PlayerMatchResponse>>(new Error("INVALID_REQUEST", "Request cannot be null."));
        }

        var normalizedRequest = new PlayerMatchSearchRequest
        {
            Keyword = NormalizeText(request.Keyword),
            MatchDate = request.MatchDate,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            SkillLevel = NormalizeText(request.SkillLevel),
            City = NormalizeText(request.City),
            District = NormalizeText(request.District),
            SortBy = NormalizeSort(request.SortBy),
            Page = request.Page <= 0 ? 1 : request.Page,
            PageSize = request.PageSize <= 0 ? 9 : Math.Min(request.PageSize, 50)
        };

        try
        {
            var matches = await repository.SearchAsync(normalizedRequest, cancellationToken);
            return Result.Success(matches);
        }
        catch (Exception exception)
        {
            return Result.Failure<PagedResult<PlayerMatchResponse>>(Error.FromException(exception));
        }
    }

    public async Task<Result<PlayerMatchDetailResponse>> GetMatchByIdAsync(
        Guid matchId,
        CancellationToken cancellationToken)
    {
        if (matchId == Guid.Empty)
        {
            return Result.Failure<PlayerMatchDetailResponse>(InvalidMatchId);
        }

        try
        {
            var match = await repository.GetByIdAsync(matchId, cancellationToken);
            if (match is null)
            {
                return Result.Failure<PlayerMatchDetailResponse>(MatchNotFound);
            }

            match.RemainingSlots = Math.Max(match.MaxPlayers - match.CurrentPlayers, 0);
            match.CanJoin = string.Equals(match.Status, "OPEN", StringComparison.OrdinalIgnoreCase)
                && match.CurrentPlayers < match.MaxPlayers;
            match.IsFull = match.CurrentPlayers >= match.MaxPlayers;
            match.ParticipantCount = match.Participants.Count;

            return Result.Success(match);
        }
        catch (Exception exception)
        {
            return Result.Failure<PlayerMatchDetailResponse>(Error.FromException(exception));
        }
    }

    private static string? NormalizeText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string NormalizeSort(string? value)
    {
        var normalized = value?.Trim();

        if (string.IsNullOrWhiteSpace(normalized))
        {
            return "latest";
        }

        return normalized.ToLowerInvariant() switch
        {
            "oldest" => "oldest",
            "match_date_asc" => "match_date_asc",
            "match_date_desc" => "match_date_desc",
            _ => "latest"
        };
    }
}
