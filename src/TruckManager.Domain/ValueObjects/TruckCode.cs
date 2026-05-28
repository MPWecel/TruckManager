using TruckManager.Common.Extensions;
using TruckManager.Common.Results;

namespace TruckManager.Domain.ValueObjects;

// Truck business code. Normalized via NormalizeCode() (Trim + ToUpperInvariant) then validated as strict alphanumeric ASCII, length 1–32 (design doc §3.3, ADR for Phase 3).
// Immutable post-creation in V1 — display formatting (hyphens, dots, etc.) is a frontend concern.
// Add new rules to the Rules array as one-liners.
public sealed record TruckCode
{
    public const int MaxLength = 32;

    public string Value { get; }

    private TruckCode(string value) => Value = value;

    // [Phase 4 — Persistence]   Fast-path for DB-sourced strings that were validated by Create(...) on insert.
    // Used by the EF Core value converter on load. Do NOT use for any input originating outside the persistence boundary — call Create(...) instead.
    internal static TruckCode FromTrusted(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new TruckCode(value);
    }

    public static Result<TruckCode> Create(string raw)
    {
        // null is an invariant violation per [ADR-0028] — well-behaved callers normalize at the boundary (FluentValidation in Phase 5) before reaching the VO factory.
        ArgumentNullException.ThrowIfNull(raw);

        string normalized = raw.NormalizeCode();
        Error? failure = Rules.Select(rule => rule(normalized))
                              .FirstOrDefault(e => e is not null);
        
        bool isSuccess = failure is null;
        Result<TruckCode> result = isSuccess ? Result<TruckCode>.Success(new TruckCode(normalized)) : Result<TruckCode>.Failure(failure!);
        return result;
    }

    private static readonly Func<string, Error?>[] Rules =
    [
        v => String.IsNullOrEmpty(v) ? new Error("truck.code.empty", "Truck code cannot be empty or whitespace.", EErrorType.Validation) : null,
        v => IsStringLengthOverLimit(v) ? new Error("truck.code.too_long", $"Truck code cannot exceed {MaxLength} characters.", EErrorType.Validation) : null,
        v => !IsValidCharset(v) ? new Error("truck.code.invalid_charset", "Truck code must contain only uppercase ASCII letters and digits.", EErrorType.Validation) : null,
    ];

    private static bool IsStringLengthOverLimit(string value) => value.Length > MaxLength;
    private static bool IsValidCharset(string value) => value.All(IsCharacterUpperAlphaNumeric);
    private static bool IsCharacterUpperAlphaNumeric(char character) => Char.IsAsciiDigit(character) || Char.IsAsciiLetterUpper(character);

}
