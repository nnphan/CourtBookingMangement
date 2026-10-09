using CourtBookingManagement.Application.Courts.Models.Requests;
using CourtBookingManagement.Application.Courts.Models.Responses;
using CourtBookingManagement.Application.Courts.Repositories;
using CourtBookingManagement.Application.Matching.Models;
using CourtBookingManagement.Domain.Abstractions;

namespace CourtBookingManagement.Application.Courts.Services;

public sealed class CourtService(ICourtRepository repository) : ICourtService
{
    private const int MaxNameLength = 100;

    private static readonly string[] AllowedStatuses = ["active", "inactive", "blocked"];

    private static readonly Error InvalidCourtId = new("Court.InvalidId", "A valid court id is required.");

    private static readonly Error InvalidUser = new("Court.InvalidUser", "The current user is invalid.");

    private static readonly Error CourtNotFound = new("Court.NotFound", "Court not found.");

    private static readonly Error DuplicateName = new("Court.DuplicateName", "Court name already exists in this branch.");

    private static readonly Error DuplicateNumber = new("Court.DuplicateNumber", "Court number already exists in this branch.");

    public async Task<Result<PagedResult<CourtListItemResponse>>> SearchAsync(
        CourtSearchRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return Result.Failure<PagedResult<CourtListItemResponse>>(
                new Error("Court.InvalidRequest", "Request cannot be null."));
        }

        if (request.PageNumber < 1)
        {
            return Result.Failure<PagedResult<CourtListItemResponse>>(
                new Error("Court.InvalidPage", "PageNumber must be greater than or equal to 1."));
        }

        if (request.PageSize is < 1 or > 100)
        {
            return Result.Failure<PagedResult<CourtListItemResponse>>(
                new Error("Court.InvalidPageSize", "PageSize must be between 1 and 100."));
        }

        var sortBy = NormalizeSort(request.SortBy);
        if (sortBy is null)
        {
            return Result.Failure<PagedResult<CourtListItemResponse>>(
                new Error("Court.InvalidSort", "SortBy must be latest, oldest, name_asc, or name_desc."));
        }

