using CourtBookingManagement.Application.StaffManagement.DTOs;
using CourtBookingManagement.Domain.Abstractions;

namespace CourtBookingManagement.Application.StaffManagement.Interfaces;

public interface IStaffCreationService
{
    Task<Result<CreateStaffResponse>> CreateAsync(Guid actorId, CreateStaffRequest request, CancellationToken cancellationToken);

    Task<Result<CreateStaffResponse>> ResendAsync(Guid actorId, Guid userId, CancellationToken cancellationToken);

    Task<Result> ActivateAsync(ActivateStaffRequest request, CancellationToken cancellationToken);
}
