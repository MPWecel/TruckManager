namespace TruckManager.Common.Results;

public enum EErrorType
{
    Validation=0,
    NotFound=1,
    Conflict=2,
    Concurrency=3,
    Unauthorized=4,
    Forbidden=5,
    Unexpected=6
}