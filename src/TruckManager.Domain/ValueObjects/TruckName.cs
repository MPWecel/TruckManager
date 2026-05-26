using TruckManager.Common.Results;

namespace TruckManager.Domain.ValueObjects;

// Truck display name. Trimmed; non-empty after trim; length 1–128; no control characters.
// The validator pipeline below makes future rules (e.g. zero-width-char rejection) cheap
// to add — one new element in the Rules array.
public sealed record TruckName
{
    public const int MaxLength = 128;

    public string Value { get; }

    private TruckName(string value) => Value = value;

    // [Phase 4 — Persistence]   Fast-path for DB-sourced strings that were validated by
    // Create(...) on insert. Used by the EF Core value converter on load. Do NOT use for
    // any input originating outside the persistence boundary — call Create(...) instead.
    internal static TruckName FromTrusted(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new TruckName(value);
    }

    public static Result<TruckName> Create(string raw)
    {
        ArgumentNullException.ThrowIfNull(raw);

        string trimmed = raw.Trim();
        Error? failure = Rules.Select(rule => rule(trimmed))
                              .FirstOrDefault(e => e is not null);

        bool isSuccess = failure is null;
        Result<TruckName> result = isSuccess ? Result<TruckName>.Success(new TruckName(trimmed)) : Result<TruckName>.Failure(failure!);
        return result;
    }

    private static readonly Func<string, Error?>[] Rules =
    [
        v => String.IsNullOrEmpty(v) ? new Error("truck.name.empty", "Truck name cannot be empty or whitespace.", EErrorType.Validation) : null,
        v => IsStringLengthOverLimit(v) ? new Error("truck.name.too_long", $"Truck name cannot exceed {MaxLength} characters.", EErrorType.Validation) : null,
        v => HasControlChars(v) ? new Error("truck.name.control_chars", "Truck name cannot contain control characters.", EErrorType.Validation) : null,
    ];

    private static bool IsStringLengthOverLimit(string value) => value.Length > MaxLength;
    private static bool HasControlChars(string value) => value.Any(Char.IsControl);
}
