using TruckManager.Common.Results;

namespace TruckManager.Domain.ValueObjects;

// Truck description — optional, but always present as a VO (never null on Truck). Empty
// value (TruckDescription.Empty) represents "no description". Phase 4's EF Core mapping
// will translate empty ↔ NULL in the nullable `Description` column.
//
// Length cap: 512 characters. Control characters rejected; relax via the Rules array if
// multi-line descriptions are needed (newline / carriage-return / tab are currently
// blocked — easy to whitelist when the requirement arises).
public sealed record TruckDescription
{
    public const int MaxLength = 512;

    public static readonly TruckDescription Empty = new(String.Empty);

    public string Value { get; }

    public bool IsEmpty => Value.Length == 0;

    private TruckDescription(string value) => Value = value;

    public static Result<TruckDescription> Create(string? raw)
    {   
        //TODO I don't like early returns. To consider when refactoring: Single exit point with default value when condition not met. Might produce ugly code, tho. Food for thought...
        if (raw is null)
            return Empty;
        
        string trimmed = raw.Trim();

        if (trimmed.Length == 0)
            return Empty;
        
        Error? failure = Rules.Select(rule => rule(trimmed))
                              .FirstOrDefault(e => e is not null);

        bool isSuccess = failure is null;
        Result<TruckDescription> result = isSuccess ? Result<TruckDescription>.Success(new TruckDescription(trimmed)) : Result<TruckDescription>.Failure(failure!);
        return result;

        //return failure is null ? new TruckDescription(trimmed) : failure; //TODO I don't like it.
    }

    private static readonly Func<string, Error?>[] Rules =
    [
        v => IsStringLengthOverLimit(v) ? new Error("truck.description.too_long", $"Truck description cannot exceed {MaxLength} characters.", EErrorType.Validation) : null,
        v => HasControlChars(v) ? new Error("truck.description.control_chars", "Truck description cannot contain control characters.", EErrorType.Validation) : null,
    ];

    private static bool IsStringLengthOverLimit(string value) => value.Length > MaxLength;
    private static bool HasControlChars(string value) => value.Any(char.IsControl);
}
