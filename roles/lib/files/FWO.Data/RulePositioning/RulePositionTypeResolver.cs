using System.Reflection;

namespace FWO.Data.RulePositioning;

internal static class RulePositionTypeResolver
{
    /// <summary>
    /// Finds the one loaded concrete rule-position subtype registered for a discriminator.
    /// </summary>
    internal static Type? Resolve(string discriminator)
    {
        List<Type> matches = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(GetLoadableTypes)
            .Where(type => !type.IsAbstract && typeof(RulePosition).IsAssignableFrom(type))
            .Where(type => string.Equals(
                type.GetCustomAttribute<RulePositionTypeAttribute>()?.Discriminator,
                discriminator,
                StringComparison.Ordinal))
            .ToList();

        return matches.Count switch
        {
            0 => null,
            1 => matches[0],
            _ => throw new InvalidOperationException(
                "More than one rule-position type uses the same discriminator.")
        };
    }

    /// <summary>
    /// Gets the stable discriminator declared directly on a known subtype.
    /// </summary>
    internal static string GetRequiredDiscriminator(Type positionType)
    {
        RulePositionTypeAttribute? attribute = positionType.GetCustomAttribute<RulePositionTypeAttribute>();
        return attribute?.Discriminator
            ?? throw new InvalidOperationException(
                $"Rule-position type '{positionType.FullName}' has no {nameof(RulePositionTypeAttribute)}.");
    }

    /// <summary>
    /// Returns all types that can safely be examined from an assembly.
    /// </summary>
    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types.OfType<Type>();
        }
    }
}
