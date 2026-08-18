namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Describes the accepted JSON shape for one request object.
/// </summary>
public sealed class RequestValidationSchema
{
    private readonly List<RequestValidationField> fields = [];

    private RequestValidationSchema(string endpointName)
    {
        EndpointName = endpointName;
    }

    /// <summary>
    /// Gets the endpoint or schema name used for diagnostics.
    /// </summary>
    public string EndpointName { get; }

    /// <summary>
    /// Gets the fields accepted by this schema.
    /// </summary>
    public IReadOnlyList<RequestValidationField> Fields => fields;

    /// <summary>
    /// Creates a validation schema for an endpoint request body.
    /// </summary>
    public static RequestValidationSchema Endpoint(string endpointName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpointName);

        return new RequestValidationSchema(endpointName);
    }

    /// <summary>
    /// Creates a validation schema for a standard middleware POST request body with an optional JSON options object.
    /// </summary>
    public static RequestValidationSchema EndpointWithOptions(string endpointName, Action<RequestValidationSchema>? configureOptions = null)
    {
        RequestValidationSchema schema = Endpoint(endpointName).ObjectRoot();
        schema.OptionalObject("options", options =>
        {
            configureOptions?.Invoke(options);
        });
        return schema;
    }

    /// <summary>
    /// Marks this schema as describing an object root.
    /// </summary>
    public RequestValidationSchema ObjectRoot()
    {
        return this;
    }

    /// <summary>
    /// Adds an optional object field.
    /// </summary>
    public RequestValidationSchema OptionalObject(string jsonName, Action<RequestValidationSchema> configure)
    {
        return AddObject(jsonName, required: false, configure);
    }

    /// <summary>
    /// Adds a required object field.
    /// </summary>
    public RequestValidationSchema RequiredObject(string jsonName, Action<RequestValidationSchema> configure)
    {
        return AddObject(jsonName, required: true, configure);
    }

    /// <summary>
    /// Adds an optional string field.
    /// </summary>
    public RequestValidationSchema OptionalString(string jsonName)
    {
        return AddScalar(jsonName, RequestValidationFieldKind.String, required: false);
    }

    /// <summary>
    /// Adds a required string field.
    /// </summary>
    public RequestValidationSchema RequiredString(string jsonName)
    {
        return AddScalar(jsonName, RequestValidationFieldKind.String, required: true);
    }

    /// <summary>
    /// Adds an optional integer field.
    /// </summary>
    public RequestValidationSchema OptionalInt(string jsonName)
    {
        return AddScalar(jsonName, RequestValidationFieldKind.Int, required: false);
    }

    /// <summary>
    /// Adds a required integer field.
    /// </summary>
    public RequestValidationSchema RequiredInt(string jsonName)
    {
        return AddScalar(jsonName, RequestValidationFieldKind.Int, required: true);
    }

    /// <summary>
    /// Adds an optional boolean field.
    /// </summary>
    public RequestValidationSchema OptionalBool(string jsonName)
    {
        return AddScalar(jsonName, RequestValidationFieldKind.Bool, required: false);
    }

    /// <summary>
    /// Adds a required boolean field.
    /// </summary>
    public RequestValidationSchema RequiredBool(string jsonName)
    {
        return AddScalar(jsonName, RequestValidationFieldKind.Bool, required: true);
    }

    /// <summary>
    /// Adds an optional list field.
    /// </summary>
    public RequestValidationSchema OptionalList(string jsonName, Action<RequestValidationSchema>? configureItems = null)
    {
        return AddList(jsonName, required: false, configureItems);
    }

    /// <summary>
    /// Adds a required list field.
    /// </summary>
    public RequestValidationSchema RequiredList(string jsonName, Action<RequestValidationSchema>? configureItems = null)
    {
        return AddList(jsonName, required: true, configureItems);
    }

    private RequestValidationSchema AddObject(
        string jsonName,
        bool required,
        Action<RequestValidationSchema> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        RequestValidationSchema nestedSchema = Endpoint(jsonName).ObjectRoot();
        configure(nestedSchema);
        return AddField(new RequestValidationField(
            jsonName,
            RequestValidationFieldKind.Object,
            required,
            nestedSchema));
    }

    private RequestValidationSchema AddList(
        string jsonName,
        bool required,
        Action<RequestValidationSchema>? configureItems)
    {
        RequestValidationSchema? itemSchema = null;
        if (configureItems is not null)
        {
            itemSchema = Endpoint(jsonName).ObjectRoot();
            configureItems(itemSchema);
        }

        return AddField(new RequestValidationField(
            jsonName,
            RequestValidationFieldKind.List,
            required,
            itemSchema));
    }

    private RequestValidationSchema AddScalar(string jsonName, RequestValidationFieldKind kind, bool required)
    {
        return AddField(new RequestValidationField(jsonName, kind, required, null));
    }

    private RequestValidationSchema AddField(RequestValidationField field)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(field.JsonName);

        if (fields.Any(existingField => string.Equals(existingField.JsonName, field.JsonName, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException($"Request validation schema '{EndpointName}' already contains field '{field.JsonName}'.");
        }

        fields.Add(field);
        return this;
    }
}
