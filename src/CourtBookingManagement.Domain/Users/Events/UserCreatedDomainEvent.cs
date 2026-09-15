using CourtBookingManagement.Domain.Abstractions;

namespace CourtBookingManagement.Domain.Users.Events;

public sealed record UserCreatedDomainEvent(Guid UserId) : IDomainEvent;