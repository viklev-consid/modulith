using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Modulith.Shared.Kernel.Gdpr;
using Serilog.Core;
using Serilog.Events;

namespace Modulith.Shared.Infrastructure.Logging;

public sealed class PersonalDataDestructuringPolicy : IDestructuringPolicy
{
    private static readonly HashSet<string> SensitiveNamePatterns =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "password", "token", "secret", "key", "credential", "apikey", "accesskey",
        };

    public bool TryDestructure(
        object value,
        ILogEventPropertyValueFactory propertyValueFactory,
        [NotNullWhen(true)] out LogEventPropertyValue? result)
    {
        var type = value.GetType();
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        var hasClassifiedProperty = properties.Any(p =>
            p.IsDefined(typeof(PersonalDataAttribute), inherit: true) ||
            p.IsDefined(typeof(SensitivePersonalDataAttribute), inherit: true));

        if (!hasClassifiedProperty)
        {
            result = null;
            return false;
        }

        var logProperties = new List<LogEventProperty>(properties.Length);

        foreach (var prop in properties)
        {
            var isSensitive = prop.IsDefined(typeof(SensitivePersonalDataAttribute), inherit: true);
            var isPersonal = prop.IsDefined(typeof(PersonalDataAttribute), inherit: true);
            var isNameSensitive = SensitiveNamePatterns.Any(
                p => prop.Name.Contains(p, StringComparison.OrdinalIgnoreCase));

            LogEventPropertyValue logValue;
            if (isSensitive || isPersonal || isNameSensitive)
            {
                logValue = new ScalarValue("***");
            }
            else
            {
                var propValue = prop.GetValue(value);
                logValue = propValue is null
                    ? new ScalarValue(null)
                    : propertyValueFactory.CreatePropertyValue(propValue, destructureObjects: true);
            }

            logProperties.Add(new LogEventProperty(prop.Name, logValue));
        }

        result = new StructureValue(logProperties);
        return true;
    }
}
