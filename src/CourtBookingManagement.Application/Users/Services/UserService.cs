using CourtBookingManagement.Application.Abstractions.Clock;
using CourtBookingManagement.Application.Abstractions.Data;
using CourtBookingManagement.Application.Auth.Exceptions;
using CourtBookingManagement.Application.Customers.Models.Requests;
using CourtBookingManagement.Application.Customers.Repositories;
using CourtBookingManagement.Application.UserRoles.Repositories;
using CourtBookingManagement.Application.Matching.Models;
using CourtBookingManagement.Application.Users.DTOs;
using CourtBookingManagement.Domain.Abstractions;
using CourtBookingManagement.Domain.Users;
using FluentValidation;
using System.Data.Common;
using System.Text;

namespace CourtBookingManagement.Application.Users.Services;

public sealed class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IValidator<CreateUserRequestDto> _validator;
    private readonly ISqlConnectionFactory _connectionFactory;
    private readonly IUserRoleRepository _roleRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IMembershipLevelRepository _membershipLevels;

    public UserService(
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider,
        IValidator<CreateUserRequestDto> validator,
        ISqlConnectionFactory connectionFactory,
        IUserRoleRepository roleRepository,
        ICustomerRepository customerRepository,
        IMembershipLevelRepository membershipLevels)
    {
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
        _validator = validator;
        _connectionFactory = connectionFactory;
        _roleRepository = roleRepository;
        _customerRepository = customerRepository;
        _membershipLevels = membershipLevels;
    }

    public async Task<Result<UserResponseDto>> ProvisionUserAsync(
        ProvisionUserRequest request,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        if (actorId == Guid.Empty || string.IsNullOrWhiteSpace(request.FullName)
            || request.RoleIds.Contains(Guid.Empty) || Encoding.UTF8.GetByteCount(request.Password) > 72)
        {
            return Result.Failure<UserResponseDto>(new Error("Error.Validation", "Invalid account information."));
        }

        var email = request.Email.Trim().ToLowerInvariant();
        var fullName = request.FullName.Trim();
        var phone = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim();
        var roleIds = request.RoleIds.Distinct().ToArray();
        var userResult = User.Create(email, fullName, BCrypt.Net.BCrypt.HashPassword(request.Password),
            _dateTimeProvider.UtcNow, phone, actorId);
        if (userResult.IsFailure)
        {
            return Result.Failure<UserResponseDto>(userResult.Error);
        }

        var user = userResult.Value;
        if (!request.IsActive)
        {
            user.Deactivate(_dateTimeProvider.UtcNow, actorId);
        }

        await using var connection = (DbConnection)_connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var roles = await _roleRepository.GetActiveRolesAsync(roleIds, transaction, cancellationToken);
        if (roles.Count != roleIds.Length)
        {
            return Result.Failure<UserResponseDto>(new Error("Role.NotFound", "One or more selected roles are unavailable."));
        }

        var isCustomer = roles.Any(role => role.RoleCode == "CUSTOMER");
        if (isCustomer && phone is null)
        {
            return Result.Failure<UserResponseDto>(new Error("User.CustomerPhoneRequired", "A customer account needs a phone number."));
        }

        try
        {
            var userId = await _userRepository.CreateUserAsync(user, transaction, cancellationToken);
            if (roleIds.Length > 0)
            {
                await _roleRepository.AssignRolesAsync(userId, roleIds, actorId, transaction, cancellationToken);
            }

            if (isCustomer)
            {
                await _customerRepository.CreateCustomerAsync(new CreateCustomerInternalRequest
                {
                    UserId = userId,
                    MembershipLevelId = await _membershipLevels.GetDefaultMembershipLevelIdAsync(cancellationToken),
                    FullName = fullName,
                    Email = email,
                    PhoneNumber = phone!,
                    IsActive = request.IsActive,
                    CreatedBy = actorId
                }, transaction, cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return ToResponse(user);
        }
        catch (RegistrationConflictException exception)
        {
            var error = exception.ConstraintName == "ux_users_active_phone_number"
                ? new Error("AUTH_PHONE_ALREADY_EXISTS", "Phone number is already registered.")
                : new Error("AUTH_EMAIL_ALREADY_EXISTS", "Email address is already registered.");
            return Result.Failure<UserResponseDto>(error);
        }
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
            request.FullName,
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

    public async Task<Result<PagedResult<UserListItemResponseDto>>> GetListAsync(
        UserListRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Keyword = string.IsNullOrWhiteSpace(request.Keyword)
            ? null
            : request.Keyword.Trim();

        var users = await _userRepository.GetListWithRolesAsync(
            request.Keyword,
            request.PageNumber,
            request.PageSize,
            cancellationToken);

        var items = users.Items.Select(user => new UserListItemResponseDto
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            IsActive = user.IsActive,
            Roles = user.Roles
                .Select(role => new UserListRoleResponseDto
                {
                    Id = role.Id,
                    Code = role.Code,
                    Name = role.Name
                })
                .ToArray()
        }).ToArray();

        return PagedResult<UserListItemResponseDto>.Create(
            items,
            request.PageNumber,
            request.PageSize,
            users.TotalCount);
    }

    private static UserResponseDto ToResponse(User user) => new()
    {
        Id = user.Id,
        FullName = user.FullName,
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
