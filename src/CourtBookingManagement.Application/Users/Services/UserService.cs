using CourtBookingManagement.Application.Abstractions.Clock;
using CourtBookingManagement.Application.Users.DTOs;
using CourtBookingManagement.Domain.Abstractions;
using CourtBookingManagement.Domain.Users;
using FluentValidation;

namespace CourtBookingManagement.Application.Users.Services;

public sealed class UserService(
    IUserRepository userRepository,
    IUnitOfWork unitOfWork,
    IDateTimeProvider dateTimeProvider,
    IValidator<CreateUserRequestDto> createValidator,
    IValidator<UpdateUserRequestDto> updateValidator) : IUserService
{
    public async Task<Result<UserResponseDto>> CreateUserAsync(
        CreateUserRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await createValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result.Failure<UserResponseDto>(CreateValidationError(validationResult));
        }

        if (await userRepository.ExistsByEmailAsync(request.Email, cancellationToken: cancellationToken))
        {
            return Result.Failure<UserResponseDto>(UserErrors.EmailAlreadyExists(request.Email));
        }

        var userResult = User.Create(
            request.Email,
            request.PasswordHash,
            dateTimeProvider.UtcNow,
            request.PhoneNumber,
            request.CreatedBy);

        if (userResult.IsFailure)
        {
            return Result.Failure<UserResponseDto>(userResult.Error);
        }

        await userRepository.AddAsync(userResult.Value, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return ToResponse(userResult.Value);
    }

    public async Task<Result<UserResponseDto>> UpdateUserAsync(
        Guid id,
        UpdateUserRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await updateValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result.Failure<UserResponseDto>(CreateValidationError(validationResult));
        }

        var user = await userRepository.GetByIdAsync(id, cancellationToken);
        if (user is null)
        {
            return Result.Failure<UserResponseDto>(UserErrors.NotFound(id));
        }

        if (await userRepository.ExistsByEmailAsync(request.Email, id, cancellationToken))
        {
            return Result.Failure<UserResponseDto>(UserErrors.EmailAlreadyExists(request.Email));
        }

        var updateResult = user.UpdateContactInformation(
            request.Email,
            request.PhoneNumber,
            dateTimeProvider.UtcNow,
            request.UpdatedBy);

        if (updateResult.IsFailure)
        {
            return Result.Failure<UserResponseDto>(updateResult.Error);
        }

        if (!string.IsNullOrWhiteSpace(request.PasswordHash))
        {
            var passwordResult = user.ChangePassword(
                request.PasswordHash,
                dateTimeProvider.UtcNow,
                request.UpdatedBy);

            if (passwordResult.IsFailure)
            {
                return Result.Failure<UserResponseDto>(passwordResult.Error);
            }
        }

        await userRepository.UpdateAsync(user, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return ToResponse(user);
    }

    public async Task<Result> DeleteUserAsync(
        Guid id,
        Guid? deletedBy = null,
        CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByIdAsync(id, cancellationToken);
        if (user is null)
        {
            return Result.Failure(UserErrors.NotFound(id));
        }

        user.Delete(dateTimeProvider.UtcNow, deletedBy);
        await userRepository.UpdateAsync(user, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    public async Task<Result<UserResponseDto>> GetUserByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByIdAsync(id, cancellationToken);

        return user is null
            ? Result.Failure<UserResponseDto>(UserErrors.NotFound(id))
            : ToResponse(user);
    }

    public async Task<Result<IReadOnlyList<UserResponseDto>>> GetAllUsersAsync(
        CancellationToken cancellationToken = default)
    {
        var users = await userRepository.GetAllAsync(cancellationToken);
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

internal sealed class CreateUserRequestValidator : AbstractValidator<CreateUserRequestDto>
{
    public CreateUserRequestValidator()
    {
        RuleFor(request => request.Email)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(request => request.PasswordHash)
            .NotEmpty();

        RuleFor(request => request.PhoneNumber)
            .MaximumLength(20)
            .When(request => request.PhoneNumber is not null);
    }
}

internal sealed class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequestDto>
{
    public UpdateUserRequestValidator()
    {
        RuleFor(request => request.Email)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(request => request.PasswordHash)
            .MaximumLength(500)
            .When(request => request.PasswordHash is not null);

        RuleFor(request => request.PhoneNumber)
            .MaximumLength(20)
            .When(request => request.PhoneNumber is not null);
    }
}