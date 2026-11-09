using System.Collections;
using System.Reflection;

namespace FWO.Middleware.Server.OpenApi;

/// <summary>
/// Resolves API examples from typed providers and a generator-agnostic DTO fallback.
/// </summary>
public sealed class ApiExampleCatalog
{
    private readonly Dictionary<Type, IApiExampleProvider> providers;
    private readonly ApiExampleObjectFactory objectFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApiExampleCatalog"/> class.
    /// </summary>
    public ApiExampleCatalog(IEnumerable<IApiExampleProvider> providers, ApiExampleObjectFactory objectFactory)
    {
        this.providers = providers.ToDictionary(provider => provider.ExampleType);
        this.objectFactory = objectFactory;
    }

    /// <summary>
    /// Tries to create an example for the requested API type.
    /// </summary>
    public bool TryGetExample(Type apiType, out object? example)
    {
        Type normalizedType = NormalizeType(apiType);
        if (providers.TryGetValue(normalizedType, out IApiExampleProvider? provider))
        {
            example = provider.GetExampleObject();
            return true;
        }

        if (TryCreateCollectionExample(normalizedType, out example))
        {
            return true;
        }

        return objectFactory.TryCreate(normalizedType, out example);
    }

    private bool TryCreateCollectionExample(Type type, out object? example)
    {
        example = null;
        Type? itemType = GetCollectionItemType(type);
        if (itemType == null || !TryGetExample(itemType, out object? itemExample) || itemExample == null)
        {
            return false;
        }

        IList list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(itemType))!;
        list.Add(itemExample);
        example = list;
        return true;
    }

    private static Type NormalizeType(Type type)
    {
        return Nullable.GetUnderlyingType(type) ?? type;
    }

    private static Type? GetCollectionItemType(Type type)
    {
        if (type.IsArray)
        {
            return type.GetElementType();
        }

        if (!type.IsGenericType)
        {
            return null;
        }

        Type genericDefinition = type.GetGenericTypeDefinition();
        if (genericDefinition == typeof(List<>) || genericDefinition == typeof(IEnumerable<>))
        {
            return type.GetGenericArguments()[0];
        }

        return type.GetInterfaces()
            .FirstOrDefault(interfaceType => interfaceType.IsGenericType
                && interfaceType.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            ?.GetGenericArguments()[0];
    }
}

/// <summary>
/// Creates real DTO instances for API types without hand-written example providers.
/// </summary>
public sealed class ApiExampleObjectFactory
{
    private const int kMaxDepth = 2;

    /// <summary>
    /// Tries to create an example object for an API type.
    /// </summary>
    public bool TryCreate(Type type, out object? example)
    {
        return TryCreate(type, string.Empty, 0, out example);
    }

    private bool TryCreate(Type type, string propertyName, int depth, out object? example)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (TryCreateScalar(type, propertyName, out example))
        {
            return true;
        }

        if (TryCreateDictionary(type, depth, out example) || TryCreateList(type, depth, out example))
        {
            return true;
        }

        if (depth > kMaxDepth || !CanCreateDto(type))
        {
            example = null;
            return false;
        }

        example = Activator.CreateInstance(type);
        SetExampleProperties(type, example, depth);
        return true;
    }

    private void SetExampleProperties(Type type, object? target, int depth)
    {
        if (target == null)
        {
            return;
        }

        foreach (PropertyInfo property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (!property.CanWrite || !TryCreate(property.PropertyType, property.Name, depth + 1, out object? value))
            {
                continue;
            }

            property.SetValue(target, value);
        }
    }

    private static bool TryCreateScalar(Type type, string propertyName, out object? example)
    {
        example = type switch
        {
            Type stringType when stringType == typeof(string) => CreateStringValue(propertyName),
            Type boolType when boolType == typeof(bool) => true,
            Type intType when intType == typeof(int) => CreateIntegerValue(propertyName),
            Type longType when longType == typeof(long) => (long)CreateIntegerValue(propertyName),
            Type dateType when dateType == typeof(DateTime) => new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc),
            Type guidType when guidType == typeof(Guid) => Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Type enumType when enumType.IsEnum => Enum.GetValues(enumType).GetValue(0),
            _ => null
        };
        return example != null;
    }

    private bool TryCreateDictionary(Type type, int depth, out object? example)
    {
        example = null;
        if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(Dictionary<,>))
        {
            return false;
        }

        Type[] arguments = type.GetGenericArguments();
        if (arguments[0] != typeof(string) || !TryCreate(arguments[1], "value", depth + 1, out object? value))
        {
            return false;
        }

        IDictionary dictionary = (IDictionary)Activator.CreateInstance(type)!;
        dictionary["example"] = value;
        example = dictionary;
        return true;
    }

    private bool TryCreateList(Type type, int depth, out object? example)
    {
        example = null;
        if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(List<>))
        {
            return false;
        }

        Type itemType = type.GetGenericArguments()[0];
        if (!TryCreate(itemType, "item", depth + 1, out object? item))
        {
            return false;
        }

        IList list = (IList)Activator.CreateInstance(type)!;
        list.Add(item);
        example = list;
        return true;
    }

    private static bool CanCreateDto(Type type)
    {
        return type.GetConstructor(Type.EmptyTypes) != null
            && !type.IsAbstract
            && type.Namespace?.StartsWith("FWO.", StringComparison.Ordinal) == true;
    }

    private static string CreateStringValue(string propertyName)
    {
        string loweredName = propertyName.ToLowerInvariant();
        if (loweredName.Contains("password", StringComparison.Ordinal) || loweredName.Contains("token", StringComparison.Ordinal))
        {
            return "<redacted>";
        }

        if (loweredName.Contains("ip", StringComparison.Ordinal))
        {
            return "192.0.2.10";
        }

        if (loweredName.Contains("protocol", StringComparison.Ordinal))
        {
            return "tcp";
        }

        if (loweredName.Contains("action", StringComparison.Ordinal))
        {
            return "accept";
        }

        return string.IsNullOrWhiteSpace(propertyName) ? "example" : $"example-{propertyName}";
    }

    private static int CreateIntegerValue(string propertyName)
    {
        string loweredName = propertyName.ToLowerInvariant();
        if (loweredName.Contains("port", StringComparison.Ordinal))
        {
            return 443;
        }

        if (loweredName.Contains("mask", StringComparison.Ordinal) || loweredName.Contains("prefix", StringComparison.Ordinal))
        {
            return 24;
        }

        return 1;
    }
}
