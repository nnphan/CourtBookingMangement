using CourtBookingManagement.Api.Common.Responses;
using CourtBookingManagement.Application.Auth.Interfaces;
using CourtBookingManagement.Application.Catalog.Models.Requests;
using CourtBookingManagement.Application.Catalog.Models.Responses;
using CourtBookingManagement.Application.Catalog.Services;
using CourtBookingManagement.Infrastructure.Auth;
using Microsoft.AspNetCore.Mvc;

namespace CourtBookingManagement.Api.Controllers;

/// <summary>Search and manage products in the shared catalog.</summary>
[ApiController]
[Route("api/products")]
public sealed class ProductsController(
    ICatalogService service,
    ICurrentUserService currentUserService) : ApiControllerBase
{
    [HttpGet]
    [Permission("product.view")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ProductResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search(
        [FromQuery] ProductSearchRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.SearchProductsAsync(request, cancellationToken);
        return result.IsSuccess
            ? Success(result.Value.Items, "Products retrieved successfully.", result.Value.Metadata)
            : Error(result.Error);
    }

    [HttpGet("{id:guid}", Name = nameof(GetById))]
    [Permission("product.view")]
    [ProducesResponseType(typeof(ApiResponse<ProductResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await service.GetProductAsync(id, cancellationToken);
        return FromResult(result, "Product retrieved successfully.");
    }

    [HttpPost]
    [Permission("product.create")]
    [ProducesResponseType(typeof(ApiResponse<ProductResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreateProductRequest request,
        CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not Guid userId)
        {
            return Unauthorized();
        }

        var result = await service.CreateProductAsync(request, userId, cancellationToken);
        return result.IsSuccess
            ? Created(result.Value, nameof(GetById), new { id = result.Value.Id }, "Product created successfully.")
            : Error(result.Error);
    }

    [HttpPut("{id:guid}")]
    [Permission("product.update")]
    [ProducesResponseType(typeof(ApiResponse<ProductResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateProductRequest request,
        CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not Guid userId)
        {
            return Unauthorized();
        }

        var result = await service.UpdateProductAsync(id, request, userId, cancellationToken);
        return FromResult(result, "Product updated successfully.");
    }

    [HttpDelete("{id:guid}")]
    [Permission("product.deactivate")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not Guid userId)
        {
            return Unauthorized();
        }

        var result = await service.DeactivateProductAsync(id, userId, cancellationToken);
        return FromResult(result, "Product deactivated successfully.");
    }
}
