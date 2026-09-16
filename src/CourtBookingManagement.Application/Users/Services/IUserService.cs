using CourtBookingManagement.Application.Users.DTOs;
using CourtBookingManagement.Domain.Abstractions;

namespace CourtBookingManagement.Application.Users.Services;

public interface IUserService
{
    Task<Result<UserResponseDto>> CreateUserAsync(
        CreateUserRequestDto request,
        CancellationToken cancellationToken = default);

    Task<Result<UserResponseDto>> UpdateUserAsync(
        Guid id,
        UpdateUserRequestDto request,
        CancellationToken cancellationToken = default);

    Task<Result> DeleteUserAsync(
        Guid id,
        Guid? deletedBy = null,
        CancellationToken cancellationToken = default);

    Task<Result<UserResponseDto>> GetUserByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<UserResponseDto>>> GetAllUsersAsync(
        CancellationToken cancellationToken = default);
}