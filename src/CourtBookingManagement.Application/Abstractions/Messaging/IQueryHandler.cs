using CourtBookingManagement.Domain.Abstractions;
using MediatR;

namespace CourtBookingManagement.Application.Abstractions.Messaging;

public interface IQueryHandler<TQuery, TResponse> : IRequestHandler<TQuery, Result<TResponse>>
    where TQuery : IQuery<TResponse>
{
}