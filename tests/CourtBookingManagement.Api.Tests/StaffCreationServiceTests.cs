using CourtBookingManagement.Application.Auth.Interfaces;
using CourtBookingManagement.Application.Auth.Exceptions;
using CourtBookingManagement.Application.Abstractions.Clock;
using CourtBookingManagement.Application.Email;
using CourtBookingManagement.Application.StaffManagement.DTOs;
using CourtBookingManagement.Application.StaffManagement.Interfaces;
using CourtBookingManagement.Application.StaffManagement.Models;
using CourtBookingManagement.Application.StaffManagement.Services;
using CourtBookingManagement.Application.StaffManagement.Validators;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace CourtBookingManagement.Api.Tests;

public sealed class StaffCreationServiceTests
{
    private readonly Guid _actor = Guid.NewGuid();
    private readonly Guid _owner = Guid.NewGuid();
    private readonly Guid _branch = Guid.NewGuid();
    private readonly Mock<IBranchOwnerStaffRepository> _repository = new();
    private readonly Mock<IBranchAuthorizationService> _authorization = new();
    private readonly Mock<IJwtTokenService> _tokens = new();
    private readonly Mock<IStaffInvitationTokenService> _invitationTokens = new();
    private readonly Mock<IStaffInvitationRepository> _invitationRepository = new();
    private readonly Mock<IStaffInvitationPolicy> _invitationPolicy = new();
    private readonly Mock<IStaffInvitationEmailComposer> _emailComposer = new();
    private readonly Mock<IEmailSender> _emailSender = new();
    private readonly Mock<IDateTimeProvider> _dateTimeProvider = new();
    private readonly Mock<IStaffInvitationDelivery> _delivery = new();
    private readonly StaffCreationService _service;
    private readonly string _invitationToken = new('A', 43);
    private readonly DateTimeOffset _databaseNow = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private StaffInvitationEmailData? _composedInvitation;

