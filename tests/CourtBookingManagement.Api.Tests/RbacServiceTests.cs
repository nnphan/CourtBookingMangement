using CourtBookingManagement.Application.Matching.Models;
using CourtBookingManagement.Application.Permissions.Models.Requests;
using CourtBookingManagement.Application.Permissions.Models.Responses;
using CourtBookingManagement.Application.Permissions.Repositories;
using CourtBookingManagement.Application.Permissions.Services;
using CourtBookingManagement.Application.Roles.Models.Requests;
using CourtBookingManagement.Application.Roles.Models.Responses;
using CourtBookingManagement.Application.Roles.Repositories;
using CourtBookingManagement.Application.Roles.Services;
using Xunit;

namespace CourtBookingManagement.Api.Tests;

public sealed class RbacServiceTests
{
    private static readonly Guid RoleId = Guid.NewGuid();
    private static readonly Guid PermissionId = Guid.NewGuid();
    private static readonly Guid ActorId = Guid.NewGuid();

    [Fact]
    public async Task Permission_create_normalizes_code_and_returns_created_record()
    {
        var repository = new FakePermissionRepository();
        var service = new PermissionService(repository);

        var result = await service.CreateAsync(
            new CreatePermissionRequest
            {
                Code = "  COURT.VIEW ",
                Name = " View courts "
            },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("court.view", repository.CreatedRequest!.Code);
        Assert.Equal("View courts", repository.CreatedRequest.Name);
        Assert.Equal("court.view", result.Value.Code);
    }

    [Fact]
    public async Task Permission_delete_returns_conflict_when_permission_is_in_use()
    {
        var repository = new FakePermissionRepository
        {
            DeleteStatus = PermissionDeleteStatus.InUse
        };
        var service = new PermissionService(repository);

        var result = await service.DeleteAsync(PermissionId, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Permission.InUse", result.Error.Code);
    }

    [Fact]
    public async Task Role_list_defaults_to_active_roles_and_includes_permission_items()
    {
        var repository = new FakeRoleRepository
        {
            SearchResult = [new RoleResponse
            {
                Id = RoleId,
                Permissions = [new PermissionSummaryResponse { Id = PermissionId, Code = "court.view" }]
            }]
        };
        var service = new RoleService(repository);

        var result = await service.SearchAsync(new RoleSearchRequest(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(repository.SearchRequest!.IsActive);
        Assert.Single(result.Value.Items);
        Assert.Single(result.Value.Items[0].Permissions);
        Assert.Equal(PermissionId, result.Value.Items[0].Permissions[0].Id);
    }

    [Fact]
    public async Task Role_create_rejects_duplicate_permission_ids_before_repository_write()
    {
        var repository = new FakeRoleRepository();
        var service = new RoleService(repository);

        var result = await service.CreateAsync(
            new CreateRoleRequest
            {
                Code = "staff",
                Name = "Staff",
                PermissionIds = [PermissionId, PermissionId]
            },
            ActorId,
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("ROLE.DUPLICATE_PERMISSION_IDS", result.Error.Code);
        Assert.Null(repository.CreatedRequest);
    }

    [Fact]
    public async Task Role_create_normalizes_code_and_passes_empty_permission_set_and_actor()
    {
        var repository = new FakeRoleRepository
        {
            CreateResult = new RoleWriteResult(
                RoleWriteStatus.Success,
                new RoleResponse { Id = RoleId, Code = "STAFF" })
        };
        var service = new RoleService(repository);

        var result = await service.CreateAsync(
            new CreateRoleRequest { Code = " staff ", Name = " Staff " },
            ActorId,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("STAFF", repository.CreatedRequest!.Code);
        Assert.Empty(repository.CreatedRequest.PermissionIds);
        Assert.Equal(ActorId, repository.ActorId);
    }

    [Fact]
    public async Task Role_update_passes_empty_permission_set_as_replace_all()
    {
        var repository = new FakeRoleRepository
        {
            UpdateResult = new RoleWriteResult(
                RoleWriteStatus.Success,
                new RoleResponse { Id = RoleId, Permissions = Array.Empty<PermissionSummaryResponse>() })
        };
        var service = new RoleService(repository);

        var result = await service.UpdateAsync(
            RoleId,
            new UpdateRoleRequest
            {
                Code = "STAFF",
                Name = "Staff",
                PermissionIds = Array.Empty<Guid>()
            },
            ActorId,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(repository.UpdatedRequest!.PermissionIds);
        Assert.Empty(result.Value.Permissions);
    }

    private sealed class FakePermissionRepository : IPermissionRepository
    {
        public CreatePermissionRequest? CreatedRequest { get; private set; }

        public PermissionDeleteStatus DeleteStatus { get; init; } = PermissionDeleteStatus.Deleted;

        public Task<PagedResult<PermissionResponse>> SearchAsync(
            PermissionSearchRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(PagedResult<PermissionResponse>.Create([], request.PageNumber, request.PageSize, 0));

        public Task<PermissionResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult<PermissionResponse?>(null);

        public Task<bool> ExistsByCodeAsync(string code, CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task<PermissionWriteResult> CreateAsync(
            CreatePermissionRequest request,
            CancellationToken cancellationToken)
        {
            CreatedRequest = request;
            return Task.FromResult(new PermissionWriteResult(
                PermissionWriteStatus.Success,
                new PermissionResponse { Code = request.Code, Name = request.Name }));
        }

        public Task<PermissionWriteResult> UpdateAsync(
            Guid id,
            UpdatePermissionRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new PermissionWriteResult(
                PermissionWriteStatus.Success,
                new PermissionResponse { Id = id, Code = "court.view", Name = request.Name }));

        public Task<PermissionDeleteStatus> DeleteAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(DeleteStatus);
    }

    private sealed class FakeRoleRepository : IRoleRepository
    {
        public IReadOnlyList<RoleResponse> SearchResult { get; init; } = Array.Empty<RoleResponse>();

        public RoleSearchRequest? SearchRequest { get; private set; }

        public CreateRoleRequest? CreatedRequest { get; private set; }

        public UpdateRoleRequest? UpdatedRequest { get; private set; }

        public Guid? ActorId { get; private set; }

        public RoleWriteResult CreateResult { get; init; } =
            new(RoleWriteStatus.Success, new RoleResponse { Id = RoleId });

        public RoleWriteResult UpdateResult { get; init; } =
            new(RoleWriteStatus.Success, new RoleResponse { Id = RoleId });

        public async Task<PagedResult<RoleResponse>> SearchAsync(
            RoleSearchRequest request,
            CancellationToken cancellationToken)
        {
            SearchRequest = request;
            await Task.Yield();
            return PagedResult<RoleResponse>.Create(SearchResult, request.Page, request.PageSize, SearchResult.Count);
        }

        public Task<Guid?> GetRoleIdByCodeAsync(string roleCode, CancellationToken cancellationToken) =>
            Task.FromResult<Guid?>(null);

        public Task<RoleResponse?> GetByIdWithPermissionsAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult<RoleResponse?>(null);

        public Task<bool> ExistsByCodeAsync(
            string code,
            Guid? excludeId,
            CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task<RoleWriteResult> CreateAsync(
            CreateRoleRequest request,
            Guid? actorId,
            CancellationToken cancellationToken)
        {
            CreatedRequest = request;
            ActorId = actorId;
            return Task.FromResult(CreateResult);
        }

        public Task<RoleWriteResult> UpdateAsync(
            Guid id,
            UpdateRoleRequest request,
            Guid? actorId,
            CancellationToken cancellationToken)
        {
            UpdatedRequest = request;
            ActorId = actorId;
            return Task.FromResult(UpdateResult);
        }

        public Task<RoleDeleteStatus> DeleteAsync(Guid id, Guid? actorId, CancellationToken cancellationToken) =>
            Task.FromResult(RoleDeleteStatus.Deleted);
    }
}
