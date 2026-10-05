using System.Net;
using System.Text.Json;
using CourtBookingManagement.Application.Branches.DTOs;
using CourtBookingManagement.Application.Branches.GetBranchDetails;
using CourtBookingManagement.Application.Branches.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace CourtBookingManagement.Api.Tests;

public sealed class GetBranchDetailsTests
{
    [Fact]
    public async Task Handler_returns_all_branch_details_when_branch_exists()
    {
        var expected = CreateBranchDetails();
        var handler = new GetBranchDetailsQueryHandler(new StubBranchQueryRepository(expected));

        var result = await handler.Handle(new GetBranchDetailsQuery(expected.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(expected.Id, result.Value.Id);
        Assert.Single(result.Value.Images);
        Assert.Single(result.Value.Amenities);
        Assert.Single(result.Value.Courts);
        Assert.Single(result.Value.OperatingHours);
        Assert.Single(result.Value.Pricings);
        Assert.Equal(1, result.Value.Statistics.TotalCourts);
    }

    [Fact]
    public async Task Handler_returns_not_found_when_branch_does_not_exist()
    {
        var handler = new GetBranchDetailsQueryHandler(new StubBranchQueryRepository(null));

        var result = await handler.Handle(new GetBranchDetailsQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Branch.NotFound", result.Error.Code);
        Assert.Equal("Branch not found.", result.Error.Description);
    }

    [Fact]
    public void Validator_rejects_an_empty_id()
    {
        var validator = new GetBranchDetailsQueryValidator();

        var result = validator.Validate(new GetBranchDetailsQuery(Guid.Empty));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(GetBranchDetailsQuery.Id));
    }

    [Fact]
    public async Task Get_branch_details_returns_success_envelope_with_nested_collections()
    {
        var details = CreateBranchDetails();
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting(WebHostDefaults.EnvironmentKey, "Testing");
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Database:ConnectionString"] = "Host=localhost;Database=branch_details_test;Username=test;Password=test",
                    ["Jwt:SecretKey"] = "test-secret-key-that-is-at-least-32-characters-long",
                    ["Jwt:Issuer"] = "tests",
                    ["Jwt:Audience"] = "tests"
                }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IBranchQueryRepository>();
                services.AddSingleton<IBranchQueryRepository>(new StubBranchQueryRepository(details));
            });
        });

        using var client = factory.CreateClient();
        using var response = await client.GetAsync($"/api/branches/{details.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = payload.RootElement;
        Assert.True(root.GetProperty("success").GetBoolean());
        Assert.Equal("Branch details retrieved successfully.", root.GetProperty("message").GetString());

        var data = root.GetProperty("data");
        Assert.Equal(details.Id, data.GetProperty("id").GetGuid());
        Assert.Single(data.GetProperty("images").EnumerateArray());
        Assert.Single(data.GetProperty("amenities").EnumerateArray());
        Assert.Single(data.GetProperty("courts").EnumerateArray());
        Assert.Single(data.GetProperty("operatingHours").EnumerateArray());
        Assert.Single(data.GetProperty("pricings").EnumerateArray());
    }

    private static BranchDetailsResponse CreateBranchDetails() => new()
    {
        Id = Guid.NewGuid(),
        OwnerId = Guid.NewGuid(),
        Name = "Central Badminton",
        Address = "1 Court Street",
        City = "Central",
        District = "District 1",
        Latitude = 10.123456m,
        Longitude = 106.123456m,
        PhoneNumber = "0123456789",
        IsActive = true,
        TimeZone = "Asia/Ho_Chi_Minh",
        SupportsInstantBooking = true,
        Images = [new BranchImageResponse { Id = Guid.NewGuid(), ImageUrl = "https://example.test/branch.jpg", SortOrder = 1 }],
        Amenities = [new AmenityResponse { Id = Guid.NewGuid(), Code = "PARKING", Name = "Parking", Icon = "parking" }],
        Courts = [new CourtResponse { Id = Guid.NewGuid(), CourtNumber = 1, Name = "Court 1", Status = "active" }],
        OperatingHours = [new OperatingHourResponse { Id = Guid.NewGuid(), OpenTime = TimeSpan.FromHours(6), CloseTime = TimeSpan.FromHours(22) }],
        Pricings = [new BranchPricingResponse { Id = Guid.NewGuid(), PricingType = "standard", StartTime = TimeSpan.FromHours(6), EndTime = TimeSpan.FromHours(22), PricePerHour = 100000m }],
        Statistics = new BranchStatisticsResponse { TotalCourts = 1, TotalAmenities = 1, TotalImages = 1, MinPrice = 100000m, MaxPrice = 100000m }
    };

    private sealed class StubBranchQueryRepository(BranchDetailsResponse? response) : IBranchQueryRepository
    {
        public Task<BranchDetailsResponse?> GetBranchDetailsAsync(Guid branchId, CancellationToken cancellationToken) =>
            Task.FromResult(response?.Id == branchId ? response : null);
    }
}