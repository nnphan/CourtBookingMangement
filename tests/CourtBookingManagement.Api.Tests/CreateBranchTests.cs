using System.Data;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Encodings.Web;
using CourtBookingManagement.Application.Branches.DTOs;
using CourtBookingManagement.Application.Branches.DTOs.Requests;
using CourtBookingManagement.Application.Branches.DTOs.Responses;
using CourtBookingManagement.Application.Branches.Interfaces;
using CourtBookingManagement.Application.Branches.Services;
using CourtBookingManagement.Application.Branches.Validators;
using CourtBookingManagement.Application.Abstractions.Clock;
using CourtBookingManagement.Application.Abstractions.Data;
using CourtBookingManagement.Domain.Abstractions;
using CourtBookingManagement.Infrastructure.Persistence;
using CourtBookingManagement.Infrastructure.Persistence.Entities;
using FluentValidation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CourtBookingManagement.Api.Tests;

public sealed class CreateBranchTests
{
    [Fact]
    public void Ef_model_maps_branch_amenities_and_multiple_operating_hours()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=model_test;Username=test;Password=test")
            .Options;
        using var dbContext = new ApplicationDbContext(options, new TestClock());

        var branchType = dbContext.Model.FindEntityType(typeof(Branch))!;
        var amenityNavigation = branchType.GetSkipNavigations()
            .Single(navigation => navigation.Name == nameof(Branch.Amenities));
        var operatingHourType = dbContext.Model.FindEntityType(typeof(OperatingHour))!;

