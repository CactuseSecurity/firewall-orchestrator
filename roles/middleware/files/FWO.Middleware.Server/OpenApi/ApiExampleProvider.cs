namespace FWO.Middleware.Server.OpenApi;

/// <summary>
/// Provides a generator-agnostic example object for one API DTO type.
/// </summary>
public interface IApiExampleProvider
{
    /// <summary>
    /// Gets the DTO type this provider supplies.
    /// </summary>
    Type ExampleType { get; }

    /// <summary>
    /// Gets the example DTO instance as an object for adapter code.
    /// </summary>
    object GetExampleObject();
}

/// <summary>
/// Provides a strongly typed example for one API DTO type.
/// </summary>
public abstract class ApiExampleProvider<T> : IApiExampleProvider
{
    /// <inheritdoc />
    public Type ExampleType => typeof(T);

    /// <summary>
    /// Gets the strongly typed example DTO instance.
    /// </summary>
    public abstract T GetExample();

    /// <inheritdoc />
    public object GetExampleObject()
    {
        return GetExample() ?? throw new InvalidOperationException($"Example provider for {typeof(T).Name} returned null.");
    }
}
