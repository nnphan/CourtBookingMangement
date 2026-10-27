using CourtBookingManagement.Application.Matching.Models;
using CourtBookingManagement.Application.Owners.Models;
using CourtBookingManagement.Application.Owners.Repositories;
using CourtBookingManagement.Domain.Abstractions;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace CourtBookingManagement.Application.Owners.Services;

public sealed class OwnerService(
    IOwnerRepository repository,
    IValidator<CreateOwnerRequest> createValidator,
    IValidator<UpdateOwnerRequest> updateValidator,
    ILogger<OwnerService> logger) : IOwnerService
{
    private static readonly Error NotFound = new("Owner.NotFound", "Owner not found.");

    public async Task<Result<PagedResult<OwnerResponse>>> SearchAsync(
        OwnerSearchRequest request,
        CancellationToken cancellationToken)
    {
        if (request.PageNumber < 1)
        {
            return Result.Failure<PagedResult<OwnerResponse>>(
                new Error("Owner.InvalidPage", "PageNumber must be greater than or equal to 1."));
        }

        if (request.PageSize is < 1 or > 100)
        {
            return Result.Failure<PagedResult<OwnerResponse>>(
                new Error("Owner.InvalidPageSize", "PageSize must be between 1 and 100."));
        }

        var sortBy = (string.IsNullOrWhiteSpace(request.SortBy) ? "latest" : request.SortBy.Trim().ToLowerInvariant());
        if (sortBy is not ("latest" or "oldest" or "name_asc" or "name_desc"))
        {
            return Result.Failure<PagedResult<OwnerResponse>>(
                new Error("Owner.InvalidSort", "SortBy must be latest, oldest, name_asc, or name_desc."));
        }

        var normalized = new OwnerSearchRequest
        {
            Keyword = string.IsNullOrWhiteSpace(request.Keyword) ? null : request.Keyword.Trim(),
            IsActive = request.IsActive,
            SortBy = sortBy,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };

        try
        {
            return Result.Success(await repository.SearchAsync(normalized, cancellationToken));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Failed to search owners.");
            return Result.Failure<PagedResult<OwnerResponse>>(Error.FromException(exception));
        }
    }

    public async Task<Result<OwnerResponse>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return Result.Failure<OwnerResponse>(new Error("Owner.InvalidId", "A valid owner id is required."));
        }

        try
        {
            var owner = await repository.GetByIdAsync(id, cancellationToken);
            return owner is null ? Result.Failure<OwnerResponse>(NotFound) : Result.Success(owner);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Failed to get owner {OwnerId}.", id);
            return Result.Failure<OwnerResponse>(Error.FromException(exception));
        }
    }

    public async Task<Result<OwnerResponse>> CreateAsync(
        CreateOwnerRequest request,
        Guid? actorId,
        CancellationToken cancellationToken)
    {
        var validation = await createValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<OwnerResponse>(ToValidationError(validation));
        }

        var normalized = new CreateOwnerRequest
        {
            UserId = request.UserId,
            BusinessName = request.BusinessName.Trim(),
            TaxId = NormalizeTaxId(request.TaxId),
            IsActive = request.IsActive
        };

        try
        {
            var (status, id) = await repository.CreateAsync(normalized, actorId, cancellationToken);
            if (status != OwnerWriteStatus.Success)
            {
                return Result.Failure<OwnerResponse>(ToError(status));
            }

            var created = await repository.GetByIdAsync(id, cancellationToken);
            logger.LogInformation("Owner {OwnerId} created by {ActorId}.", id, actorId);
            return created is null ? Result.Failure<OwnerResponse>(NotFound) : Result.Success(created);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Failed to create owner for user {UserId}.", request.UserId);
            return Result.Failure<OwnerResponse>(Error.FromException(exception));
        }
    }

    public async Task<Result<OwnerResponse>> UpdateAsync(
        Guid id,
        UpdateOwnerRequest request,
        Guid? actorId,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return Result.Failure<OwnerResponse>(new Error("Owner.InvalidId", "A valid owner id is required."));
        }

        var validation = await updateValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<OwnerResponse>(ToValidationError(validation));
        }

        var normalized = new UpdateOwnerRequest
        {
            BusinessName = request.BusinessName.Trim(),
            TaxId = NormalizeTaxId(request.TaxId),
            IsActive = request.IsActive
        };

        try
        {
            var status = await repository.UpdateAsync(id, normalized, actorId, cancellationToken);
            if (status != OwnerWriteStatus.Success)
            {
                return Result.Failure<OwnerResponse>(ToError(status));
            }

            var updated = await repository.GetByIdAsync(id, cancellationToken);
            logger.LogInformation("Owner {OwnerId} updated by {ActorId}.", id, actorId);
            return updated is null ? Result.Failure<OwnerResponse>(NotFound) : Result.Success(updated);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Failed to update owner {OwnerId}.", id);
            return Result.Failure<OwnerResponse>(Error.FromException(exception));
        }
    }

    public async Task<Result> DeleteAsync(Guid id, Guid? actorId, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return Result.Failure(new Error("Owner.InvalidId", "A valid owner id is required."));
        }

        try
        {
            var status = await repository.DeleteAsync(id, actorId, cancellationToken);
            if (status != OwnerWriteStatus.Success)
            {
                return Result.Failure(ToError(status));
            }

            logger.LogInformation("Owner {OwnerId} deleted by {ActorId}.", id, actorId);
            return Result.Success();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Failed to delete owner {OwnerId}.", id);
            return Result.Failure(Error.FromException(exception));
        }
    }

    private static string? NormalizeTaxId(string? taxId) =>
        string.IsNullOrWhiteSpace(taxId) ? null : taxId.Trim();

    private static Error ToValidationError(FluentValidation.Results.ValidationResult validation) =>
        new("Error.Validation", string.Join(" ", validation.Errors.Select(e => e.ErrorMessage).Distinct()));

    private static Error ToError(OwnerWriteStatus status) => status switch
    {
        OwnerWriteStatus.NotFound => NotFound,
        OwnerWriteStatus.UserNotFound => new Error("Owner.UserNotFound", "User not found or inactive."),
        OwnerWriteStatus.UserAlreadyOwner => new Error("Owner.AlreadyExists", "This user already has an owner profile."),
        OwnerWriteStatus.TaxIdTaken => new Error("Owner.TaxIdAlreadyExists", "Tax id is already used by another owner."),
        OwnerWriteStatus.HasActiveBranches => new Error("Owner.HasActiveBranches", "Owner still has active branches."),
        _ => new Error("Error.Exception", "An unexpected error occurred.")
    };
}
