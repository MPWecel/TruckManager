namespace TruckManager.Common.Results;

public sealed record Error(string Code, string Message, EErrorType Type);