    public StaffCreationServiceTests()
    {
        _repository.Setup(x => x.GetOwnerScopeAsync(_actor, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StaffOwnerScope(_owner, [new StaffOwnerBranch(_branch, "A")]));
        _tokens.Setup(x => x.HashToken(It.IsAny<string>())).Returns((string token) => $"hash:{token}");
        _invitationTokens.Setup(x => x.GenerateToken()).Returns(_invitationToken);
        _invitationTokens.Setup(x => x.HashToken(_invitationToken)).Returns($"invitation-hash:{_invitationToken}");
        _invitationTokens.Setup(x => x.IsValidToken(_invitationToken)).Returns(true);
        _invitationRepository.Setup(x => x.GetStateByTokenHashAsync(
                $"invitation-hash:{_invitationToken}", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ValidInvitationState());
        _invitationPolicy.SetupGet(x => x.ExpirationHours).Returns(24);
        _invitationPolicy.SetupGet(x => x.ResendCooldownMinutes).Returns(5);
        _invitationPolicy.SetupGet(x => x.MaxResendsPerWindow).Returns(3);
        _invitationPolicy.SetupGet(x => x.ResendWindowMinutes).Returns(15);
        _dateTimeProvider.SetupGet(x => x.UtcNow)
            .Returns(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        _emailComposer.Setup(x => x.Compose(It.IsAny<StaffInvitationEmailData>()))
            .Callback<StaffInvitationEmailData>(data => _composedInvitation = data)
            .Returns((StaffInvitationEmailData data) =>
                new EmailMessage(data.Recipient, "activation", data.Token, data.Token));
        _emailSender.Setup(x => x.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _delivery.SetupGet(x => x.IsConfigured).Returns(true);
        _repository.Setup(x => x.SetInvitationDeliveryAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _service = new StaffCreationService(_repository.Object, _authorization.Object,
            new CreateStaffRequestValidator(), _tokens.Object, _invitationTokens.Object,
            _invitationRepository.Object, _invitationPolicy.Object, _emailComposer.Object, _emailSender.Object,
            _dateTimeProvider.Object, _delivery.Object,
            NullLogger<StaffCreationService>.Instance);
    }

    private CreateStaffRequest ValidRequest() => new()
    {
        DisplayName = "Staff Member",
        Email = "Staff@example.com",
        PhoneNumber = "0901234567",
        BranchIds = [_branch]
    };

    [Fact]
    public async Task ForeignBranchDoesNotCreateAnAccount()
    {
        var result = await _service.CreateAsync(_actor,
            ValidRequest() with { BranchIds = [_branch, Guid.NewGuid()] }, CancellationToken.None);

        Assert.Equal("STAFF_BRANCH_ACCESS_DENIED", result.Error.Code);
        _repository.Verify(x => x.CreateAsync(It.IsAny<StaffCreation>(), It.IsAny<CancellationToken>()), Times.Never);
        _delivery.Verify(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _emailSender.Verify(x => x.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DuplicateBranchesAreRejectedBeforeWriting()
    {
        var result = await _service.CreateAsync(_actor,
            ValidRequest() with { BranchIds = [_branch, _branch] }, CancellationToken.None);

        Assert.Equal("STAFF_INVALID_REQUEST", result.Error.Code);
        _repository.Verify(x => x.CreateAsync(It.IsAny<StaffCreation>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeliveryFailureIsReportedAfterAccountCreation()
    {
        StaffCreation? created = null;
        _repository.Setup(x => x.CreateAsync(It.IsAny<StaffCreation>(), It.IsAny<CancellationToken>()))
            .Callback<StaffCreation, CancellationToken>((value, _) => created = value)
            .ReturnsAsync(StaffCreationOutcome.Created);
        _emailSender.Setup(x => x.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _service.CreateAsync(_actor, ValidRequest(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("FAILED", result.Value.InvitationDeliveryStatus);
        Assert.Equal("INVITATION_SEND_FAILED", result.Value.WarningCode);
        Assert.NotNull(created);
        Assert.Equal(created.UserId, result.Value.UserId);
        Assert.Equal($"invitation-hash:{_invitationToken}", created.TokenHash);
        Assert.Equal(new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero), created.ExpiresAt);
        Assert.Equal("staff@example.com", _composedInvitation?.Recipient);
        Assert.Equal("Staff Member", _composedInvitation?.DisplayName);
        Assert.Equal(["A"], _composedInvitation?.BranchNames);
        Assert.Equal(created.ExpiresAt, _composedInvitation?.ExpiresAt);
        _repository.Verify(x => x.SetInvitationDeliveryAsync(
            created.InvitationId, _actor, false, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InvitationIsSentOnlyAfterCreationAndDeliveryStateIsPersistedLast()
    {
        var callOrder = new List<string>();
        _repository.Setup(x => x.CreateAsync(It.IsAny<StaffCreation>(), It.IsAny<CancellationToken>()))
            .Callback<StaffCreation, CancellationToken>((_, _) => callOrder.Add("create"))
            .ReturnsAsync(StaffCreationOutcome.Created);
        _emailSender.Setup(x => x.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .Callback<EmailMessage, CancellationToken>((_, _) => callOrder.Add("send"))
            .ReturnsAsync(true);
        _repository.Setup(x => x.SetInvitationDeliveryAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, Guid, bool, CancellationToken>((_, _, _, _) => callOrder.Add("delivery-state"))
            .ReturnsAsync(true);

        var result = await _service.CreateAsync(_actor, ValidRequest(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(["create", "send", "delivery-state"], callOrder);
    }

    [Fact]
    public async Task DeliveryStatePersistenceFailureReturnsCommittedCreationWithWarning()
    {
        _repository.Setup(x => x.CreateAsync(It.IsAny<StaffCreation>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(StaffCreationOutcome.Created);
        _repository.Setup(x => x.SetInvitationDeliveryAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _service.CreateAsync(_actor, ValidRequest(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("SENT", result.Value.InvitationDeliveryStatus);
        Assert.Equal("INVITATION_STATUS_PERSISTENCE_FAILED", result.Value.WarningCode);
    }

    [Fact]
    public async Task DuplicateAccountDoesNotSendAnInvitation()
    {
        _repository.Setup(x => x.CreateAsync(It.IsAny<StaffCreation>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(StaffCreationOutcome.UserAlreadyExists);

        var result = await _service.CreateAsync(_actor, ValidRequest(), CancellationToken.None);

        Assert.Equal("STAFF_ALREADY_EXISTS", result.Error.Code);
        _delivery.Verify(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _emailSender.Verify(x => x.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResendIssuesVersionTwoInvitationAndSendsAfterIssuance()
    {
        var userId = Guid.NewGuid();
        var invitationId = Guid.NewGuid();
        var callOrder = new List<string>();
        _repository.Setup(x => x.IssueInvitationAsync(
                _owner,
                _actor,
                userId,
                It.IsAny<Guid>(),
                $"invitation-hash:{_invitationToken}",
                new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero),
                5,
                3,
                15,
                It.IsAny<CancellationToken>()))
            .Callback<Guid, Guid, Guid, Guid, string, DateTimeOffset, int, int, int, CancellationToken>(
                (_, _, _, _, _, _, _, _, _, _) => callOrder.Add("issue"))
            .ReturnsAsync(new StaffInvitationIssueResult(
                StaffInvitationIssueOutcome.Issued,
                new StaffInvitation(
                    invitationId, "staff@example.com", "Staff Member", ["A"], false, false)));
        _emailSender.Setup(x => x.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .Callback<EmailMessage, CancellationToken>((_, _) => callOrder.Add("send"))
            .ReturnsAsync(true);
        _repository.Setup(x => x.SetInvitationDeliveryAsync(
                invitationId, _actor, true, It.IsAny<CancellationToken>()))
            .Callback<Guid, Guid, bool, CancellationToken>((_, _, _, _) => callOrder.Add("delivery-state"))
            .ReturnsAsync(true);

        var result = await _service.ResendAsync(_actor, userId, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(userId, result.Value.UserId);
        Assert.Equal("SENT", result.Value.InvitationDeliveryStatus);
        Assert.Equal("staff@example.com", _composedInvitation?.Recipient);
        Assert.Equal(_invitationToken, _composedInvitation?.Token);
        Assert.Equal(["A"], _composedInvitation?.BranchNames);
        Assert.Equal(new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero), _composedInvitation?.ExpiresAt);
        Assert.Equal(["issue", "send", "delivery-state"], callOrder);
        _delivery.Verify(x => x.SendAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(StaffInvitationIssueOutcome.Cooldown, "INVITATION_RESEND_TOO_SOON")]
    [InlineData(StaffInvitationIssueOutcome.RateLimited, "INVITATION_RATE_LIMITED")]
    public async Task ResendLimitReturnsRetryAfterWithoutSending(
        StaffInvitationIssueOutcome outcome,
        string expectedErrorCode)
    {
        _repository.Setup(x => x.IssueInvitationAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StaffInvitationIssueResult(outcome, RetryAfterSeconds: 240));

        var result = await _service.ResendAsync(_actor, Guid.NewGuid(), CancellationToken.None);

        Assert.Equal(expectedErrorCode, result.Error.Code);
        Assert.Equal(240, result.Error.RetryAfterSeconds);
        _emailSender.Verify(x => x.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
        _repository.Verify(x => x.SetInvitationDeliveryAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResendRejectsAnActiveStaffAccount()
    {
        _repository.Setup(x => x.IssueInvitationAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StaffInvitationIssueResult(StaffInvitationIssueOutcome.AlreadyActive));

        var result = await _service.ResendAsync(_actor, Guid.NewGuid(), CancellationToken.None);

        Assert.Equal("STAFF_ALREADY_ACTIVE", result.Error.Code);
        _emailSender.Verify(x => x.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task WeakPasswordCannotConsumeInvitation()
    {
        var result = await _service.ActivateAsync(new ActivateStaffRequest
        {
            Token = new string('A', 64),
            NewPassword = "weak"
        }, CancellationToken.None);

        Assert.Equal("STAFF_PASSWORD_INVALID", result.Error.Code);
        _repository.Verify(x => x.ActivateAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<short>(), It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ValidActivationPassesOnlyHashedCredentialsToRepository()
    {
        const string password = "StrongPassword42!";
        var token = new string('A', 64);
        _repository.Setup(x => x.ActivateAsync(
                $"hash:{token}", It.IsAny<string>(), 1, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _service.ActivateAsync(new ActivateStaffRequest
        {
            Token = token,
            NewPassword = password
        }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        _repository.Verify(x => x.ActivateAsync(
            $"hash:{token}",
            It.Is<string>(hash => VerifyPassword(password, hash)),
            1,
            null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static bool VerifyPassword(string password, string hash) => BCrypt.Net.BCrypt.Verify(password, hash);

    [Fact]
    public async Task ValidateInvitationReturnsOnlySafeInvitationDetails()
    {
        var result = await _service.ValidateInvitationAsync(
            new ValidateStaffInvitationRequest
            {
                Token = _invitationToken,
                Email = " STAFF@example.com "
            },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Valid);
        Assert.Equal("Staff Member", result.Value.DisplayName);
        Assert.Equal(["A"], result.Value.BranchNames);
        Assert.Equal(_databaseNow.AddHours(24), result.Value.ExpiresAt);
        _invitationRepository.Verify(x => x.GetStateByTokenHashAsync(
            $"invitation-hash:{_invitationToken}", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UnknownTokenAndEmailMismatchUseTheSameGenericError()
    {
        _invitationRepository.Setup(x => x.GetStateByTokenHashAsync(
                $"invitation-hash:{_invitationToken}", It.IsAny<CancellationToken>()))
            .ReturnsAsync((StaffInvitationState?)null);
        var unknown = await _service.ValidateInvitationAsync(
            new ValidateStaffInvitationRequest { Token = _invitationToken, Email = "staff@example.com" },
            CancellationToken.None);

        _invitationRepository.Setup(x => x.GetStateByTokenHashAsync(
                $"invitation-hash:{_invitationToken}", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ValidInvitationState());
        var mismatch = await _service.ValidateInvitationAsync(
            new ValidateStaffInvitationRequest { Token = _invitationToken, Email = "other@example.com" },
            CancellationToken.None);

        Assert.Equal("STAFF_INVITATION_INVALID", unknown.Error.Code);
        Assert.Equal(unknown.Error.Code, mismatch.Error.Code);
        Assert.Equal(unknown.Error.Description, mismatch.Error.Description);
    }

    [Fact]
    public async Task ValidateInvitationReportsExpiredUsedAndRevokedStates()
    {
        _invitationRepository.Setup(x => x.GetStateByTokenHashAsync(
                $"invitation-hash:{_invitationToken}", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ValidInvitationState(expiresAt: _databaseNow.AddSeconds(-1)));
        var expired = await ValidateInvitation();

        _invitationRepository.Setup(x => x.GetStateByTokenHashAsync(
                $"invitation-hash:{_invitationToken}", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ValidInvitationState(usedAt: _databaseNow));
        var used = await ValidateInvitation();

        _invitationRepository.Setup(x => x.GetStateByTokenHashAsync(
                $"invitation-hash:{_invitationToken}", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ValidInvitationState(revokedAt: _databaseNow));
        var revoked = await ValidateInvitation();

        Assert.Equal("STAFF_INVITATION_EXPIRED", expired.Error.Code);
        Assert.Equal("STAFF_INVITATION_ALREADY_USED", used.Error.Code);
        Assert.Equal("STAFF_INVITATION_REVOKED", revoked.Error.Code);
    }

    [Fact]
    public async Task ValidateInvitationRejectsActiveOrPrivilegedAccounts()
    {
        _invitationRepository.Setup(x => x.GetStateByTokenHashAsync(
                $"invitation-hash:{_invitationToken}", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ValidInvitationState() with { UserIsActive = true });
        var active = await ValidateInvitation();

        _invitationRepository.Setup(x => x.GetStateByTokenHashAsync(
                $"invitation-hash:{_invitationToken}", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ValidInvitationState() with { HasNonStaffRole = true });
        var privileged = await ValidateInvitation();

        Assert.Equal("STAFF_INVITATION_INVALID", active.Error.Code);
        Assert.Equal("STAFF_INVITATION_INVALID", privileged.Error.Code);
    }

    [Fact]
    public async Task ActivateInvitationRequiresConfirmationAndPasswordPolicy()
    {
        var mismatch = await _service.ActivateInvitationAsync(
            new ActivateStaffInvitationRequest
            {
                Token = _invitationToken,
                Email = "staff@example.com",
                Password = "StrongPassword42!",
                ConfirmPassword = "DifferentPassword42!"
            },
            CancellationToken.None);
        var weak = await _service.ActivateInvitationAsync(
            new ActivateStaffInvitationRequest
            {
                Token = _invitationToken,
                Email = "staff@example.com",
                Password = "weak",
                ConfirmPassword = "weak"
            },
            CancellationToken.None);

        Assert.Equal("PASSWORD_CONFIRMATION_MISMATCH", mismatch.Error.Code);
        Assert.Equal("PASSWORD_POLICY_FAILED", weak.Error.Code);
        _repository.Verify(x => x.ActivateAsync(
            It.IsAny<string>(), It.IsAny<string>(), 2, It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ActivateInvitationPassesEmailAndOnlyPasswordHashToRepository()
    {
        const string password = "StrongPassword42!";
        _repository.Setup(x => x.ActivateAsync(
                $"invitation-hash:{_invitationToken}",
                It.IsAny<string>(),
                2,
                "staff@example.com",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _service.ActivateInvitationAsync(
            new ActivateStaffInvitationRequest
            {
                Token = _invitationToken,
                Email = " STAFF@example.com ",
                Password = password,
                ConfirmPassword = password
            },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        _repository.Verify(x => x.ActivateAsync(
            $"invitation-hash:{_invitationToken}",
            It.Is<string>(hash => VerifyPassword(password, hash)),
            2,
            "staff@example.com",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private async Task<CourtBookingManagement.Domain.Abstractions.Result<StaffInvitationValidationResponse>>
        ValidateInvitation() =>
        await _service.ValidateInvitationAsync(
            new ValidateStaffInvitationRequest
            {
                Token = _invitationToken,
                Email = "staff@example.com"
            },
            CancellationToken.None);

    private StaffInvitationState ValidInvitationState(
        DateTimeOffset? expiresAt = null,
        DateTimeOffset? usedAt = null,
        DateTimeOffset? revokedAt = null) =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "staff@example.com",
            "Staff Member",
            expiresAt ?? _databaseNow.AddHours(24),
            _databaseNow,
            usedAt,
            revokedAt,
            2,
            "CREATE",
            false,
            false,
            true,
            false,
            ["A"]);

    [Fact]
    public async Task StaffBranchLookupChecksEveryOwnedBranch()
    {
        var secondBranch = Guid.NewGuid();
        _repository.Setup(x => x.GetOwnerScopeAsync(_actor, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StaffOwnerScope(_owner,
                [new StaffOwnerBranch(_branch, "A"), new StaffOwnerBranch(secondBranch, "B")]));
        var service = new BranchOwnerStaffService(_repository.Object, _authorization.Object);

        var result = await service.GetBranchesAsync(_actor, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { _branch, secondBranch }, result.Value.Select(branch => branch.BranchId));
        _authorization.Verify(x => x.EnsureCanAccessAsync(_actor, _branch, "staff.view", It.IsAny<CancellationToken>()), Times.Once);
        _authorization.Verify(x => x.EnsureCanAccessAsync(_actor, secondBranch, "staff.view", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StaffBranchLookupRejectsMissingOwnerAndUnauthorizedBranch()
    {
        var service = new BranchOwnerStaffService(_repository.Object, _authorization.Object);
        _repository.Setup(x => x.GetOwnerScopeAsync(_actor, It.IsAny<CancellationToken>()))
            .ReturnsAsync((StaffOwnerScope?)null);
        Assert.Equal("STAFF_OWNER_NOT_FOUND", (await service.GetBranchesAsync(_actor, CancellationToken.None)).Error.Code);

        _repository.Setup(x => x.GetOwnerScopeAsync(_actor, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StaffOwnerScope(_owner, [new StaffOwnerBranch(_branch, "A")]));
        _authorization.Setup(x => x.EnsureCanAccessAsync(_actor, _branch, "staff.view", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BranchAccessDeniedException());
        await Assert.ThrowsAsync<BranchAccessDeniedException>(() => service.GetBranchesAsync(_actor, CancellationToken.None));
    }

    [Fact]
    public async Task StaffBranchLookupReportsCreatePermissionPerBranch()
    {
        var secondBranch = Guid.NewGuid();
        _repository.Setup(x => x.GetOwnerScopeAsync(_actor, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StaffOwnerScope(_owner,
                [new StaffOwnerBranch(_branch, "A"), new StaffOwnerBranch(secondBranch, "B")]));
        _authorization.Setup(x => x.EnsureCanAccessAsync(_actor, secondBranch, "staff.create", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BranchAccessDeniedException());
        var service = new BranchOwnerStaffService(_repository.Object, _authorization.Object);

        var result = await service.GetBranchesAsync(_actor, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Single(branch => branch.BranchId == _branch).CanCreateStaff);
        Assert.False(result.Value.Single(branch => branch.BranchId == secondBranch).CanCreateStaff);
    }

    [Fact]
    public void UpdateRequiresDistinctNonEmptyBranches()
    {
        var validator = new UpdateStaffRequestValidator();
        var request = new UpdateStaffRequest
        {
            DisplayName = "Staff Member",
            PhoneNumber = "0901234567",
            BranchIds = [_branch]
        };

        Assert.True(validator.Validate(request).IsValid);
        Assert.False(validator.Validate(request with { BranchIds = [] }).IsValid);
        Assert.False(validator.Validate(request with { BranchIds = [_branch, _branch] }).IsValid);
        Assert.False(validator.Validate(request with { BranchIds = [Guid.Empty] }).IsValid);
    }

    [Fact]
    public void UpdateAndStatusRespectFieldLengthLimits()
    {
        var request = new UpdateStaffRequest
        {
            DisplayName = "Staff Member",
            PhoneNumber = "0901234567",
            BranchIds = [_branch],
            Notes = new string('x', 1001)
        };

        Assert.False(new UpdateStaffRequestValidator().Validate(request).IsValid);
        Assert.True(new ChangeStaffStatusRequestValidator().Validate(
            new ChangeStaffStatusRequest { Reason = new string('x', 500) }).IsValid);
        Assert.False(new ChangeStaffStatusRequestValidator().Validate(
            new ChangeStaffStatusRequest { Reason = new string('x', 501) }).IsValid);
    }
}
