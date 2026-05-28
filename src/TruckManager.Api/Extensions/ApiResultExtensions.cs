using Microsoft.AspNetCore.Mvc;

using TruckManager.Common.Results;
using TruckManager.Domain.ValueObjects;

namespace TruckManager.Api.Extensions;

// Phase 6 / Section C    Maps Result / Result<T> -> IActionResult.
// Owns the EErrorType  ->  HTTP-status / type-URI mapping from architecture §14a. Single source of truth — controllers call these extensions instead of branching on the result themselves.
// Success paths:
//   >  ToNoContentResult   ->  204 NoContent  (Update / ChangeStatus / Delete — resultless commands)
//   >  ToCreatedResult     ->  201 Created + Location header pointing at the GET-by-id action (Create)
//   >  ToOkResult<T>       ->  200 OK + DTO body  (GetById / List)
//
// Failure paths converge on ToProblemResult. The FIRST error's Type drives the HTTP shape (per architecture §14a — see MapErrorType / TitleFor below for the full table).
// Validation is special-cased into ValidationProblemDetails with a per-property error dictionary; every other EErrorType maps to a plain ProblemDetails.
//
// Validation property keys: ValidationBehavior emits Error.Code = "Validation.<PropertyName>" (or bare "Validation" for rules with no PropertyName).
// ExtractPropertyName splits the prefix; the unscoped "" bucket holds errors that don't bind to a single field.
public static class ApiResultExtensions
{
    public static IActionResult ToNoContentResult(this Result result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.IsSuccess)
            return new NoContentResult();
        else
            return ToProblemResult(result.Errors);
    }

    public static IActionResult ToCreatedResult(this Result<TruckId> result, string actionName, object routeValues)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(actionName);
        ArgumentNullException.ThrowIfNull(routeValues);

        if (result.IsSuccess)
        {
            // controllerName: null -> "current controller". The framework resolves the action URL at execution time via IUrlHelper from HttpContext.
            // The response body wraps the new id in a minimal { id } object so JSON consumers never see a bare scalar.
            object responseBody = new { id = result.Value!.Value };

            CreatedAtActionResult successResult = new(
                                                         actionName: actionName,
                                                         controllerName: null,
                                                         routeValues: routeValues,
                                                         value: responseBody
                                                     );
            return successResult;
        }
        else
        {
            return ToProblemResult(result.Errors);
        }
    }

    public static IActionResult ToOkResult<T>(this Result<T> result)
    {
        ArgumentNullException.ThrowIfNull(result);

        IActionResult methodResult = result.IsSuccess ? 
                                        new OkObjectResult(result.Value) : 
                                        ToProblemResult(result.Errors);
        
        return methodResult;
    }

    // ---- failure mapping --------------------------------------------------------------
    // Result invariant (Common.Results.Result ctor): a failed Result has at least one error, so errors[0] is safe.
    private static IActionResult ToProblemResult(IReadOnlyList<Error> errors)
    {
        Error first = errors[0];

        if (first.Type == EErrorType.Validation)
            return BuildValidationProblem(errors);

        (int status, string typeUri) = MapErrorType(first.Type);

        ProblemDetails problemDetails = new()
        {
            Type    = typeUri,
            Title   = TitleFor(first.Type),
            Status  = status,
            Detail  = first.Message,
        };

        return new ObjectResult(problemDetails) { StatusCode = status };
    }

    private static IActionResult BuildValidationProblem(IReadOnlyList<Error> errors)
    {
        Dictionary<string, List<string>> grouped = new(StringComparer.Ordinal);
        foreach (Error err in errors)
        {
            string propertyName = ExtractPropertyName(err.Code);
            if (!grouped.TryGetValue(propertyName, out List<string>? messages))
            {
                messages = new List<string>();
                grouped[propertyName] = messages;
            }
            messages.Add(err.Message);
        }

        Dictionary<string, string[]> errorsByProperty = grouped.ToDictionary(
                                                                                kvp => kvp.Key,
                                                                                kvp => kvp.Value.ToArray(),
                                                                                StringComparer.Ordinal
                                                                            );

        const string validationErrorTitle = "One or more validation errors occurred.";
        ValidationProblemDetails validationProblem = 
            new(errorsByProperty)
            {
                Type = ProblemDetailsTypes.ValidationError,
                Title = validationErrorTitle,
                Status = StatusCodes.Status400BadRequest,
                Detail = errors[0].Message,
            };

        ObjectResult objectResult = new(validationProblem) { StatusCode = StatusCodes.Status400BadRequest };
        return objectResult;
    }

    // ValidationBehavior emits Code = "Validation.<PropertyName>"; bare "Validation" (no PropertyName) collapses to an empty string so it still appears in the ValidationProblemDetails.Errors dict.
    private static string ExtractPropertyName(string code)
    {
        const string prefix = "Validation.";
        string result = code.StartsWith(prefix, StringComparison.Ordinal) ? code[prefix.Length..] : String.Empty;
        return result;
    }

    // architecture §14a — kept in lock-step with EErrorType. Default branch maps both EErrorType.Unexpected and any future, not-yet-handled member to 500 — fail safe.
    private static (int Status, string TypeUri) MapErrorType(EErrorType type) 
        => type switch
        {
            EErrorType.NotFound => (StatusCodes.Status404NotFound, ProblemDetailsTypes.NotFound),
            EErrorType.Conflict => (StatusCodes.Status409Conflict, ProblemDetailsTypes.Conflict),
            EErrorType.Concurrency => (StatusCodes.Status409Conflict, ProblemDetailsTypes.ConcurrencyConflict),
            EErrorType.Unauthorized => (StatusCodes.Status401Unauthorized, ProblemDetailsTypes.Unauthorized),
            EErrorType.Forbidden => (StatusCodes.Status403Forbidden, ProblemDetailsTypes.Forbidden),
            _ => (StatusCodes.Status500InternalServerError, ProblemDetailsTypes.Unexpected),
        };

    private static string TitleFor(EErrorType type) 
        => type switch
        {
            EErrorType.NotFound => "Resource not found.",
            EErrorType.Conflict => "Conflict.",
            EErrorType.Concurrency => "Concurrency conflict.",
            EErrorType.Unauthorized => "Unauthorized.",
            EErrorType.Forbidden => "Forbidden.",
            _ => "An unexpected error occurred.",
        };
}
