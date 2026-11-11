using CourtBookingManagement.Api.Common.Responses;
using CourtBookingManagement.Application.Auth.Interfaces;
using CourtBookingManagement.Application.Catalog.Models.Requests;
using CourtBookingManagement.Application.Catalog.Models.Responses;
using CourtBookingManagement.Application.Catalog.Services;
using CourtBookingManagement.Infrastructure.Auth;
using Microsoft.AspNetCore.Mvc;

namespace CourtBookingManagement.Api.Controllers;

/// <summary>Manage shared product categories.</summary>
[ApiController]
[Route("api/product-categories")]
public sealed class ProductCategoriesController(
    ICatalogService service,
    ICurrentUserService currentUserService) : ApiControllerBase
{
    [HttpGet]
    [Permission("product.view")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ProductCategoryResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search(
        [FromQuery] CategorySearchRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.SearchCategoriesAsync(request, cancellationToken);
        return result.IsSuccess
            ? Success(result.Value, "Product categories retrieved successfully.")
            : Error(result.Error);
    }

    [HttpGet("{id:guid}", Name = nameof(GetById))]
    [Permission("product.view")]
    [ProducesResponseType(typeof(ApiResponse<ProductCategoryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await service.GetCategoryAsync(id, cancellationToken);
        return FromResult(result, "Product category retrieved successfully.");
    }

    [HttpPost]
    [Permission("product.create")]
    [ProducesResponseType(typeof(ApiResponse<ProductCategoryResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreateProductCategoryRequest request,
        CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not Guid userId)
        {
            return Unauthorized();
        }

        var result = await service.CreateCategoryAsync(request, userId, cancellationToken);
        return result.IsSuccess
            ? Created(result.Value, nameof(GetById), new { id = result.Value.Id }, "Product category created successfully.")
            : Error(result.Error);
    }

    [HttpPut("{id:guid}")]
    [Permission("product.update")]
    [ProducesResponseType(typeof(ApiResponse<ProductCategoryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateProductCategoryRequest request,
        CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not Guid userId)
        {
            return Unauthorized();
        }

        var result = await service.UpdateCategoryAsync(id, request, userId, cancellationToken);
        return FromResult(result, "Product category updated successfully.");
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

        var result = await service.DeactivateCategoryAsync(id, userId, cancellationToken);
        return FromResult(result, "Product category deactivated successfully.");
    }
}
