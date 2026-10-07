using CourtBookingManagement.Application.Matching.Models;
using CourtBookingManagement.Application.Permissions.Models.Requests;
using CourtBookingManagement.Application.Permissions.Models.Responses;
using CourtBookingManagement.Application.Permissions.Repositories;
using CourtBookingManagement.Domain.Abstractions;

namespace CourtBookingManagement.Application.Permissions.Services;

public sealed class PermissionService(IPermissionRepository repository) : IPermissionService
{
    public async Task<Result<PagedResult<PermissionResponse>>> SearchAsync(
        PermissionSearchRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return Result.Failure<PagedResult<PermissionResponse>>(
                new Error("PERMISSION.INVALID_REQUEST", "Request cannot be null."));
        }

        if (request.PageNumber < 1)
        {
            return Result.Failure<PagedResult<PermissionResponse>>(
                new Error("PERMISSION.INVALID_PAGE", "PageNumber must be greater than or equal to 1."));
        }

        if (request.PageSize is < 1 or > 100)
        {
            return Result.Failure<PagedResult<PermissionResponse>>(
                new Error("PERMISSION.INVALID_PAGE_SIZE", "PageSize must be between 1 and 100."));
        }

        var sortBy = NormalizeSort(request.SortBy);
        if (sortBy is null)
        {
            return Result.Failure<PagedResult<PermissionResponse>>(
                new Error("PERMISSION.INVALID_SORT", "SortBy must be code_asc, code_desc, name_asc, name_desc, latest, or oldest."));
        }

        var normalizedRequest = new PermissionSearchRequest
        {
            Keyword = string.IsNullOrWhiteSpace(request.Keyword) ? null : request.Keyword.Trim(),
            RoleId = request.RoleId,
            SortBy = sortBy,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };

        try
        {
            return Result.Success(await repository.SearchAsync(normalizedRequest, cancellationToken));
        }
        catch (Exception exception)
        {
            return Result.Failure<PagedResult<PermissionResponse>>(Error.FromException(exception));
        }
    }

    private static string? NormalizeSort(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "code_asc";
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "code_asc" => "code_asc",
            "code_desc" => "code_desc",
            "name_asc" => "name_asc",
            "name_desc" => "name_desc",
            "latest" => "latest",
            "oldest" => "oldest",
            _ => null
        };
    }
}