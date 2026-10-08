using CourtBookingManagement.Api.Common.Constants;
using CourtBookingManagement.Api.Common.Responses;
using CourtBookingManagement.Application.Amenities.Models.Responses;
using CourtBookingManagement.Application.Amenities.Services;
using CourtBookingManagement.Application.Matching.Models;
using CourtBookingManagement.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CourtBookingManagement.Api.Controllers;

/// <summary>Provides the available amenities.</summary>
[ApiController]
[Route("api/amenities")]
public sealed class AmenitiesController(IAmenityService service) : ApiControllerBase
{
    /// <summary>Gets all amenities ordered by name.</summary>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<AmenityResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetAll(
        CancellationToken cancellationToken)
    {
        var amenities = await service.GetAllAsync(cancellationToken);
        var metadata = PagedResult<AmenityResponse>.Create(
            amenities,
            page: 1,
            pageSize: Math.Max(12, amenities.Count),
            totalCount: amenities.Count).Metadata;

        return Success(amenities, "Success", metadata);
    }
}