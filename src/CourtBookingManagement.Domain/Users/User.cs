using CourtBookingManagement.Domain.Abstractions;
using CourtBookingManagement.Domain.Users.Events;

namespace CourtBookingManagement.Domain.Users;

public sealed class User : Entity
{
    private User(
        Guid id,
        string email,
        string fullName,
        string? phoneNumber,
        string passwordHash,
        bool isActive,
        bool isEmailVerified,
        DateTime? lastLoginAt,
        DateTime createdAt,
        Guid? createdBy,
        DateTime updatedAt,
        Guid? updatedBy,
        DateTime? deletedAt,
        Guid? deletedBy)
        : base(id)
    {
        Email = email;
        FullName = fullName;
        PhoneNumber = phoneNumber;
        PasswordHash = passwordHash;
        IsActive = isActive;
        IsEmailVerified = isEmailVerified;
        LastLoginAt = lastLoginAt;
        CreatedAt = createdAt;
        CreatedBy = createdBy;
        UpdatedAt = updatedAt;
        UpdatedBy = updatedBy;
        DeletedAt = deletedAt;
        DeletedBy = deletedBy;
    }

    public string Email { get; private set; } = null!;

    public string FullName { get; private set; } = null!;

    public string? PhoneNumber { get; private set; }

    public string PasswordHash { get; private set; } = null!;

    public bool IsActive { get; private set; }

    public bool IsEmailVerified { get; private set; }

    public DateTime? LastLoginAt { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public Guid? CreatedBy { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    public Guid? UpdatedBy { get; private set; }

    public DateTime? DeletedAt { get; private set; }

    public Guid? DeletedBy { get; private set; }

    public static Result<User> Create(
        string email,
        string? fullName,
        string passwordHash,
        DateTime createdAt,
        string? phoneNumber = null,
        Guid? createdBy = null)
    {
        var validationError = Validate(email, phoneNumber, passwordHash);
        if (validationError != Error.None)
        {
            return Result.Failure<User>(validationError);
        }

        var user = new User(
            Guid.NewGuid(),
            email.Trim(),
            fullName,
            phoneNumber,
            passwordHash,
            true,
            false,
            null,
            createdAt,
            createdBy,
            createdAt,
            createdBy,
            null,
            null); user.RaiseDomainEvent(new UserCreatedDomainEvent(user.Id));

        return user;
    }

    public static Result<User> Rehydrate(
        Guid id,
        string email,
        string fullName,
        string? phoneNumber,
        string passwordHash,
        bool isActive,
        bool isEmailVerified,
        DateTime? lastLoginAt,
        DateTime createdAt,
        Guid? createdBy,
        DateTime updatedAt,
        Guid? updatedBy,
        DateTime? deletedAt,
        Guid? deletedBy)
    {
        var validationError = Validate(email, phoneNumber, passwordHash);
        if (validationError != Error.None)
        {
            return Result.Failure<User>(validationError);
        }

        return new User(
            id,
            email,
            fullName,
            phoneNumber,
            passwordHash,
            isActive,
            isEmailVerified,
            lastLoginAt,
            createdAt,
            createdBy,
            updatedAt,
            updatedBy,
            deletedAt,
            deletedBy);
    }

    public Result UpdateContactInformation(
        string email,
        string? phoneNumber,
        DateTime updatedAt,
        Guid? updatedBy = null)
    {
        var validationError = Validate(email, phoneNumber, PasswordHash);
        if (validationError != Error.None)
        {
            return Result.Failure(validationError);
        }

        Email = email.Trim();
        PhoneNumber = phoneNumber;
        UpdatedAt = updatedAt;
        UpdatedBy = updatedBy;

        return Result.Success();
    }

    public Result ChangePassword(string passwordHash, DateTime updatedAt, Guid? updatedBy = null)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            return Result.Failure(UserErrors.PasswordHashIsRequired);
        }

        PasswordHash = passwordHash;
        UpdatedAt = updatedAt;
        UpdatedBy = updatedBy;

        return Result.Success();
    }

    public void VerifyEmail(DateTime updatedAt, Guid? updatedBy = null)
    {
        IsEmailVerified = true;
        UpdatedAt = updatedAt;
        UpdatedBy = updatedBy;
    }

    public void RecordLogin(DateTime loggedInAt)
    {
        LastLoginAt = loggedInAt;
    }

    public void Deactivate(DateTime updatedAt, Guid? updatedBy = null)
    {
        IsActive = false;
        UpdatedAt = updatedAt;
        UpdatedBy = updatedBy;
    }

    public void Activate(DateTime updatedAt, Guid? updatedBy = null)
    {
        IsActive = true;
        DeletedAt = null;
        DeletedBy = null;
        UpdatedAt = updatedAt;
        UpdatedBy = updatedBy;
    }

    public void Delete(DateTime deletedAt, Guid? deletedBy = null)
    {
        IsActive = false;
        DeletedAt = deletedAt;
        DeletedBy = deletedBy;
        UpdatedAt = deletedAt;
        UpdatedBy = deletedBy;
    }

    private static Error Validate(string email, string? phoneNumber, string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return UserErrors.EmailIsRequired;
        }

        if (email.Length > 100)
        {
            return UserErrors.EmailIsTooLong;
        }

        if (phoneNumber?.Length > 20)
        {
            return UserErrors.PhoneNumberIsTooLong;
        }

        return string.IsNullOrWhiteSpace(passwordHash)
            ? UserErrors.PasswordHashIsRequired
            : Error.None;
    }
}