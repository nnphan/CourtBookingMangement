using CourtBookingManagement.Application.Matching.Models;
using CourtBookingManagement.Application.Matching.Models.Requests;
using CourtBookingManagement.Application.Matching.Models.Responses;
using CourtBookingManagement.Application.Matching.Repositories;
using CourtBookingManagement.Domain.Abstractions;

namespace CourtBookingManagement.Application.Matching.Services;

public sealed class MyMatchService(IMyMatchRepository repository) : IMyMatchService
{
    private static readonly HashSet<string> MatchStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "OPEN",
        "FULL",
        "CANCELLED",
        "COMPLETED"
    };

    public async Task<Result<PagedResult<MyMatchResponse>>> GetMyMatchesAsync(
        MyMatchSearchRequest request,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return Result.Failure<PagedResult<MyMatchResponse>>(
                new Error("PLAYER_MATCH.INVALID_REQUEST", "Request cannot be null."));
        }

        if (currentUserId == Guid.Empty)
        {
            return Result.Failure<PagedResult<MyMatchResponse>>(
                new Error("PLAYER_MATCH.INVALID_USER", "The current user is invalid."));
        }

        var type = NormalizeType(request.Type);
        if (type is null)
        {
            return Result.Failure<PagedResult<MyMatchResponse>>(
                new Error("PLAYER_MATCH.INVALID_TYPE", "Type must be Hosted, Joined, Pending, or All."));
        }

        var status = string.IsNullOrWhiteSpace(request.Status) ? null : request.Status.Trim().ToUpperInvariant();
        if (status is not null && !MatchStatuses.Contains(status))
        {
            return Result.Failure<PagedResult<MyMatchResponse>>(
                new Error("PLAYER_MATCH.INVALID_STATUS", "Status must be OPEN, FULL, CANCELLED, or COMPLETED."));
        }

        var sortBy = NormalizeSort(request.SortBy);
        if (sortBy is null)
        {
            return Result.Failure<PagedResult<MyMatchResponse>>(
                new Error("PLAYER_MATCH.INVALID_SORT", "SortBy must be latest, oldest, match_date_asc, or match_date_desc."));
        }

        var normalizedRequest = new MyMatchSearchRequest
        {
            Type = type,
            Status = status,
            MatchDate = request.MatchDate,
            SortBy = sortBy,
            Page = request.Page <= 0 ? 1 : request.Page,
            PageSize = request.PageSize <= 0 ? 10 : Math.Min(request.PageSize, 50)
        };

        try
        {
            var matches = await repository.GetMyMatchesAsync(currentUserId, normalizedRequest, cancellationToken);
            return Result.Success(matches);
        }
        catch (Exception exception)
        {
            return Result.Failure<PagedResult<MyMatchResponse>>(Error.FromException(exception));
        }
    }

    private static string? NormalizeType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "All";
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "hosted" => "Hosted",
            "joined" => "Joined",
            "pending" => "Pending",
            "all" => "All",
            _ => null
        };
    }

    private static string? NormalizeSort(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        null or "" => "latest",
        "latest" => "latest",
        "oldest" => "oldest",
        "match_date_asc" => "match_date_asc",
        "match_date_desc" => "match_date_desc",
        _ => null
    };
}