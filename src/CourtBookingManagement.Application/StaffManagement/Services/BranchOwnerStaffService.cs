using CourtBookingManagement.Application.Auth.Interfaces;
using CourtBookingManagement.Application.Auth.Exceptions;
using CourtBookingManagement.Application.Matching.Models;
using CourtBookingManagement.Application.StaffManagement.DTOs;
using CourtBookingManagement.Application.StaffManagement.Interfaces;
using CourtBookingManagement.Domain.Abstractions;

namespace CourtBookingManagement.Application.StaffManagement.Services;

public sealed class BranchOwnerStaffService(
    IBranchOwnerStaffRepository repository,
    IBranchAuthorizationService branchAuthorizationService) : IBranchOwnerStaffService
{
    private const string StaffViewPermission = "staff.view";
    private static readonly string[] StatusValues = ["ACTIVE", "INACTIVE"];
    private static readonly string[] ActivationStatusValues =
        ["PENDING_ACTIVATION", "ACTIVE", "INACTIVE", "EXPIRED"];
    private static readonly string[] SortFields =
        ["displayName", "email", "staffCode", "status", "createdAt", "lastLoginAt"];

    private static readonly Error OwnerNotFound = new(
        "STAFF_OWNER_NOT_FOUND",
        "An active Branch Owner profile is required.");

    private static readonly Error StaffNotFound = new(
        "STAFF_NOT_FOUND",
        "Staff was not found in the caller's branch scope.");

    private static readonly Error BranchAccessDenied = new(
        "STAFF_BRANCH_ACCESS_DENIED",
        "The requested branch is not owned by the caller.");

    private static readonly Error InvalidRequest = new(
        "STAFF_INVALID_REQUEST",
        "One or more Staff list filters are invalid.");

    private static readonly Error InvalidSort = new(
        "STAFF_INVALID_SORT",
        "The requested Staff sort field or direction is not supported.");

    public async Task<Result<IReadOnlyList<StaffOwnedBranchResponse>>> GetBranchesAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken)
    {
        var scope = await repository.GetOwnerScopeAsync(ownerUserId, cancellationToken);
        if (scope is null)
        {
            return OwnerNotFound;
        }

        var branchIds = scope.Branches.Select(branch => branch.BranchId).ToArray();
        if (branchIds.Length == 0)
        {
            return BranchAccessDenied;
        }

        await EnsureCanViewBranchesAsync(ownerUserId, branchIds, cancellationToken);
        var result = new List<StaffOwnedBranchResponse>(scope.Branches.Count);
        foreach (var branch in scope.Branches)
        {
            var canCreate = true;
            try
            {
                await branchAuthorizationService.EnsureCanAccessAsync(
                    ownerUserId, branch.BranchId, "staff.create", cancellationToken);
            }
            catch (BranchAccessDeniedException)
            {
                canCreate = false;
            }

            result.Add(new StaffOwnedBranchResponse(branch.BranchId, branch.BranchName, canCreate));
        }

        return result;
    }

    public async Task<Result<PagedResult<StaffListItemResponse>>> GetListAsync(
        Guid ownerUserId,
        StaffListRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var scope = await repository.GetOwnerScopeAsync(ownerUserId, cancellationToken);
        if (scope is null)
        {
            return OwnerNotFound;
        }

        if (!TryNormalizeFilters(request, out var normalizedRequest, out var filterError))
        {
            return filterError;
        }

        var branches = scope.Branches;
        if (normalizedRequest.BranchId is Guid branchId)
        {
            var requestedBranch = branches.FirstOrDefault(branch => branch.BranchId == branchId);
            if (requestedBranch is null)
            {
                return BranchAccessDenied;
            }

            branches = [requestedBranch];
        }

        var branchIds = branches.Select(branch => branch.BranchId).ToArray();
        if (branchIds.Length == 0)
        {
            return BranchAccessDenied;
        }

        await EnsureCanViewBranchesAsync(ownerUserId, branchIds, cancellationToken);

        return await repository.SearchAsync(
            scope.OwnerId,
            branchIds,
            normalizedRequest,
            cancellationToken);
    }

    public async Task<Result<StaffDetailResponse>> GetByIdAsync(
        Guid ownerUserId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var scope = await repository.GetOwnerScopeAsync(ownerUserId, cancellationToken);
        if (scope is null)
        {
            return OwnerNotFound;
        }

        var branchIds = scope.Branches.Select(branch => branch.BranchId).ToArray();
        if (branchIds.Length == 0)
        {
            return BranchAccessDenied;
        }

        await EnsureCanViewBranchesAsync(ownerUserId, branchIds, cancellationToken);

        var staff = await repository.GetByIdAsync(
            scope.OwnerId,
            userId,
            branchIds,
            cancellationToken);
        if (staff is null)
        {
            return StaffNotFound;
        }

        return staff;
    }

    private async Task EnsureCanViewBranchesAsync(
        Guid ownerUserId,
        IReadOnlyCollection<Guid> branchIds,
        CancellationToken cancellationToken)
    {
        foreach (var branchId in branchIds)
        {
            await branchAuthorizationService.EnsureCanAccessAsync(
                ownerUserId,
                branchId,
                StaffViewPermission,
                cancellationToken);
        }
    }

    private static bool TryNormalizeFilters(
        StaffListRequest request,
        out StaffListRequest normalizedRequest,
        out Error error)
    {
        normalizedRequest = request;
        error = Error.None;

        var status = NormalizeOptional(request.Status);
        if (status is not null && !StatusValues.Contains(status, StringComparer.Ordinal))
        {
            error = InvalidRequest;
            return false;
        }

        var activationStatus = NormalizeOptional(request.ActivationStatus);
        if (activationStatus is not null &&
            !ActivationStatusValues.Contains(activationStatus, StringComparer.Ordinal))
        {
            error = InvalidRequest;
            return false;
        }

        var sortBy = NormalizeOptional(request.SortBy) ?? "createdAt";
        var sortDirection = NormalizeOptional(request.SortDirection) ?? "DESC";
        if (!SortFields.Contains(sortBy, StringComparer.OrdinalIgnoreCase) ||
            sortDirection is not ("ASC" or "DESC"))
        {
            error = InvalidSort;
            return false;
        }

        normalizedRequest = request with
        {
            Search = string.IsNullOrWhiteSpace(request.Search) ? null : request.Search.Trim(),
            Status = status,
            ActivationStatus = activationStatus,
            SortBy = SortFields.First(field =>
                string.Equals(field, sortBy, StringComparison.OrdinalIgnoreCase)),
            SortDirection = sortDirection
        };
        return true;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
}
