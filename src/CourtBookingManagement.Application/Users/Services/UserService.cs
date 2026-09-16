using CourtBookingManagement.Application.Abstractions.Clock;
using CourtBookingManagement.Application.Users.DTOs;
using CourtBookingManagement.Domain.Abstractions;
using CourtBookingManagement.Domain.Users;
using FluentValidation;

namespace CourtBookingManagement.Application.Users.Services;

public sealed class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IValidator<CreateUserRequestDto> _validator;

    public UserService(
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider,
        IValidator<CreateUserRequestDto> validator)
    {
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
        _validator = validator;
    }

    public async Task<Result<UserResponseDto>> CreateUserAsync(
        CreateUserRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result.Failure<UserResponseDto>(CreateValidationError(validationResult));
        }

        if (await _userRepository.ExistsByEmailAsync(request.Email, cancellationToken: cancellationToken))
        {
            return Result.Failure<UserResponseDto>(UserErrors.EmailAlreadyExists(request.Email));
        }

        var userResult = User.Create(
            request.Email,
            request.PasswordHash,
            _dateTimeProvider.UtcNow,
            request.PhoneNumber,
            request.CreatedBy);

        if (userResult.IsFailure)
        {
            return Result.Failure<UserResponseDto>(userResult.Error);
        }

        await _userRepository.AddAsync(userResult.Value, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToResponse(userResult.Value);
    }

    public async Task<Result<UserResponseDto>> UpdateUserAsync(
        Guid id,
        UpdateUserRequestDto request,
        CancellationToken cancellationToken = default)
    {
        //var validationResult = await validator.ValidateAsync(request, cancellationToken);
        //if (!validationResult.IsValid)
        //{
        //    return Result.Failure<UserResponseDto>(CreateValidationError(validationResult));
        //}

        var user = await _userRepository.GetByIdAsync(id, cancellationToken);
        if (user is null)
        {
            return Result.Failure<UserResponseDto>(UserErrors.NotFound(id));
        }

        if (await _userRepository.ExistsByEmailAsync(request.Email, id, cancellationToken))
        {
            return Result.Failure<UserResponseDto>(UserErrors.EmailAlreadyExists(request.Email));
        }

        var updateResult = user.UpdateContactInformation(
            request.Email,
            request.PhoneNumber,
            _dateTimeProvider.UtcNow,
            request.UpdatedBy);

        if (updateResult.IsFailure)
        {
            return Result.Failure<UserResponseDto>(updateResult.Error);
        }

        if (!string.IsNullOrWhiteSpace(request.PasswordHash))
        {
            var passwordResult = user.ChangePassword(
                request.PasswordHash,
                _dateTimeProvider.UtcNow,
                request.UpdatedBy);

            if (passwordResult.IsFailure)
            {
                return Result.Failure<UserResponseDto>(passwordResult.Error);
            }
        }

        await _userRepository.UpdateAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToResponse(user);
    }

    public async Task<Result> DeleteUserAsync(
        Guid id,
        Guid? deletedBy = null,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(id, cancellationToken);
        if (user is null)
        {
            return Result.Failure(UserErrors.NotFound(id));
        }

        user.Delete(_dateTimeProvider.UtcNow, deletedBy);
        await _userRepository.UpdateAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    public async Task<Result<UserResponseDto>> GetUserByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(id, cancellationToken);

        return user is null
            ? Result.Failure<UserResponseDto>(UserErrors.NotFound(id))
            : ToResponse(user);
    }

    public async Task<Result<IReadOnlyList<UserResponseDto>>> GetAllUsersAsync(
        CancellationToken cancellationToken = default)
    {
        var users = await _userRepository.GetAllAsync(cancellationToken);
        return users.Select(ToResponse).ToList();
    }

    private static UserResponseDto ToResponse(User user) => new()
    {
        Id = user.Id,
        Email = user.Email,
        PhoneNumber = user.PhoneNumber,
        IsActive = user.IsActive,
        IsEmailVerified = user.IsEmailVerified,
        LastLoginAt = user.LastLoginAt,
        CreatedAt = user.CreatedAt,
        CreatedBy = user.CreatedBy,
        UpdatedAt = user.UpdatedAt,
        UpdatedBy = user.UpdatedBy
    };

    private static Error CreateValidationError(FluentValidation.Results.ValidationResult result) => new(
        "Error.Validation",
        string.Join("; ", result.Errors.Select(error =>
            $"{error.PropertyName}: {error.ErrorMessage}")));
}