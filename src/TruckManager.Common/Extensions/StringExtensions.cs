namespace TruckManager.Common.Extensions;

public static class StringExtensions
{
    // Normalisation method for usage when assigning, comparing and the like of TruckCodes. Removes leading/trailing spaces and UPPERCASES the whole string. Culture INVARIANT. Potentially should be limited to ASCII charset on validation - to be considered
    // Potential to extend if rules for TruckCode format are estabilished
    public static string NormalizeCode(this string value)
    {
        ArgumentNullException.ThrowIfNullOrEmpty(value);    //Cool .NET9 feature for guarding. Downside - blocks using function body. Alternative is buttfugly, this is lesser of two evulz.
        return value.Trim().ToUpperInvariant();
    }
}
