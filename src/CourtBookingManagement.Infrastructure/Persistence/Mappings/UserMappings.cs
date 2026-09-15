using DomainUser = CourtBookingManagement.Domain.Users.User;
using EfUser = CourtBookingManagement.Infrastructure.Persistence.Entities.User;

namespace CourtBookingManagement.Infrastructure.Persistence.Mappings;

public static class UserMappings
{
    public static DomainUser ToDomain(this EfUser entity)
    {
        var result = DomainUser.Rehydrate(
            entity.Id,
            entity.Email,
            entity.PhoneNumber,
            entity.PasswordHash,
            entity.IsActive,
            entity.IsEmailVerified,
            entity.LastLoginAt,
            entity.CreatedAt,
            entity.CreatedBy,
            entity.UpdatedAt,
            entity.UpdatedBy,
            entity.DeletedAt,
            entity.DeletedBy);

        return result.IsSuccess
            ? result.Value
            : throw new InvalidOperationException(
                $"Persisted user '{entity.Id}' is invalid: {result.Error.Code}");
    }

    public static EfUser ToPersistence(this DomainUser domain)
    {
        var entity = new EfUser();
        domain.ApplyTo(entity);
        return entity;
    }

    public static void ApplyTo(this DomainUser domain, EfUser entity)
    {
        entity.Id = domain.Id;
        entity.Email = domain.Email;
        entity.PhoneNumber = domain.PhoneNumber;
        entity.PasswordHash = domain.PasswordHash;
        entity.IsActive = domain.IsActive;
        entity.IsEmailVerified = domain.IsEmailVerified;
        entity.LastLoginAt = domain.LastLoginAt;
        entity.CreatedAt = domain.CreatedAt;
        entity.CreatedBy = domain.CreatedBy;
        entity.UpdatedAt = domain.UpdatedAt;
        entity.UpdatedBy = domain.UpdatedBy;
        entity.DeletedAt = domain.DeletedAt;
        entity.DeletedBy = domain.DeletedBy;
    }
}