using System.Reflection;

using FluentValidation;
using FluentValidation.Results;
using TruckManager.Application.Abstractions.Cqrs;
using TruckManager.Common.Results;

namespace TruckManager.Application.Behaviors;

// [ADR-0038 / ADR-0011]   Runs every registered IValidator<TRequest> before the handler.
// Registered on both command and query pipelines. Short-circuits on any validation failure — next() is never called.
//
// Returns a TResult constructed via reflection over TResult's static Failure factory so callers always receive a Result / Result<T> - never an exception on validation failure.
// s_failureFactory is resolved once per closed generic instantiation (static field in a generic class), so the reflection cost is bounded to startup / first-use.
//
// [Phase 7: LoggingBehavior slots before this in the pipeline.]
public sealed class ValidationBehavior<TRequest, TResult> : IPipelineBehavior<TRequest, TResult>
{
    private const BindingFlags _bindingFlagAggregate = BindingFlags.Public | BindingFlags.Static;
    private static readonly MethodInfo s_failureFactory = typeof(TResult).GetMethod(
                                                                                       nameof(Result.Failure), 
                                                                                       _bindingFlagAggregate, 
                                                                                       [typeof(IEnumerable<Error>)]
                                                                                   ) ?? throw new InvalidOperationException($"{typeof(TResult).Name} has no static Failure(IEnumerable<Error>) factory. TResult must be Result or Result<T>.");

    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResult> HandleAsync(
                                              TRequest request,
                                              Func<Task<TResult>> next,
                                              CancellationToken cancellationToken
                                          )
    {
        bool hasNoValidators = !_validators.Any();
        if (hasNoValidators)
            return await next();

        ValidationResult[] results = await Task.WhenAll(
                                                           _validators.Select(v => v.ValidateAsync(request, cancellationToken))
                                                       );

        List<Error> errors = results.Where(r => !r.IsValid)
                                    .SelectMany(r => r.Errors)
                                    .Select(
                                               f => new Error(
                                                                 f.PropertyName.Length > 0 ? $"Validation.{f.PropertyName}" : "Validation",
                                                                 f.ErrorMessage,
                                                                 EErrorType.Validation
                                                             )
                                           ).ToList();

        bool hasErrors = errors.Count > 0;
        if (hasErrors)
            return (TResult)(s_failureFactory.Invoke(null, [errors])!);

        return await next();
    }
}
