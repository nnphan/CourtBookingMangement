using CourtBookingManagement.Application.Customers.Models.Requests;
using CourtBookingManagement.Application.Customers.Models.Responses;
using CourtBookingManagement.Application.Matching.Models;
using CourtBookingManagement.Domain.Abstractions;

namespace CourtBookingManagement.Application.Customers.Services;

public interface ICustomerService
{
    Task<Result<PagedResult<CustomerListItemResponse>>> SearchAsync(
        CustomerSearchRequest request,
        CancellationToken cancellationToken);

    Task<Result<CustomerDetailResponse>> GetByIdAsync(
        Guid customerId,
        CancellationToken cancellationToken);
}
