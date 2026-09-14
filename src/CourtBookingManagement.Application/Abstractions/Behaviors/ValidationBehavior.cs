using FluentValidation;
using CourtBookingManagement.Domain.Abstractions;
using MediatR;

namespace CourtBookingManagement.Application.Abstractions.Behaviors;

public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!validators.Any())
        {
            return await next();
        }

        var context = new ValidationContext<TRequest>(request);
        var results = await Task.WhenAll(validators.Select(validator => validator.ValidateAsync(context, cancellationToken)));
        var failures = results.SelectMany(result => result.Errors).Where(error => error is not null).ToList();

        if (failures.Count != 0)
        {
            return CreateValidationResult(failures);
        }

        return await next();
    }

    private static TResponse CreateValidationResult(
        IReadOnlyCollection<FluentValidation.Results.ValidationFailure> failures)
    {
        var error = new Error(
            "Error.Validation",
            string.Join("; ", failures.Select(failure =>
                $"{failure.PropertyName}: {failure.ErrorMessage}")));

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
            $"Cannot create validation result for type {typeof(TResponse).Name}");
    }
}