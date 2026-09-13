using CourtBookingManagement.Domain.Abstractions;
using MediatR;

namespace CourtBookingManagement.Application.Abstractions.Messaging;

public interface ICommand : IRequest<Result>
{
}

public interface ICommand<TResponse> : IRequest<Result<TResponse>>
{
}