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
using CourtBookingManagement.Application.Branches.UpdateBranch;
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
        var pricingType = dbContext.Model.FindEntityType(typeof(BranchPricing))!;

        Assert.Equal("core", amenityNavigation.JoinEntityType.GetSchema());
        Assert.Equal("branches_amenities", amenityNavigation.JoinEntityType.GetTableName());
        Assert.DoesNotContain(
            operatingHourType.GetIndexes(),
            index => index.IsUnique && index.Properties.Any(property => property.Name == nameof(OperatingHour.BranchId)));
        Assert.NotNull(branchType.FindNavigation(nameof(Branch.OperatingHours)));
        Assert.NotNull(branchType.FindNavigation(nameof(Branch.BranchPricings)));
        Assert.Equal("branch_pricings", pricingType.GetTableName());
        Assert.Equal("description", branchType.FindProperty(nameof(Branch.Description))!.GetColumnName());
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
        request.Description = "Competition-standard courts.";
        request.Courts = [new CreateCourtRequest { CourtNumber = 1, Name = "Court 1" }];
        request.BranchPricings = [new CreateBranchPricingRequest
        {
            PricingType = "NORMAL",
            StartTime = TimeSpan.FromHours(5),
            EndTime = TimeSpan.FromHours(17),
            PricePerHour = 80000
        }];

        var result = await service.CreateAsync(
            request,
            UserId,
            CancellationToken.None);

        Assert.Equal(branchRepository.BranchId, result.Id);
        Assert.Equal(1, unitOfWork.TransactionCount);
        Assert.True(branchRepository.Saved);
        Assert.Equal(ownerId, branchRepository.AddedBranch!.OwnerId);
        Assert.Equal(UserId, branchRepository.AddedBranch.CreatedByUserId);
        Assert.Equal(request.Description, branchRepository.AddedBranch.Description);
        Assert.Equal([AmenityId], branchRepository.AddedBranch.AmenityIds);
        Assert.Equal([image], branchRepository.AddedBranch.Images);
        Assert.Equal(request.Courts, branchRepository.AddedBranch.Courts);
        Assert.Equal(request.BranchPricings, branchRepository.AddedBranch.BranchPricings);
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
    public async Task Create_rejects_duplicate_court_numbers()
    {
        var branchRepository = new StubBranchRepository(Guid.NewGuid());
        var unitOfWork = new StubUnitOfWork();
        var service = CreateService(branchRepository, new StubAmenityRepository([]), unitOfWork);
        var request = ValidRequest();
        request.Courts =
        [
            new CreateCourtRequest { CourtNumber = 1, Name = "Court 1" },
            new CreateCourtRequest { CourtNumber = 1, Name = "Court 2" }
        ];

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            service.CreateAsync(request, UserId, CancellationToken.None));

        Assert.Contains(exception.Errors, error => error.ErrorMessage.Contains("unique", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(0, unitOfWork.TransactionCount);
    }

    [Fact]
    public async Task Create_rejects_invalid_pricing_type()
    {
        var branchRepository = new StubBranchRepository(Guid.NewGuid());
        var service = CreateService(branchRepository, new StubAmenityRepository([]), new StubUnitOfWork());
        var request = ValidRequest();
        request.BranchPricings = [new CreateBranchPricingRequest
        {
            PricingType = "HOLIDAY",
            StartTime = TimeSpan.FromHours(5),
            EndTime = TimeSpan.FromHours(17),
            PricePerHour = 80000
        }];

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            service.CreateAsync(request, UserId, CancellationToken.None));

        Assert.Contains(exception.Errors, error => error.PropertyName.Contains("PricingType", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Create_rejects_invalid_pricing_time_range()
    {
        var branchRepository = new StubBranchRepository(Guid.NewGuid());
        var service = CreateService(branchRepository, new StubAmenityRepository([]), new StubUnitOfWork());
        var request = ValidRequest();
        request.BranchPricings = [new CreateBranchPricingRequest
        {
            PricingType = "NORMAL",
            StartTime = TimeSpan.FromHours(17),
            EndTime = TimeSpan.FromHours(5),
            PricePerHour = 80000
        }];

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            service.CreateAsync(request, UserId, CancellationToken.None));

        Assert.Contains(exception.Errors, error => error.PropertyName.Contains("EndTime", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Create_rolls_back_when_court_insert_fails()
    {
        var branchRepository = new StubBranchRepository(Guid.NewGuid()) { FailCourtInsert = true };
        var unitOfWork = new StubUnitOfWork();
        var service = CreateService(branchRepository, new StubAmenityRepository([]), unitOfWork);
        var request = ValidRequest();
        request.Courts = [new CreateCourtRequest { CourtNumber = 1, Name = "Court 1" }];

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(request, UserId, CancellationToken.None));

        Assert.Equal(1, unitOfWork.RollbackCount);
    }

    [Fact]
    public async Task Create_rolls_back_when_pricing_insert_fails()
    {
        var branchRepository = new StubBranchRepository(Guid.NewGuid()) { FailPricingInsert = true };
        var unitOfWork = new StubUnitOfWork();
        var service = CreateService(branchRepository, new StubAmenityRepository([]), unitOfWork);
        var request = ValidRequest();
        request.BranchPricings = [new CreateBranchPricingRequest
        {
            PricingType = "NORMAL",
            StartTime = TimeSpan.FromHours(5),
            EndTime = TimeSpan.FromHours(17),
            PricePerHour = 80000
        }];

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(request, UserId, CancellationToken.None));

        Assert.Equal(1, unitOfWork.RollbackCount);
    }

    [Fact]
    public async Task Update_replaces_branch_data_successfully()
    {
        var branchRepository = new StubBranchRepository(Guid.NewGuid());
        var unitOfWork = new StubUnitOfWork();
        var handler = new UpdateBranchCommandHandler(
            branchRepository,
            new StubAmenityRepository([AmenityId]),
            unitOfWork);
        var request = ValidRequest();
        request.AmenityIds = [AmenityId];
        request.Images = [new CreateBranchImageRequest { ImageUrl = "https://example.test/new.jpg", SortOrder = 0 }];
        request.Courts = [new CreateCourtRequest { CourtNumber = 1, Name = "Court 1" }];
        request.BranchPricings = [new CreateBranchPricingRequest
        {
            PricingType = "NORMAL",
            StartTime = TimeSpan.FromHours(5),
            EndTime = TimeSpan.FromHours(17),
            PricePerHour = 80000
        }];
        var command = new UpdateBranchCommand(branchRepository.BranchId, request, UserId);

        var validation = await new UpdateBranchCommandValidator().ValidateAsync(command);
        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(validation.IsValid);
        Assert.True(result.IsSuccess);
        Assert.Equal(branchRepository.BranchId, result.Value.Id);
        Assert.Equal(1, unitOfWork.TransactionCount);
        Assert.Equal(request.Name, branchRepository.UpdatedBranch!.Name);
        Assert.Equal([AmenityId], branchRepository.UpdatedBranch.AmenityIds);
        Assert.Equal(request.Images, branchRepository.UpdatedBranch.Images);
        Assert.Equal(request.Courts, branchRepository.UpdatedBranch.Courts);
        Assert.Equal(request.BranchPricings, branchRepository.UpdatedBranch.BranchPricings);
        Assert.Equal(UserId, branchRepository.UpdatedBy);
    }

    [Fact]
    public async Task Update_returns_not_found_for_unknown_branch()
    {
        var branchRepository = new StubBranchRepository(Guid.NewGuid()) { UpdateBranchExists = false };
        var handler = new UpdateBranchCommandHandler(
            branchRepository,
            new StubAmenityRepository([]),
            new StubUnitOfWork());

        var result = await handler.Handle(
            new UpdateBranchCommand(branchRepository.BranchId, ValidRequest(), UserId),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Branch.NotFound", result.Error.Code);
        Assert.Equal("Branch not found", result.Error.Description);
    }

    [Fact]
    public async Task Update_validator_rejects_duplicate_court_numbers()
    {
        var request = ValidRequest();
        request.Courts =
        [
            new CreateCourtRequest { CourtNumber = 2, Name = "Court 2" },
            new CreateCourtRequest { CourtNumber = 2, Name = "Court 2B" }
        ];
        var result = await new UpdateBranchCommandValidator().ValidateAsync(
            new UpdateBranchCommand(Guid.NewGuid(), request, UserId));

        Assert.Contains(result.Errors, error => error.ErrorMessage.Contains("unique", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Update_validator_rejects_invalid_pricing_type()
    {
        var request = ValidRequest();
        request.BranchPricings = [new CreateBranchPricingRequest
        {
            PricingType = "HOLIDAY",
            StartTime = TimeSpan.FromHours(5),
            EndTime = TimeSpan.FromHours(17),
            PricePerHour = 80000
        }];
        var result = await new UpdateBranchCommandValidator().ValidateAsync(
            new UpdateBranchCommand(Guid.NewGuid(), request, UserId));

        Assert.Contains(result.Errors, error => error.PropertyName.Contains("PricingType", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Update_rolls_back_when_image_insert_fails()
    {
        var branchRepository = new StubBranchRepository(Guid.NewGuid()) { FailImageInsert = true };
        var unitOfWork = new StubUnitOfWork();
        var handler = CreateUpdateHandler(branchRepository, unitOfWork);
        var request = ValidRequest();
        request.Images = [new CreateBranchImageRequest { ImageUrl = "https://example.test/new.jpg", SortOrder = 0 }];

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(
            new UpdateBranchCommand(branchRepository.BranchId, request, UserId), CancellationToken.None));

        Assert.Equal(1, unitOfWork.RollbackCount);
    }

    [Fact]
    public async Task Update_rolls_back_when_pricing_insert_fails()
    {
        var branchRepository = new StubBranchRepository(Guid.NewGuid()) { FailPricingInsert = true };
        var unitOfWork = new StubUnitOfWork();
        var handler = CreateUpdateHandler(branchRepository, unitOfWork);
        var request = ValidRequest();
        request.BranchPricings = [new CreateBranchPricingRequest
        {
            PricingType = "NORMAL",
            StartTime = TimeSpan.FromHours(5),
            EndTime = TimeSpan.FromHours(17),
            PricePerHour = 80000
        }];

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(
            new UpdateBranchCommand(branchRepository.BranchId, request, UserId), CancellationToken.None));

        Assert.Equal(1, unitOfWork.RollbackCount);
    }

    [Fact]
    public async Task Update_rolls_back_when_court_insert_fails()
    {
        var branchRepository = new StubBranchRepository(Guid.NewGuid()) { FailCourtInsert = true };
        var unitOfWork = new StubUnitOfWork();
        var handler = CreateUpdateHandler(branchRepository, unitOfWork);
        var request = ValidRequest();
        request.Courts = [new CreateCourtRequest { CourtNumber = 1, Name = "Court 1" }];

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(
            new UpdateBranchCommand(branchRepository.BranchId, request, UserId), CancellationToken.None));

        Assert.Equal(1, unitOfWork.RollbackCount);
    }

    [Fact]
    public async Task Branch_endpoints_enforce_permissions_and_post_returns_created_response()
    {
        var expected = new CreateBranchResponse { Id = Guid.NewGuid() };
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
        Assert.Single(data.EnumerateObject());

        using var updateResponse = await client.PutAsJsonAsync(
            $"/api/branches/{expected.Id}",
            ValidRequest());
        Assert.Equal(HttpStatusCode.Forbidden, updateResponse.StatusCode);
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

    private static UpdateBranchCommandHandler CreateUpdateHandler(
        StubBranchRepository branchRepository,
        IUnitOfWork unitOfWork) =>
        new(branchRepository, new StubAmenityRepository([]), unitOfWork);

    private static CreateBranchRequest ValidRequest() => new()
    {
        Name = "North Court",
        Address = "12 Sports Avenue",
        City = "Ho Chi Minh City",
        District = "District 7",
        PhoneNumber = "0900000000",
        TimeZone = "Asia/Ho_Chi_Minh"
    };

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid AmenityId = Guid.NewGuid();

    private sealed class StubBranchRepository(Guid ownerId) : IBranchRepository
    {
        public bool BranchExists { get; init; }
        public bool FailCourtInsert { get; init; }
        public bool FailPricingInsert { get; init; }
        public bool FailImageInsert { get; init; }
        public bool UpdateBranchExists { get; init; } = true;
        public bool Saved { get; private set; }
        public Guid BranchId { get; } = Guid.NewGuid();
        public CreateBranchData? AddedBranch { get; private set; }
        public UpdateBranchData? UpdatedBranch { get; private set; }
        public Guid? UpdatedBy { get; private set; }

        public Task<Guid?> GetOwnerIdForUserAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<Guid?>(userId == UserId ? ownerId : null);

        public Task<bool> ExistsAsync(Guid requestedOwnerId, string name, CancellationToken cancellationToken) =>
            Task.FromResult(BranchExists);

        public Task<bool> ExistsByIdAsync(Guid branchId, CancellationToken cancellationToken) =>
            Task.FromResult(UpdateBranchExists && branchId == BranchId);

        public Task<Guid> AddAsync(CreateBranchData branch, CancellationToken cancellationToken)
        {
            AddedBranch = branch;
            return Task.FromResult(BranchId);
        }

        public Task<bool> UpdateAsync(
            Guid branchId,
            UpdateBranchData branch,
            Guid updatedBy,
            CancellationToken cancellationToken)
        {
            if (!UpdateBranchExists || branchId != BranchId)
            {
                return Task.FromResult(false);
            }

            UpdatedBranch = branch;
            UpdatedBy = updatedBy;
            if ((FailImageInsert && branch.Images.Count > 0)
                || (FailCourtInsert && branch.Courts.Count > 0)
                || (FailPricingInsert && branch.BranchPricings.Count > 0))
            {
                throw new InvalidOperationException("Simulated replacement insert failure.");
            }

            return Task.FromResult(true);
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            if ((FailCourtInsert && AddedBranch?.Courts.Count > 0)
                || (FailPricingInsert && AddedBranch?.BranchPricings.Count > 0))
            {
                throw new InvalidOperationException("Simulated child insert failure.");
            }

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
        public int RollbackCount { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);

        public Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default)
        {
            TransactionCount++;
            return ExecuteAsync(operation, cancellationToken);
        }

        private async Task ExecuteAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken)
        {
            try
            {
                await operation(cancellationToken);
            }
            catch
            {
                RollbackCount++;
                throw;
            }
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
            var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, UserId.ToString()),
                new Claim("permission", "branch.create")
            ], Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}