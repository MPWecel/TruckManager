namespace TruckManager.Common.Results;

public static class ResultExtensions
{
    public static Result<TOut> Map<TIn, TOut>(
                                                 this Result<TIn> result,
                                                 Func<TIn, TOut> mapper
                                             ) 
        => result.IsFailure ? Result<TOut>.Failure(result.Errors) : mapper(result.Value!);

    public static Result<TOut> Bind<TIn, TOut>(
                                                  this Result<TIn> result,
                                                  Func<TIn, Result<TOut>> binder
                                              ) 
        => result.IsFailure ? Result<TOut>.Failure(result.Errors) : binder(result.Value!);

    public static Result<T> Tap<T>(
                                      this Result<T> result, 
                                      Action<T> action
                                  )
    {
        if (result.IsSuccess)
            action(result.Value!);
        
        return result;
    }

    public static TOut Match<TIn, TOut>(
                                           this Result<TIn> result,
                                           Func<TIn, TOut> onSuccess,
                                           Func<IReadOnlyList<Error>, TOut> onFailure
                                       ) 
        => result.IsSuccess ? onSuccess(result.Value!) : onFailure(result.Errors);

    public static Result Combine(params Result[] results)
    {
        List<Error> errors = results.Where(r => r.IsFailure)
                                    .SelectMany(r=>r.Errors)
                                    .ToList();

        bool isErrorsEmpty = errors.Count == 0;
        Result finalResult = isErrorsEmpty ? Result.Success() : Result.Failure(errors);

        return finalResult;
    }
}
