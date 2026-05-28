using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Serilog.Core;
using Serilog.Events;

namespace TruckManager.Infrastructure.Logging;

// Phase 7 / Section D.   Serilog destructuring policy that masks sensitive property values.
//
// Applied at Serilog wiring time via cfg.Destructure.With<SensitivePropertyDestructuringPolicy>() in Api/Program.cs.
// When Serilog destructures any non-primitive object (the @ in a message template, or implicit destructuring of complex types),
// this policy intercepts and inspects the public instance properties:
//   >  If the property name matches one of the sensitive keywords (case-insensitive contains), the value is replaced with the scalar "***".
//   >  Otherwise, the value is delegated back to the framework's property-value factory, which may recursively apply this policy + any default destructurer.
//
// Performance / scope:
//   >  Returns false (no interception) when the object has zero sensitive-named properties.
//      Serilog then falls through to its built-in reflection-based destructurer — same behavior as before. Keeps the policy zero-cost for the 99 % case.
//   >  Scalar types (primitives, strings, Guid, DateTime, etc.) are also passed through.
//
// Match list — case-insensitive `String.Contains`:
//   password / pwd / secret / token / authorization / apikey / connectionstring / cookie.
// Intentionally loose: false positives (over-masking a non-secret) are safer than false negatives (leaked secret).
// When in doubt, add the keyword to the list.
public sealed class SensitivePropertyDestructuringPolicy : IDestructuringPolicy
{
    public const string MaskedValue = "***";

    private const BindingFlags PublicInstance = BindingFlags.Public | BindingFlags.Instance;

    private static readonly string[] s_sensitiveKeywords =
    [
        "password",
        "pwd",
        "secret",
        "token",
        "authorization",
        "apikey",
        "connectionstring",
        "cookie",
    ];

    public bool TryDestructure(
                                  object value,
                                  ILogEventPropertyValueFactory propertyValueFactory,
                                  [NotNullWhen(true)] out LogEventPropertyValue? result
                              )
    {
        result = null;

        if (value is null)
            return false;

        ArgumentNullException.ThrowIfNull(propertyValueFactory);

        Type type = value.GetType();
        if (IsScalar(type))
            return false;

        PropertyInfo[] properties = type.GetProperties(PublicInstance);

        // Fast path: nothing sensitive here — let Serilog use its default destructurer.
        bool hasSensitiveProperty = false;
        foreach (PropertyInfo prop in properties)
        {
            if (IsSensitive(prop.Name))
            {
                hasSensitiveProperty = true;
                break;
            }
        }
        if (!hasSensitiveProperty)
            return false;

        List<LogEventProperty> rendered = new(properties.Length);
        foreach (PropertyInfo prop in properties)
        {
            if (!prop.CanRead)
                continue;

            string propertyName = prop.Name;
            object? rawValue;
            try
            {
                rawValue = prop.GetValue(value);
            }
            catch
            {
                // Never let a misbehaving property getter take down logging.
                continue;
            }

            LogEventPropertyValue valueRepresentation = IsSensitive(propertyName) ? 
                                                            new ScalarValue(MaskedValue) : 
                                                            propertyValueFactory.CreatePropertyValue(rawValue, destructureObjects: true);

            rendered.Add(new LogEventProperty(propertyName, valueRepresentation));
        }

        result = new StructureValue(rendered, type.Name);
        return true;
    }

    private static bool IsSensitive(string propertyName)
    {
        bool result = false;
        foreach (string keyword in s_sensitiveKeywords)
        {
            if (propertyName.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                result = true;
                break;
            }
        }
        return result;
    }

    private static bool IsScalar(Type type) =>  type.IsPrimitive || 
                                                type == typeof(string) || 
                                                type == typeof(decimal) || 
                                                type == typeof(DateTime) || 
                                                type == typeof(DateTimeOffset) || 
                                                type == typeof(TimeSpan) || 
                                                type == typeof(Guid) || 
                                                type.IsEnum;
}
