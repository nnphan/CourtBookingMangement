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
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return Result.Failure<PagedResult<RoleResponse>>(Error.FromException(exception));
        }
    }

    public async Task<Result<RoleResponse>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return Result.Failure<RoleResponse>(
                new Error("ROLE.INVALID_ID", "A valid role id is required."));
        }

        try
        {
            var role = await repository.GetByIdWithPermissionsAsync(id, cancellationToken);
            return role is null
                ? Result.Failure<RoleResponse>(RoleNotFound)
                : Result.Success(role);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return Result.Failure<RoleResponse>(Error.FromException(exception));
        }
    }

    public async Task<Result<RoleResponse>> CreateAsync(
        CreateRoleRequest request,
        Guid? actorId,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return Result.Failure<RoleResponse>(
                new Error("ROLE.INVALID_REQUEST", "Request cannot be null."));
        }

        var code = request.Code?.Trim().ToUpperInvariant();
        var name = request.Name?.Trim();
        var validationError = ValidateFields(code, name);
        if (validationError is not null)
        {
            return Result.Failure<RoleResponse>(validationError);
        }

        var permissionIds = request.PermissionIds;
        var permissionError = ValidatePermissionIds(permissionIds);
        if (permissionError is not null)
        {
            return Result.Failure<RoleResponse>(permissionError);
        }

        var normalizedRequest = new CreateRoleRequest
        {
            Code = code!,
            Name = name!,
            Description = NormalizeDescription(request.Description),
            IsActive = request.IsActive,
            PermissionIds = permissionIds.ToArray()
        };

        try
        {
            if (await repository.ExistsByCodeAsync(code!, null, cancellationToken))
            {
                return Result.Failure<RoleResponse>(RoleCodeAlreadyExists);
            }

            var result = await repository.CreateAsync(normalizedRequest, actorId, cancellationToken);
            return result.Status switch
            {
                RoleWriteStatus.Success when result.Role is not null =>
                    Result.Success(result.Role),
                RoleWriteStatus.CodeAlreadyExists =>
                    Result.Failure<RoleResponse>(RoleCodeAlreadyExists),
                RoleWriteStatus.PermissionsNotFound =>
                    Result.Failure<RoleResponse>(PermissionsNotFound),
                _ => Result.Failure<RoleResponse>(Error.FromException(
                    new InvalidOperationException("Role creation returned an invalid result.")))
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return Result.Failure<RoleResponse>(Error.FromException(exception));
        }
    }

    public async Task<Result<RoleResponse>> UpdateAsync(
        Guid id,
        UpdateRoleRequest request,
        Guid? actorId,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return Result.Failure<RoleResponse>(
                new Error("ROLE.INVALID_ID", "A valid role id is required."));
        }

        if (request is null)
        {
            return Result.Failure<RoleResponse>(
                new Error("ROLE.INVALID_REQUEST", "Request cannot be null."));
        }

        var code = request.Code?.Trim().ToUpperInvariant();
        var name = request.Name?.Trim();
        var validationError = ValidateFields(code, name);
        if (validationError is not null)
        {
            return Result.Failure<RoleResponse>(validationError);
        }

        var permissionIds = request.PermissionIds;
        var permissionError = ValidatePermissionIds(permissionIds);
        if (permissionError is not null)
        {
            return Result.Failure<RoleResponse>(permissionError);
        }

        var normalizedRequest = new UpdateRoleRequest
        {
            Code = code!,
            Name = name!,
            Description = NormalizeDescription(request.Description),
            IsActive = request.IsActive,
            PermissionIds = permissionIds.ToArray()
        };

        try
        {
            if (await repository.ExistsByCodeAsync(code!, id, cancellationToken))
            {
                return Result.Failure<RoleResponse>(RoleCodeAlreadyExists);
            }

            var result = await repository.UpdateAsync(id, normalizedRequest, actorId, cancellationToken);
            return result.Status switch
            {
                RoleWriteStatus.Success when result.Role is not null =>
                    Result.Success(result.Role),
                RoleWriteStatus.NotFound =>
                    Result.Failure<RoleResponse>(RoleNotFound),
                RoleWriteStatus.CodeAlreadyExists =>
                    Result.Failure<RoleResponse>(RoleCodeAlreadyExists),
                RoleWriteStatus.PermissionsNotFound =>
                    Result.Failure<RoleResponse>(PermissionsNotFound),
                _ => Result.Failure<RoleResponse>(Error.FromException(
                    new InvalidOperationException("Role update returned an invalid result.")))
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return Result.Failure<RoleResponse>(Error.FromException(exception));
        }
    }

    public async Task<Result> DeleteAsync(
        Guid id,
        Guid? actorId,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return Result.Failure(
                new Error("ROLE.INVALID_ID", "A valid role id is required."));
        }

        try
        {
            return (await repository.DeleteAsync(id, actorId, cancellationToken)) switch
            {
                RoleDeleteStatus.Deleted => Result.Success(),
                RoleDeleteStatus.NotFound => Result.Failure(RoleNotFound),
                RoleDeleteStatus.InUse => Result.Failure(RoleInUse),
                _ => Result.Failure(Error.FromException(
                    new InvalidOperationException("Role deletion returned an invalid result.")))
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

    private static readonly Error RoleNotFound = new("Role.NotFound", "Role not found.");

    private static readonly Error RoleCodeAlreadyExists =
        new("Role.CodeAlreadyExists", "A role with this code already exists.");

    private static readonly Error RoleInUse =
        new("Role.InUse", "Role is assigned to one or more active users.");

    private static readonly Error PermissionsNotFound =
        new("Permission.NotFound", "One or more permissions were not found.");

    private static Error? ValidateFields(string? code, string? name)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length > 50)
        {
            return new Error("ROLE.INVALID_CODE", "Code is required and must be at most 50 characters.");
        }

        if (string.IsNullOrWhiteSpace(name) || name.Length > 100)
        {
            return new Error("ROLE.INVALID_NAME", "Name is required and must be at most 100 characters.");
        }

        return null;
    }

    private static Error? ValidatePermissionIds(IReadOnlyCollection<Guid>? permissionIds)
    {
        if (permissionIds is null)
        {
            return new Error("ROLE.INVALID_PERMISSION_IDS", "PermissionIds cannot be null.");
        }

        if (permissionIds.Any(id => id == Guid.Empty))
        {
            return new Error("ROLE.INVALID_PERMISSION_IDS", "PermissionIds cannot contain an empty id.");
        }

        if (permissionIds.Count != permissionIds.Distinct().Count())
        {
            return new Error("ROLE.DUPLICATE_PERMISSION_IDS", "PermissionIds cannot contain duplicates.");
        }

        return null;
    }

    private static string? NormalizeDescription(string? description) =>
        string.IsNullOrWhiteSpace(description) ? null : description.Trim();

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