        var normalizedRequest = new CourtSearchRequest
        {
            Keyword = string.IsNullOrWhiteSpace(request.Keyword) ? null : request.Keyword.Trim(),
            BranchId = request.BranchId,
            IsActive = request.IsActive,
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
            return Result.Failure<PagedResult<CourtListItemResponse>>(Error.FromException(exception));
        }
    }

    public async Task<Result<CourtDetailResponse>> GetByIdAsync(
        Guid courtId,
        CancellationToken cancellationToken)
    {
        if (courtId == Guid.Empty)
        {
            return Result.Failure<CourtDetailResponse>(InvalidCourtId);
        }

        try
        {
            var court = await repository.GetByIdAsync(courtId, cancellationToken);
            return court is null
                ? Result.Failure<CourtDetailResponse>(CourtNotFound)
                : Result.Success(court);
        }
        catch (Exception exception)
        {
            return Result.Failure<CourtDetailResponse>(Error.FromException(exception));
        }
    }

    public async Task<Result<CourtDetailResponse>> CreateAsync(
        CreateCourtRequestDTO request,
        Guid createdBy,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return Result.Failure<CourtDetailResponse>(new Error("Court.InvalidRequest", "Request cannot be null."));
        }

        if (createdBy == Guid.Empty)
        {
            return Result.Failure<CourtDetailResponse>(InvalidUser);
        }

        if (request.BranchId == Guid.Empty)
        {
            return Result.Failure<CourtDetailResponse>(new Error("Court.InvalidBranch", "A valid branch id is required."));
        }

        var fieldError = ValidateFields(request.CourtNumber, request.Name, request.Status, request.CourtTypeId);
        if (fieldError is not null)
        {
            return Result.Failure<CourtDetailResponse>(fieldError);
        }

        var normalizedRequest = new CreateCourtRequestDTO
        {
            BranchId = request.BranchId,
            CourtNumber = request.CourtNumber,
            Name = NormalizeName(request.Name),
            CourtTypeId = request.CourtTypeId,
            Status = request.Status.Trim().ToLowerInvariant()
        };

        try
        {
            if (!await repository.BranchExistsAsync(normalizedRequest.BranchId, cancellationToken))
            {
                return Result.Failure<CourtDetailResponse>(new Error("Court.BranchNotFound", "Branch not found."));
            }

            var referenceError = await ValidateReferencesAsync(
                normalizedRequest.BranchId,
                normalizedRequest.CourtNumber,
                normalizedRequest.Name,
                normalizedRequest.CourtTypeId,
                excludeCourtId: null,
                cancellationToken);

            if (referenceError is not null)
            {
                return Result.Failure<CourtDetailResponse>(referenceError);
            }

            var courtId = await repository.CreateAsync(normalizedRequest, createdBy, cancellationToken);
            var court = await repository.GetByIdAsync(courtId, cancellationToken);

            return court is null
                ? Result.Failure<CourtDetailResponse>(CourtNotFound)
                : Result.Success(court);
        }
        catch (CourtNumberConflictException)
        {
            return Result.Failure<CourtDetailResponse>(DuplicateNumber);
        }
        catch (Exception exception)
        {
            return Result.Failure<CourtDetailResponse>(Error.FromException(exception));
        }
    }

    public async Task<Result<CourtDetailResponse>> UpdateAsync(
        Guid courtId,
        UpdateCourtRequest request,
        Guid updatedBy,
        CancellationToken cancellationToken)
    {
        if (courtId == Guid.Empty)
        {
            return Result.Failure<CourtDetailResponse>(InvalidCourtId);
        }

        if (request is null)
        {
            return Result.Failure<CourtDetailResponse>(new Error("Court.InvalidRequest", "Request cannot be null."));
        }

        if (updatedBy == Guid.Empty)
        {
            return Result.Failure<CourtDetailResponse>(InvalidUser);
        }

        var fieldError = ValidateFields(request.CourtNumber, request.Name, request.Status, request.CourtTypeId);
        if (fieldError is not null)
        {
            return Result.Failure<CourtDetailResponse>(fieldError);
        }

        var normalizedRequest = new UpdateCourtRequest
        {
            CourtNumber = request.CourtNumber,
            Name = NormalizeName(request.Name),
            CourtTypeId = request.CourtTypeId,
            Status = request.Status.Trim().ToLowerInvariant(),
            IsActive = request.IsActive
        };

        try
        {
            var existingCourt = await repository.GetByIdAsync(courtId, cancellationToken);
            if (existingCourt is null)
            {
                return Result.Failure<CourtDetailResponse>(CourtNotFound);
            }

            var referenceError = await ValidateReferencesAsync(
                existingCourt.BranchId,
                normalizedRequest.CourtNumber,
                normalizedRequest.Name,
                normalizedRequest.CourtTypeId,
                courtId,
                cancellationToken);

            if (referenceError is not null)
            {
                return Result.Failure<CourtDetailResponse>(referenceError);
            }

            if (!await repository.UpdateAsync(courtId, normalizedRequest, updatedBy, cancellationToken))
            {
                return Result.Failure<CourtDetailResponse>(CourtNotFound);
            }

            var court = await repository.GetByIdAsync(courtId, cancellationToken);
            return court is null
                ? Result.Failure<CourtDetailResponse>(CourtNotFound)
                : Result.Success(court);
        }
        catch (CourtNumberConflictException)
        {
            return Result.Failure<CourtDetailResponse>(DuplicateNumber);
        }
        catch (Exception exception)
        {
            return Result.Failure<CourtDetailResponse>(Error.FromException(exception));
        }
    }

    public async Task<Result> DeleteAsync(
        Guid courtId,
        Guid deletedBy,
        CancellationToken cancellationToken)
    {
        if (courtId == Guid.Empty)
        {
            return Result.Failure(InvalidCourtId);
        }

        if (deletedBy == Guid.Empty)
        {
            return Result.Failure(InvalidUser);
        }

        try
        {
            if (!await repository.ExistsAsync(courtId, cancellationToken))
            {
                return Result.Failure(CourtNotFound);
            }

            if (await repository.HasFutureBookingsAsync(courtId, cancellationToken))
            {
                return Result.Failure(new Error(
                    "Court.HasFutureBookings",
                    "Cannot delete court because future bookings exist."));
            }

            if (await repository.HasFutureMatchesAsync(courtId, cancellationToken))
            {
                return Result.Failure(new Error(
                    "Court.HasFutureMatches",
                    "Cannot delete court because upcoming matches exist."));
            }

            return await repository.DeleteAsync(courtId, deletedBy, cancellationToken)
                ? Result.Success()
                : Result.Failure(CourtNotFound);
        }
        catch (Exception exception)
        {
            return Result.Failure(Error.FromException(exception));
        }
    }

    private async Task<Error?> ValidateReferencesAsync(
        Guid branchId,
        int courtNumber,
        string? name,
        Guid? courtTypeId,
        Guid? excludeCourtId,
        CancellationToken cancellationToken)
    {
        if (courtTypeId is Guid typeId && !await repository.CourtTypeExistsAsync(typeId, cancellationToken))
        {
            return new Error("Court.InvalidCourtType", "Court type is invalid.");
        }

        if (await repository.CourtNumberExistsAsync(branchId, courtNumber, excludeCourtId, cancellationToken))
        {
            return DuplicateNumber;
        }

        if (name is not null && await repository.NameExistsAsync(branchId, name, excludeCourtId, cancellationToken))
        {
            return DuplicateName;
        }

        return null;
    }

    private static Error? ValidateFields(int courtNumber, string? name, string? status, Guid? courtTypeId)
    {
        if (courtNumber < 1)
        {
            return new Error("Court.InvalidCourtNumber", "CourtNumber must be greater than 0.");
        }

        if (name is not null && name.Trim().Length > MaxNameLength)
        {
            return new Error("Court.InvalidName", $"Name must not exceed {MaxNameLength} characters.");
        }

        if (string.IsNullOrWhiteSpace(status) || !AllowedStatuses.Contains(status.Trim().ToLowerInvariant()))
        {
            return new Error("Court.InvalidStatus", "Status must be active, inactive, or blocked.");
        }

        if (courtTypeId == Guid.Empty)
        {
            return new Error("Court.InvalidCourtType", "Court type is invalid.");
        }

        return null;
    }

    private static string? NormalizeName(string? name) =>
        string.IsNullOrWhiteSpace(name) ? null : name.Trim();

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
            _ => null
        };
    }
}