        Assert.Equal("core", amenityNavigation.JoinEntityType.GetSchema());
        Assert.Equal("branches_amenities", amenityNavigation.JoinEntityType.GetTableName());
        Assert.DoesNotContain(
            operatingHourType.GetIndexes(),
            index => index.IsUnique && index.Properties.Any(property => property.Name == nameof(OperatingHour.BranchId)));
        Assert.NotNull(branchType.FindNavigation(nameof(Branch.OperatingHours)));
    }

    [Fact]
    public async Task Create_saves_branch_images_amenities_and_default_operating_hours()
    {
        var ownerId = Guid.NewGuid();
        var image = new CreateBranchImageRequest { ImageUrl = "https://example.test/court.jpg", SortOrder = 2 };
        var branchRepository = new StubBranchRepository(ownerId);
        var amenityRepository = new StubAmenityRepository([AmenityId]);
        var unitOfWork = new StubUnitOfWork();
        var service = CreateService(branchRepository, amenityRepository, unitOfWork);
        var request = ValidRequest();
        request.Images = [image];
        request.AmenityIds = [AmenityId];

        var result = await service.CreateAsync(
            request,
            UserId,
            CancellationToken.None);

        Assert.Equal(branchRepository.BranchId, result.Id);
        Assert.Equal("North Court", result.Name);
        Assert.True(result.IsActive);
        Assert.Equal(1, unitOfWork.TransactionCount);
        Assert.True(branchRepository.Saved);
        Assert.Equal(ownerId, branchRepository.AddedBranch!.OwnerId);
        Assert.Equal(UserId, branchRepository.AddedBranch.CreatedByUserId);
        Assert.Equal([AmenityId], branchRepository.AddedBranch.AmenityIds);
        Assert.Equal([image], branchRepository.AddedBranch.Images);
        var hours = Assert.Single(branchRepository.AddedBranch.OperatingHours);
        Assert.Equal(TimeSpan.FromHours(6), hours.OpenTime);
        Assert.Equal(TimeSpan.FromHours(22), hours.CloseTime);
        Assert.False(hours.IsClosed);
    }

    [Fact]
    public async Task Create_rejects_duplicate_branch_name_before_opening_transaction()
    {
        var branchRepository = new StubBranchRepository(Guid.NewGuid()) { BranchExists = true };
        var unitOfWork = new StubUnitOfWork();
        var service = CreateService(branchRepository, new StubAmenityRepository([]), unitOfWork);

        await Assert.ThrowsAsync<BranchAlreadyExistsException>(() =>
            service.CreateAsync(ValidRequest(), UserId, CancellationToken.None));

        Assert.Equal(0, unitOfWork.TransactionCount);
        Assert.Null(branchRepository.AddedBranch);
    }

    [Fact]
    public async Task Create_rejects_unknown_amenity_ids_before_opening_transaction()
    {
        var branchRepository = new StubBranchRepository(Guid.NewGuid());
        var unitOfWork = new StubUnitOfWork();
        var service = CreateService(branchRepository, new StubAmenityRepository([]), unitOfWork);
        var request = ValidRequest();
        request.AmenityIds = [AmenityId];

        await Assert.ThrowsAsync<InvalidBranchAmenitiesException>(() =>
            service.CreateAsync(request, UserId, CancellationToken.None));

        Assert.Equal(0, unitOfWork.TransactionCount);
        Assert.Null(branchRepository.AddedBranch);
    }

    [Fact]
    public async Task Create_rejects_invalid_request()
    {
        var branchRepository = new StubBranchRepository(Guid.NewGuid());
        var unitOfWork = new StubUnitOfWork();
        var service = CreateService(branchRepository, new StubAmenityRepository([]), unitOfWork);
        var request = ValidRequest();
        request.Name = " ";

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            service.CreateAsync(request, UserId, CancellationToken.None));

        Assert.Contains(exception.Errors, error => error.PropertyName == nameof(CreateBranchRequest.Name));
        Assert.Equal(0, unitOfWork.TransactionCount);
    }

    [Fact]
    public async Task Post_branches_returns_created_success_response()
    {
        var expected = new CreateBranchResponse { Id = Guid.NewGuid(), Name = "North Court", IsActive = true };
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting(WebHostDefaults.EnvironmentKey, "Testing");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Database:ConnectionString"] = "Host=localhost;Database=branch_api_test;Username=test;Password=test",
                    ["Jwt:SecretKey"] = "test-secret-key-that-is-at-least-32-characters-long",
                    ["Jwt:Issuer"] = "tests",
                    ["Jwt:Audience"] = "tests"
                }));
            builder.ConfigureTestServices(services =>
            {
                services.AddAuthentication(options =>
                    {
                        options.DefaultAuthenticateScheme = "Test";
                        options.DefaultChallengeScheme = "Test";
                    })
                    .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("Test", _ => { });
                services.RemoveAll<IBranchService>();
                services.AddSingleton<IBranchService>(new StubBranchService(expected));
            });
        });

        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/branches", ValidRequest());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = payload.RootElement;
        Assert.True(root.GetProperty("success").GetBoolean());
        Assert.Equal("Branch created successfully.", root.GetProperty("message").GetString());
        var data = root.GetProperty("data");
        Assert.Equal(expected.Id, data.GetProperty("id").GetGuid());
        Assert.Equal(expected.Name, data.GetProperty("name").GetString());
        Assert.True(data.GetProperty("isActive").GetBoolean());
    }

    private static IBranchService CreateService(
        StubBranchRepository branchRepository,
        IAmenityRepository amenityRepository,
        IUnitOfWork unitOfWork) =>
        new BranchService(
            branchRepository,
            amenityRepository,
            unitOfWork,
            new CreateBranchRequestValidator(),
            new StubSqlConnectionFactory());

    private static CreateBranchRequest ValidRequest() => new()
    {
        Name = "North Court",
        Address = "12 Sports Avenue",
        TimeZone = "Asia/Ho_Chi_Minh"
    };

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid AmenityId = Guid.NewGuid();

    private sealed class StubBranchRepository(Guid ownerId) : IBranchRepository
    {
        public bool BranchExists { get; init; }
        public bool Saved { get; private set; }
        public Guid BranchId { get; } = Guid.NewGuid();
        public CreateBranchData? AddedBranch { get; private set; }

        public Task<Guid?> GetOwnerIdForUserAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<Guid?>(userId == UserId ? ownerId : null);

        public Task<bool> ExistsAsync(Guid requestedOwnerId, string name, CancellationToken cancellationToken) =>
            Task.FromResult(BranchExists);

        public Task<Guid> AddAsync(CreateBranchData branch, CancellationToken cancellationToken)
        {
            AddedBranch = branch;
            return Task.FromResult(BranchId);
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            Saved = true;
            return Task.CompletedTask;
        }

        public Task<bool?> IsDeletedAsync(Guid branchId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> SoftDeleteAsync(
            Guid branchId,
            Guid deletedBy,
            IDbTransaction transaction,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class StubSqlConnectionFactory : ISqlConnectionFactory
    {
        public IDbConnection CreateConnection() => throw new NotSupportedException();
    }

    private sealed class TestClock : IDateTimeProvider
    {
        public DateTime UtcNow => DateTime.UtcNow;
    }

    private sealed class StubAmenityRepository(IReadOnlyCollection<Guid> existingIds) : IAmenityRepository
    {
        public Task<List<Guid>> GetExistingIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken) =>
            Task.FromResult(existingIds.Where(ids.Contains).ToList());
    }

    private sealed class StubUnitOfWork : IUnitOfWork
    {
        public int TransactionCount { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);

        public Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default)
        {
            TransactionCount++;
            return operation(cancellationToken);
        }
    }

    private sealed class StubBranchService(CreateBranchResponse response) : IBranchService
    {
        public Task<CreateBranchResponse> CreateAsync(
            CreateBranchRequest request,
            Guid ownerId,
            CancellationToken cancellationToken) => Task.FromResult(response);

        public Task<Result> DeleteAsync(
            Guid branchId,
            Guid deletedBy,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, UserId.ToString())], Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}