using CourtBookingManagement.Domain.Abstractions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CourtBookingManagement.Application.Abstractions.Behaviors;

public sealed class ExceptionHandlingBehavior<TRequest, TResponse>(
    ILogger<ExceptionHandlingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : Result
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        try
        {
            return await next();
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Unhandled exception for request {RequestName}",
                typeof(TRequest).Name);

            return CreateExceptionResult(exception);
        }
    }

    private static TResponse CreateExceptionResult(Exception exception)
    {
        var error = Error.FromException(exception);

        if (typeof(TResponse) == typeof(Result))
        {
            return (TResponse)(object)Result.Failure(error);
        }

        var resultType = typeof(TResponse);
        if (resultType.IsGenericType && resultType.GetGenericTypeDefinition() == typeof(Result<>))
        {
            var valueType = resultType.GetGenericArguments()[0];
            var failureMethod = typeof(Result)
                .GetMethod(nameof(Result.Failure), [typeof(Error)])!
                .MakeGenericMethod(valueType);

            return (TResponse)failureMethod.Invoke(null, [error])!;
        }

        throw new InvalidOperationException(
            $"Cannot create exception result for type {typeof(TResponse).Name}");
    }
}