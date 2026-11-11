using CourtBookingManagement.Api.Common.Responses;
using CourtBookingManagement.Application.Auth.Interfaces;
using CourtBookingManagement.Application.Catalog.Models.Requests;
using CourtBookingManagement.Application.Catalog.Models.Responses;
using CourtBookingManagement.Application.Catalog.Services;
using CourtBookingManagement.Infrastructure.Auth;
using Microsoft.AspNetCore.Mvc;

namespace CourtBookingManagement.Api.Controllers;

/// <summary>Manage product availability and pricing for an authorized branch.</summary>
[ApiController]
[Route("api/branches/{branchId:guid}/products")]
public sealed class BranchProductsController(
    IBranchProductService service,
    ICurrentUserService currentUserService) : ApiControllerBase
{
    [HttpGet]
    [Permission("product.view")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<BranchProductResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetProducts(
        Guid branchId,
        [FromQuery] bool? isAvailableForSale,
        CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not Guid userId)
        {
            return Unauthorized();
        }

        var result = await service.GetProductsAsync(userId, branchId, isAvailableForSale, cancellationToken);
        return result.IsSuccess
            ? Success(result.Value, "Branch products retrieved successfully.")
            : Error(result.Error);
    }

    [HttpGet("{productId:guid}", Name = nameof(GetProduct))]
    [Permission("product.view")]
    [ProducesResponseType(typeof(ApiResponse<BranchProductResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProduct(
        Guid branchId,
        Guid productId,
        CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not Guid userId)
        {
            return Unauthorized();
        }

        var result = await service.GetProductAsync(userId, branchId, productId, cancellationToken);
        return FromResult(result, "Branch product retrieved successfully.");
    }

    [HttpPut("{productId:guid}")]
    [Permission("product.update")]
    [ProducesResponseType(typeof(ApiResponse<BranchProductResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ConfigureProduct(
        Guid branchId,
        Guid productId,
        [FromBody] BranchProductConfigurationRequest request,
        CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not Guid userId)
        {
            return Unauthorized();
        }

        var result = await service.ConfigureProductAsync(
            userId,
            branchId,
            productId,
            request,
            cancellationToken);
        return FromResult(result, "Branch product configuration updated successfully.");
    }

    [HttpPost("bulk-configure")]
    [Permission("product.update")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<BranchProductResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> BulkConfigure(
        Guid branchId,
        [FromBody] BulkBranchProductConfigurationRequest request,
        CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not Guid userId)
        {
            return Unauthorized();
        }

        var result = await service.BulkConfigureAsync(userId, branchId, request, cancellationToken);
        return result.IsSuccess
            ? Success(result.Value, "Branch products configured successfully.")
            : Error(result.Error);
    }
}
