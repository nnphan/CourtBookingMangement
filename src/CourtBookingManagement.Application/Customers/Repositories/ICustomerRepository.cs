using CourtBookingManagement.Application.Customers.Models.Requests;
using CourtBookingManagement.Application.Customers.Models.Responses;
using CourtBookingManagement.Application.Matching.Models;

namespace CourtBookingManagement.Application.Customers.Repositories;

public interface ICustomerRepository
{
    Task<PagedResult<CustomerListItemResponse>> SearchAsync(
        CustomerSearchRequest request,
        CancellationToken cancellationToken);

    Task<CustomerDetailResponse?> GetByIdAsync(
        Guid customerId,
        CancellationToken cancellationToken);
}
