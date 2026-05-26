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

    // [ADR-0032 follow-up / Phase 4 decision #2]   Fast-path for DB-sourced strings that
    // have already been validated by Create(...) on insert. Used by the EF Core value
    // converter on load to skip the validator pipeline. Do NOT use for any input
    // originating outside the persistence boundary — call Create(...) instead.
    internal static TruckDescription FromTrusted(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.Length == 0 ? Empty : new TruckDescription(value);
    }

    public static Result<TruckDescription> Create(string? raw)
    {   
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
    }

    private static readonly Func<string, Error?>[] Rules =
    [
        v => IsStringLengthOverLimit(v) ? new Error("truck.description.too_long", $"Truck description cannot exceed {MaxLength} characters.", EErrorType.Validation) : null,
        v => HasControlChars(v) ? new Error("truck.description.control_chars", "Truck description cannot contain control characters.", EErrorType.Validation) : null,
    ];

    private static bool IsStringLengthOverLimit(string value) => value.Length > MaxLength;
    private static bool HasControlChars(string value) => value.Any(Char.IsControl);
}
