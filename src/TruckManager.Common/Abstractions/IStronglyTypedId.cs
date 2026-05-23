namespace TruckManager.Common.Abstractions;

// Marker interface for ValueObject Ids. The struct constraint limits the backing primitive (only Guid, int, short long and the like shall pass)
//TODO: [Phase 4] registration of generic ValueConverters fir EF Core
public interface IStronglyTypedId<TValue> where TValue : struct
{
    TValue Value { get; }
}
