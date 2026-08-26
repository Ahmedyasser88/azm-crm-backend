using AzmCrm.Domain.Common;
using FluentValidation;
using MediatR;

namespace AzmCrm.Application.Shared.Behaviors;

internal sealed class ValidationBehavior<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        if (!validators.Any())
            return await next();

        var context = new ValidationContext<TRequest>(request);

        var validationResults = await Task.WhenAll(
            validators.Select(v => v.ValidateAsync(context, ct)));

        var failures = validationResults
            .SelectMany(r => r.Errors)
            .Where(f => f != null)
            .Select(f => f.ErrorMessage)
            .ToList();

        if (failures.Count != 0)
        {
            return CreateValidationResult(failures);
        }

        return await next();
    }

    private static TResponse CreateValidationResult(List<string> errors)
    {
        var resultType = typeof(TResponse);

        if (resultType.IsGenericType && resultType.GetGenericTypeDefinition() == typeof(Result<>))
        {
            var valueType = resultType.GetGenericArguments()[0];
            var failureMethod = typeof(Result<>)
                .MakeGenericType(valueType)
                .GetMethod(nameof(Result<object>.Failure), [typeof(IReadOnlyList<string>)]);

            return (TResponse)failureMethod!.Invoke(null, [errors])!;
        }

        if (resultType == typeof(Result))
        {
            return (TResponse)(object)Result.Failure(errors);
        }

        throw new ValidationException(errors.Select(e =>
            new FluentValidation.Results.ValidationFailure(string.Empty, e)).ToList());
    }
}
