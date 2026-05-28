namespace TruckManager.Common.Extensions;

public static class StringExtensions
{
    // Normalisation method for usage when assigning, comparing and the like of TruckCodes. Removes leading/trailing spaces and UPPERCASES the whole string. Culture INVARIANT.
    // Potentially should be limited to ASCII charset on validation - to be considered
    // Potential to extend if rules for TruckCode format are estabilished
    public static string NormalizeCode(this string value)
    {
        // Null is an invariant violation (caller bug).
        // Empty / whitespace are valid inputs for the utility — they normalize to "" and the *caller's* domain rules decide whether emptiness is acceptable
        // (e.g. TruckCode.Create flags it as Validation failure via its Rules pipeline). See [ADR-0028].
        ArgumentNullException.ThrowIfNull(value);
        string result = value.Trim().ToUpperInvariant();
        return result;
    }
}
