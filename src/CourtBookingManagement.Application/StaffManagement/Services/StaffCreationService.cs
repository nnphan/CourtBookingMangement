using System.Security.Cryptography;
using System.Text;
using CourtBookingManagement.Application.Auth.Interfaces;
using CourtBookingManagement.Application.StaffManagement.DTOs;
using CourtBookingManagement.Application.StaffManagement.Interfaces;
using CourtBookingManagement.Application.StaffManagement.Models;
using CourtBookingManagement.Domain.Abstractions;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace CourtBookingManagement.Application.StaffManagement.Services;

public sealed class StaffCreationService(
    IBranchOwnerStaffRepository repository,
    IBranchAuthorizationService branchAuthorization,
    IValidator<CreateStaffRequest> validator,
    IJwtTokenService tokens,
    IStaffInvitationDelivery delivery,
    ILogger<StaffCreationService> logger) : IStaffCreationService
{
    public async Task<Result<CreateStaffResponse>> ResendAsync(
        Guid actorId, Guid userId, CancellationToken cancellationToken)
    {
        var scope = await repository.GetOwnerScopeAsync(actorId, cancellationToken);
        if (scope is null)
        {
            return new Error("STAFF_OWNER_NOT_FOUND", "An active Branch Owner profile is required.");
        }

        if (scope.Branches.Count == 0)
        {
            return new Error("STAFF_BRANCH_ACCESS_DENIED", "An active owned branch is required.");
        }

        foreach (var branch in scope.Branches.OrderBy(x => x.BranchId))
        {
            await branchAuthorization.EnsureCanAccessAsync(actorId, branch.BranchId, "staff.create", cancellationToken);
        }

        if (!delivery.IsConfigured)
        {
            return new Error("STAFF_DELIVERY_NOT_CONFIGURED", "Staff invitation delivery is not configured.");
        }

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var invitation = await repository.IssueInvitationAsync(
            scope.OwnerId, actorId, userId, Guid.NewGuid(), tokens.HashToken(token),
            DateTimeOffset.UtcNow.AddHours(24), cancellationToken);
        if (invitation is null)
        {
            return new Error("STAFF_NOT_FOUND", "A pending Staff account was not found in the caller's scope.");
        }

        var sent = await delivery.SendAsync(invitation.Email, token, CancellationToken.None);
        await repository.SetInvitationDeliveryAsync(invitation.InvitationId, sent, CancellationToken.None);
        logger.LogInformation("Staff activation invitation reissued. ActorId={ActorId} StaffUserId={StaffUserId} InvitationSent={Sent}",
            actorId, userId, sent);
        return new CreateStaffResponse(userId, sent ? "SENT" : "FAILED", sent ? null : "INVITATION_SEND_FAILED");
    }

    public async Task<Result> ActivateAsync(ActivateStaffRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.Token) || request.Token.Length != 64 ||
            !request.Token.All(Uri.IsHexDigit))
        {
            return Result.Failure(new Error("STAFF_INVITATION_INVALID", "The invitation is invalid or expired."));
        }

        var password = request.NewPassword;
        if (string.IsNullOrEmpty(password) || password.Length < 12 ||
            Encoding.UTF8.GetByteCount(password) > 72 ||
            new[] { password.Any(char.IsLower), password.Any(char.IsUpper),
                password.Any(char.IsDigit), password.Any(c => !char.IsLetterOrDigit(c)) }.Count(x => x) < 3)
        {
            return Result.Failure(new Error("STAFF_PASSWORD_INVALID",
                "The password must be at least 12 characters, at most 72 UTF-8 bytes, and contain three of uppercase, lowercase, digits, and symbols."));
        }

        var activated = await repository.ActivateAsync(
            tokens.HashToken(request.Token.ToUpperInvariant()), BCrypt.Net.BCrypt.HashPassword(password), cancellationToken);
        return activated
            ? Result.Success()
            : Result.Failure(new Error("STAFF_INVITATION_INVALID", "The invitation is invalid or expired."));
    }

    public async Task<Result<CreateStaffResponse>> CreateAsync(
        Guid actorId, CreateStaffRequest request, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return new Error("STAFF_INVALID_REQUEST", string.Join(" ", validation.Errors.Select(x => x.ErrorMessage)));
        }

        var scope = await repository.GetOwnerScopeAsync(actorId, cancellationToken);
        if (scope is null)
        {
            return new Error("STAFF_OWNER_NOT_FOUND", "An active Branch Owner profile is required.");
        }

        var allowed = scope.Branches.Select(x => x.BranchId).ToHashSet();
        if (request.BranchIds.Any(id => !allowed.Contains(id)))
        {
            return new Error("STAFF_BRANCH_ACCESS_DENIED", "The requested branch is not owned by the caller.");
        }

        foreach (var branchId in request.BranchIds.Order())
        {
            await branchAuthorization.EnsureCanAccessAsync(actorId, branchId, "staff.create", cancellationToken);
        }

        if (!delivery.IsConfigured)
        {
            return new Error("STAFF_DELIVERY_NOT_CONFIGURED", "Staff invitation delivery is not configured.");
        }

        var normalized = request with
        {
            DisplayName = request.DisplayName.Trim(),
            Email = request.Email.Trim().ToLowerInvariant(),
            PhoneNumber = request.PhoneNumber.Trim(),
            StaffCode = request.StaffCode?.Trim(),
            JobTitle = request.JobTitle?.Trim(),
            Notes = request.Notes?.Trim()
        };
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var creation = new StaffCreation(
            Guid.NewGuid(), Guid.NewGuid(), scope.OwnerId, actorId, normalized,
            BCrypt.Net.BCrypt.HashPassword(Convert.ToHexString(RandomNumberGenerator.GetBytes(32))),
            tokens.HashToken(token), DateTimeOffset.UtcNow.AddHours(24));

        var outcome = await repository.CreateAsync(creation, cancellationToken);
        if (outcome != StaffCreationOutcome.Created)
        {
            return outcome switch
            {
                StaffCreationOutcome.BranchAccessDenied =>
                    new Error("STAFF_BRANCH_ACCESS_DENIED", "The requested branch is not authorized."),
                StaffCreationOutcome.UserAlreadyExists =>
                    new Error("STAFF_ALREADY_EXISTS", "An account with the supplied contact details already exists."),
                StaffCreationOutcome.StaffCodeAlreadyUsed =>
                    new Error("STAFF_CODE_ALREADY_USED", "The staff code is already used in a selected branch."),
                _ => new Error("STAFF_ROLE_NOT_CONFIGURED", "The STAFF role is not configured.")
            };
        }

        // The committed account must have a delivery outcome even if the request disconnects.
        var sent = await delivery.SendAsync(normalized.Email, token, CancellationToken.None);
        await repository.SetInvitationDeliveryAsync(creation.InvitationId, sent, CancellationToken.None);
        logger.LogInformation("Staff account created. ActorId={ActorId} StaffUserId={StaffUserId} InvitationSent={Sent}",
            actorId, creation.UserId, sent);

        return new CreateStaffResponse(creation.UserId, sent ? "SENT" : "FAILED",
            sent ? null : "INVITATION_SEND_FAILED");
    }
}
