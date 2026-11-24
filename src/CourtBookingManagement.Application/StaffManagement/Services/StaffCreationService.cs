using System.Security.Cryptography;
using System.Text;
using CourtBookingManagement.Application.Abstractions.Clock;
using CourtBookingManagement.Application.Email;
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
    IStaffInvitationTokenService invitationTokens,
    IStaffInvitationRepository invitationRepository,
    IStaffInvitationPolicy invitationPolicy,
    IStaffInvitationEmailComposer emailComposer,
    IEmailSender emailSender,
    IDateTimeProvider dateTimeProvider,
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

        var token = invitationTokens.GenerateToken();
        var expiresAt = new DateTimeOffset(dateTimeProvider.UtcNow, TimeSpan.Zero)
            .AddHours(invitationPolicy.ExpirationHours);
        var issueResult = await repository.IssueInvitationAsync(
            scope.OwnerId,
            actorId,
            userId,
            Guid.NewGuid(),
            invitationTokens.HashToken(token),
            expiresAt,
            invitationPolicy.ResendCooldownMinutes,
            invitationPolicy.MaxResendsPerWindow,
            invitationPolicy.ResendWindowMinutes,
            cancellationToken);
        if (issueResult.Outcome == StaffInvitationIssueOutcome.NotFound)
        {
            return new Error("STAFF_NOT_FOUND", "A pending Staff account was not found in the caller's scope.");
        }

        if (issueResult.Outcome == StaffInvitationIssueOutcome.AlreadyActive)
        {
            return new Error("STAFF_ALREADY_ACTIVE", "An activation invitation cannot be sent to an active Staff account.");
        }

        if (issueResult.Outcome == StaffInvitationIssueOutcome.Cooldown)
        {
            return new Error(
                "INVITATION_RESEND_TOO_SOON",
                "Please wait before requesting another activation invitation.",
                issueResult.RetryAfterSeconds);
        }

        if (issueResult.Outcome == StaffInvitationIssueOutcome.RateLimited)
        {
            return new Error(
                "INVITATION_RATE_LIMITED",
                "The activation invitation resend limit has been reached.",
                issueResult.RetryAfterSeconds);
        }

        var invitation = issueResult.Invitation
            ?? throw new InvalidOperationException("An issued Staff invitation was not returned.");
        var email = emailComposer.Compose(new StaffInvitationEmailData(
            invitation.Email,
            token,
            invitation.DisplayName,
            invitation.BranchNames,
            expiresAt));
        var sent = await emailSender.SendAsync(email, CancellationToken.None);
        var deliveryStatePersisted = await repository.SetInvitationDeliveryAsync(
            invitation.InvitationId, actorId, sent, CancellationToken.None);
        logger.LogInformation("Staff activation invitation reissued. ActorId={ActorId} StaffUserId={StaffUserId} InvitationSent={Sent}",
            actorId, userId, sent);
        return new CreateStaffResponse(
            userId,
            sent ? "SENT" : "FAILED",
            !deliveryStatePersisted
                ? "INVITATION_STATUS_PERSISTENCE_FAILED"
                : sent ? null : "INVITATION_SEND_FAILED");
    }

    public async Task<Result> ActivateAsync(ActivateStaffRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.Token) || request.Token.Length != 64 ||
            !request.Token.All(Uri.IsHexDigit))
        {
            return Result.Failure(new Error("STAFF_INVITATION_INVALID", "The invitation is invalid or expired."));
        }

        var password = request.NewPassword;
        if (!IsValidPassword(password))
        {
            return Result.Failure(new Error("STAFF_PASSWORD_INVALID",
                "The password must be at least 12 characters, at most 72 UTF-8 bytes, and contain three of uppercase, lowercase, digits, and symbols."));
        }

        var activated = await repository.ActivateAsync(
            tokens.HashToken(request.Token.ToUpperInvariant()),
            BCrypt.Net.BCrypt.HashPassword(password),
            linkVersion: 1,
            normalizedEmail: null,
            cancellationToken: cancellationToken);
        return activated
            ? Result.Success()
            : Result.Failure(new Error("STAFF_INVITATION_INVALID", "The invitation is invalid or expired."));
    }

    public async Task<Result<StaffInvitationValidationResponse>> ValidateInvitationAsync(
        ValidateStaffInvitationRequest request,
        CancellationToken cancellationToken)
    {
        var stateResult = await GetValidInvitationStateAsync(request.Token, request.Email, cancellationToken);
        if (stateResult.Error is not null)
        {
            return stateResult.Error;
        }

        var state = stateResult.State!;
        return new StaffInvitationValidationResponse(
            true,
            state.DisplayName,
            state.BranchNames,
            state.ExpiresAt);
    }

    public async Task<Result> ActivateInvitationAsync(
        ActivateStaffInvitationRequest request,
        CancellationToken cancellationToken)
    {
        var stateResult = await GetValidInvitationStateAsync(request.Token, request.Email, cancellationToken);
        if (stateResult.Error is not null)
        {
            return Result.Failure(stateResult.Error);
        }

        if (!string.Equals(request.Password, request.ConfirmPassword, StringComparison.Ordinal))
        {
            return Result.Failure(new Error(
                "PASSWORD_CONFIRMATION_MISMATCH",
                "Password and confirmation do not match."));
        }

        if (!IsValidPassword(request.Password))
        {
            return Result.Failure(new Error(
                "PASSWORD_POLICY_FAILED",
                "The password must be at least 12 characters, at most 72 UTF-8 bytes, and contain three of uppercase, lowercase, digits, and symbols."));
        }

        var normalizedEmail = NormalizeEmail(request.Email);
        var activated = await repository.ActivateAsync(
            invitationTokens.HashToken(request.Token),
            BCrypt.Net.BCrypt.HashPassword(request.Password),
            linkVersion: 2,
            normalizedEmail: normalizedEmail,
            cancellationToken: cancellationToken);
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
        var token = invitationTokens.GenerateToken();
        var expiresAt = dateTimeProvider.UtcNow
                .AddHours(invitationPolicy.ExpirationHours);
        var branchIds = request.BranchIds.ToHashSet();
        var branchNames = scope.Branches
            .Where(branch => branchIds.Contains(branch.BranchId))
            .Select(branch => branch.BranchName)
            .ToArray();
        var email = emailComposer.Compose(new StaffInvitationEmailData(
            normalized.Email,
            token,
            normalized.DisplayName,
            branchNames,
            expiresAt));
        var creation = new StaffCreation(
            Guid.NewGuid(), Guid.NewGuid(), scope.OwnerId, actorId, normalized,
            BCrypt.Net.BCrypt.HashPassword(Convert.ToHexString(RandomNumberGenerator.GetBytes(32))),
            invitationTokens.HashToken(token), expiresAt);

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
        var sent = await emailSender.SendAsync(email, CancellationToken.None);
        var deliveryStatePersisted = await repository.SetInvitationDeliveryAsync(
            creation.InvitationId, actorId, sent, CancellationToken.None);
        logger.LogInformation("Staff account created. ActorId={ActorId} StaffUserId={StaffUserId} InvitationSent={Sent}",
            actorId, creation.UserId, sent);

        return new CreateStaffResponse(creation.UserId, sent ? "SENT" : "FAILED",
            !deliveryStatePersisted
                ? "INVITATION_STATUS_PERSISTENCE_FAILED"
                : sent ? null : "INVITATION_SEND_FAILED");
    }

    private async Task<(StaffInvitationState? State, Error? Error)> GetValidInvitationStateAsync(
        string? token,
        string? email,
        CancellationToken cancellationToken)
    {
        if (!invitationTokens.IsValidToken(token ?? string.Empty) ||
            string.IsNullOrWhiteSpace(email))
        {
            return (null, InvalidInvitationError());
        }

        var normalizedEmail = NormalizeEmail(email);
        if (normalizedEmail.Length > 100)
        {
            return (null, InvalidInvitationError());
        }

        var state = await invitationRepository.GetStateByTokenHashAsync(
            invitationTokens.HashToken(token!),
            cancellationToken);

        if (state is null ||
            state.LinkVersion != 2 ||
            !string.Equals(state.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase))
        {
            return (null, InvalidInvitationError());
        }

        if (state.UsedAt is not null)
        {
            return (null, new Error("STAFF_INVITATION_ALREADY_USED", "The invitation has already been used."));
        }

        if (state.RevokedAt is not null)
        {
            return (null, new Error("STAFF_INVITATION_REVOKED", "The invitation is no longer valid."));
        }

        if (state.IsExpired)
        {
            return (null, new Error("STAFF_INVITATION_EXPIRED", "The invitation has expired."));
        }

        if (state.UserIsActive ||
            state.UserIsEmailVerified ||
            !state.HasActiveStaffAssignment ||
            state.HasNonStaffRole)
        {
            return (null, InvalidInvitationError());
        }

        return (state, null);
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private static bool IsValidPassword(string? password) =>
        !string.IsNullOrEmpty(password) &&
        password.Length >= 12 &&
        Encoding.UTF8.GetByteCount(password) <= 72 &&
        new[]
        {
            password.Any(char.IsLower),
            password.Any(char.IsUpper),
            password.Any(char.IsDigit),
            password.Any(character => !char.IsLetterOrDigit(character))
        }.Count(categoryPresent => categoryPresent) >= 3;

    private static Error InvalidInvitationError() =>
        new("STAFF_INVITATION_INVALID", "The invitation is invalid or expired.");
}
