using CourtBookingManagement.Domain.Abstractions;
using MediatR;

namespace CourtBookingManagement.Application.Abstractions.Messaging;

public interface IQuery<TResponse> : IRequest<Result<TResponse>>
{
}