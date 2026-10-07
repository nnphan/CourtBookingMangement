using CourtBookingManagement.Application.Matching.Models;
using CourtBookingManagement.Application.Roles.Models.Requests;
using CourtBookingManagement.Application.Roles.Models.Responses;
using CourtBookingManagement.Application.Roles.Repositories;
using CourtBookingManagement.Domain.Abstractions;

namespace CourtBookingManagement.Application.Roles.Services;

public sealed class RoleService(IRoleRepository repository) : IRoleService
{
    public async Task<Result<PagedResult<RoleResponse>>> SearchAsync(
        RoleSearchRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return Result.Failure<PagedResult<RoleResponse>>(
                new Error("ROLE.INVALID_REQUEST", "Request cannot be null."));
        }

        if (request.Page < 1)
        {
            return Result.Failure<PagedResult<RoleResponse>>(
                new Error("ROLE.INVALID_PAGE", "Page must be greater than or equal to 1."));
        }

        if (request.PageSize is < 1 or > 100)
        {
            return Result.Failure<PagedResult<RoleResponse>>(
                new Error("ROLE.INVALID_PAGE_SIZE", "PageSize must be between 1 and 100."));
        }

        var sortBy = NormalizeSort(request.SortBy);
        if (sortBy is null)
        {
            return Result.Failure<PagedResult<RoleResponse>>(
                new Error("ROLE.INVALID_SORT", "SortBy must be latest, oldest, name_asc, name_desc, code_asc, or code_desc."));
        }

        var normalizedRequest = new RoleSearchRequest
        {
            Keyword = string.IsNullOrWhiteSpace(request.Keyword) ? null : request.Keyword.Trim(),
            IsActive = request.IsActive,
            SortBy = sortBy,
            Page = request.Page,
            PageSize = request.PageSize
        };

        try
        {
            return Result.Success(await repository.SearchAsync(normalizedRequest, cancellationToken));
        }
        catch (Exception exception)
        {
            return Result.Failure<PagedResult<RoleResponse>>(Error.FromException(exception));
        }
    }

    private static string? NormalizeSort(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "latest";
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "latest" => "latest",
            "oldest" => "oldest",
            "name_asc" => "name_asc",
            "name_desc" => "name_desc",
            "code_asc" => "code_asc",
            "code_desc" => "code_desc",
            _ => null
        };
    }
}