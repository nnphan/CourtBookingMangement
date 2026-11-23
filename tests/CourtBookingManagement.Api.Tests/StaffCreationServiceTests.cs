using CourtBookingManagement.Application.Auth.Interfaces;
using CourtBookingManagement.Application.Auth.Exceptions;
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
    private readonly Mock<IStaffInvitationDelivery> _delivery = new();
    private readonly StaffCreationService _service;

    public StaffCreationServiceTests()
    {
        _repository.Setup(x => x.GetOwnerScopeAsync(_actor, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StaffOwnerScope(_owner, [new StaffOwnerBranch(_branch, "A")]));
        _tokens.Setup(x => x.HashToken(It.IsAny<string>())).Returns((string token) => $"hash:{token}");
        _delivery.SetupGet(x => x.IsConfigured).Returns(true);
        _service = new StaffCreationService(_repository.Object, _authorization.Object,
            new CreateStaffRequestValidator(), _tokens.Object, _delivery.Object,
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
        _delivery.Setup(x => x.SendAsync("staff@example.com", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _service.CreateAsync(_actor, ValidRequest(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("FAILED", result.Value.InvitationDeliveryStatus);
        Assert.Equal("INVITATION_SEND_FAILED", result.Value.WarningCode);
        Assert.NotNull(created);
        Assert.Equal(created.UserId, result.Value.UserId);
        _repository.Verify(x => x.SetInvitationDeliveryAsync(created.InvitationId, false, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DuplicateAccountDoesNotSendAnInvitation()
    {
        _repository.Setup(x => x.CreateAsync(It.IsAny<StaffCreation>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(StaffCreationOutcome.UserAlreadyExists);

        var result = await _service.CreateAsync(_actor, ValidRequest(), CancellationToken.None);

        Assert.Equal("STAFF_ALREADY_EXISTS", result.Error.Code);
        _delivery.Verify(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
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
        _repository.Verify(x => x.ActivateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ValidActivationPassesOnlyHashedCredentialsToRepository()
    {
        const string password = "StrongPassword42!";
        var token = new string('A', 64);
        _repository.Setup(x => x.ActivateAsync($"hash:{token}", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _service.ActivateAsync(new ActivateStaffRequest
        {
            Token = token,
            NewPassword = password
        }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        _repository.Verify(x => x.ActivateAsync($"hash:{token}",
            It.Is<string>(hash => VerifyPassword(password, hash)), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static bool VerifyPassword(string password, string hash) => BCrypt.Net.BCrypt.Verify(password, hash);

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
