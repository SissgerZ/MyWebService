using ErrorOr;
using FluentValidation;
using MediatR;
using System.Diagnostics;

namespace MyWebService.Common.PipelineBehaviour;

[DebuggerStepThrough]
public sealed class RequestValidationPipelineBehavior<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators,
    ILogger<RequestValidationPipelineBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : IErrorOr
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (!validators.Any())
        {
            return await next(cancellationToken);
        }

        const string behavior = nameof(RequestValidationPipelineBehavior<TRequest, TResponse>);
        logger.LogDebug("[{Behavior}] Validating {RequestName}", behavior, typeof(TRequest).Name);

        var context = new ValidationContext<TRequest>(request);
        var validationResults = await Task.WhenAll(validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        var failures = validationResults
            .SelectMany(r => r.Errors)
            .Where(f => f != null)
            .ToList();

        if (failures.Count != 0)
        {
            logger.LogWarning("[{Behavior}] Validation failed for {RequestName}", behavior, typeof(TRequest).Name);

            // Map FluentValidation errors to ErrorOr validation errors
            var errors = failures.ConvertAll(failure => Error.Validation(
                code: failure.PropertyName,
                description: failure.ErrorMessage));

            // Return the errors directly. The 'dynamic' cast triggers ErrorOr's implicit conversion.
            return (dynamic)errors;
        }

        return await next(cancellationToken);
    }
}