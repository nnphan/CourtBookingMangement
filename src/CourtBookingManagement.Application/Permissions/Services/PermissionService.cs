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
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return Result.Failure<PagedResult<PermissionResponse>>(Error.FromException(exception));
        }
    }

    public async Task<Result<PermissionResponse>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return Result.Failure<PermissionResponse>(
                new Error("PERMISSION.INVALID_ID", "A valid permission id is required."));
        }

        try
        {
            var permission = await repository.GetByIdAsync(id, cancellationToken);
            return permission is null
                ? Result.Failure<PermissionResponse>(
                    new Error("Permission.NotFound", "Permission not found."))
                : Result.Success(permission);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return Result.Failure<PermissionResponse>(Error.FromException(exception));
        }
    }

    public async Task<Result<PermissionResponse>> CreateAsync(
        CreatePermissionRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return Result.Failure<PermissionResponse>(
                new Error("PERMISSION.INVALID_REQUEST", "Request cannot be null."));
        }

        var code = request.Code?.Trim().ToLowerInvariant();
        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(code) || code.Length > 100)
        {
            return Result.Failure<PermissionResponse>(
                new Error("PERMISSION.INVALID_CODE", "Code is required and must be at most 100 characters."));
        }

        if (string.IsNullOrWhiteSpace(name) || name.Length > 150)
        {
            return Result.Failure<PermissionResponse>(
                new Error("PERMISSION.INVALID_NAME", "Name is required and must be at most 150 characters."));
        }

        var normalizedRequest = new CreatePermissionRequest
        {
            Code = code,
            Name = name,
            Description = NormalizeDescription(request.Description)
        };

        try
        {
            if (await repository.ExistsByCodeAsync(code, cancellationToken))
            {
                return Result.Failure<PermissionResponse>(PermissionCodeAlreadyExists);
            }

            var result = await repository.CreateAsync(normalizedRequest, cancellationToken);
            return result.Status switch
            {
                PermissionWriteStatus.Success when result.Permission is not null =>
                    Result.Success(result.Permission),
                PermissionWriteStatus.CodeAlreadyExists =>
                    Result.Failure<PermissionResponse>(PermissionCodeAlreadyExists),
                _ => Result.Failure<PermissionResponse>(Error.FromException(
                    new InvalidOperationException("Permission creation returned an invalid result.")))
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return Result.Failure<PermissionResponse>(Error.FromException(exception));
        }
    }

    public async Task<Result<PermissionResponse>> UpdateAsync(
        Guid id,
        UpdatePermissionRequest request,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return Result.Failure<PermissionResponse>(
                new Error("PERMISSION.INVALID_ID", "A valid permission id is required."));
        }

        if (request is null)
        {
            return Result.Failure<PermissionResponse>(
                new Error("PERMISSION.INVALID_REQUEST", "Request cannot be null."));
        }

        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 150)
        {
            return Result.Failure<PermissionResponse>(
                new Error("PERMISSION.INVALID_NAME", "Name is required and must be at most 150 characters."));
        }

        var normalizedRequest = new UpdatePermissionRequest
        {
            Name = name,
            Description = NormalizeDescription(request.Description)
        };

        try
        {
            var result = await repository.UpdateAsync(id, normalizedRequest, cancellationToken);
            return result.Status switch
            {
                PermissionWriteStatus.Success when result.Permission is not null =>
                    Result.Success(result.Permission),
                PermissionWriteStatus.NotFound =>
                    Result.Failure<PermissionResponse>(
                        new Error("Permission.NotFound", "Permission not found.")),
                PermissionWriteStatus.CodeAlreadyExists =>
                    Result.Failure<PermissionResponse>(PermissionCodeAlreadyExists),
                _ => Result.Failure<PermissionResponse>(Error.FromException(
                    new InvalidOperationException("Permission update returned an invalid result.")))
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return Result.Failure<PermissionResponse>(Error.FromException(exception));
        }
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return Result.Failure(
                new Error("PERMISSION.INVALID_ID", "A valid permission id is required."));
        }

        try
        {
            return (await repository.DeleteAsync(id, cancellationToken)) switch
            {
                PermissionDeleteStatus.Deleted => Result.Success(),
                PermissionDeleteStatus.NotFound => Result.Failure(
                    new Error("Permission.NotFound", "Permission not found.")),
                PermissionDeleteStatus.InUse => Result.Failure(
                    new Error("Permission.InUse", "Permission is assigned to one or more roles.")),
                _ => Result.Failure(Error.FromException(
                    new InvalidOperationException("Permission deletion returned an invalid result.")))
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return Result.Failure(Error.FromException(exception));
        }
    }

    private static readonly Error PermissionCodeAlreadyExists =
        new("Permission.CodeAlreadyExists", "A permission with this code already exists.");

    private static string? NormalizeDescription(string? description) =>
        string.IsNullOrWhiteSpace(description) ? null : description.Trim();

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