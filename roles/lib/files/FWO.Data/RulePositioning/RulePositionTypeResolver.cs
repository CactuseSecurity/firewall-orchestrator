using System.Reflection;

namespace FWO.Data.RulePositioning;

internal static class RulePositionTypeResolver
{
    private static readonly Lazy<IReadOnlyDictionary<string, Type>> kTypesByDiscriminator =
        new(BuildRegistry);

    /// <summary>
    /// Finds the one loaded concrete rule-position subtype registered for a discriminator.
    /// </summary>
    internal static Type? Resolve(string discriminator)
    {
        return kTypesByDiscriminator.Value.TryGetValue(discriminator, out Type? positionType)
            ? positionType
            : null;
    }

    /// <summary>
    /// Builds the immutable discriminator registry once for the lifetime of the process.
    /// </summary>
    private static IReadOnlyDictionary<string, Type> BuildRegistry()
    {
        List<(string Discriminator, Type PositionType)> registrations = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(GetLoadableTypes)
            .Where(type => !type.IsAbstract && typeof(RulePosition).IsAssignableFrom(type))
            .Select(type => (type.GetCustomAttribute<RulePositionTypeAttribute>()?.Discriminator, type))
            .Where(registration => !string.IsNullOrWhiteSpace(registration.Discriminator))
            .Select(registration => (registration.Discriminator!, registration.type))
            .ToList();

        IGrouping<string, (string Discriminator, Type PositionType)>? duplicate = registrations
            .GroupBy(registration => registration.Discriminator, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate != null)
        {
            throw new InvalidOperationException(
                "More than one rule-position type uses the same discriminator.");
        }

        return registrations.ToDictionary(
            registration => registration.Discriminator,
            registration => registration.PositionType,
            StringComparer.Ordinal);
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
