using System.Linq.Expressions;
using System.Reflection;

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using TruckManager.Common.Abstractions;

namespace TruckManager.Infrastructure.Persistence.Conversions;

// [ADR-0029]   Generic value converter for any strongly-typed ID marked with
// IStronglyTypedId<TValue>. One implementation covers every aggregate ID (TruckId,
// TenantId, future JobId/ShipmentId/etc.) — no per-ID boilerplate.
//
// To-provider: id.Value (cheap property access).
// From-provider: invokes the strongly-typed ID's (TValue) constructor via a compiled
//                expression tree — one-time reflection at startup, zero per-row cost.
//
// Registration is performed in ApplicationDbContext.ConfigureConventions, which scans the
// Domain assembly for closed types implementing the marker and registers one converter per
// type via ModelConfigurationBuilder.Properties(idType).HaveConversion(converterType).
public sealed class StronglyTypedIdValueConverter<TId, TValue> : ValueConverter<TId, TValue>
    where TId : class, IStronglyTypedId<TValue>
    where TValue : struct
{
    public StronglyTypedIdValueConverter()
        : base(
                  id => id.Value,
                  BuildFromProviderExpression()
              )
    { }

    private static Expression<Func<TValue, TId>> BuildFromProviderExpression()
    {
        ConstructorInfo? ctor = typeof(TId).GetConstructor(
                                                              bindingAttr: BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                                                              binder: null,
                                                              types: [typeof(TValue)],
                                                              modifiers: null
                                                          );

        if (ctor is null)
            throw new InvalidOperationException(
                $"Strongly-typed ID type '{typeof(TId).FullName}' must declare a single-parameter constructor taking {typeof(TValue).Name} so the EF Core value converter can rehydrate it from the database."
            );

        ParameterExpression param = Expression.Parameter(typeof(TValue), "value");
        NewExpression newExpr = Expression.New(ctor, param);
        return Expression.Lambda<Func<TValue, TId>>(newExpr, param);
    }
}
