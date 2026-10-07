using CourtBookingManagement.Api.Common.Responses;
using CourtBookingManagement.Application.Customers.Models.Requests;
using CourtBookingManagement.Application.Customers.Models.Responses;
using CourtBookingManagement.Application.Customers.Services;
using CourtBookingManagement.Infrastructure.Auth;
using Microsoft.AspNetCore.Mvc;

namespace CourtBookingManagement.Api.Controllers;

[ApiController]
[Route("api/customers")]
public sealed class CustomersController(ICustomerService service) : ApiControllerBase
{
    [HttpGet]
    [Permission("customer.view")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<CustomerListItemResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetCustomers(
        [FromQuery] CustomerSearchRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.SearchAsync(request, cancellationToken);
        return result.IsSuccess
            ? Success(result.Value.Items, "Customers retrieved successfully.", result.Value.Metadata)
            : Error(result.Error);
    }

    [HttpGet("{id:guid}")]
    [Permission("customer.view")]
    [ProducesResponseType(typeof(ApiResponse<CustomerDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await service.GetByIdAsync(id, cancellationToken);
        return FromResult(result, "Customer retrieved successfully.");
    }
}
