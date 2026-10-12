namespace FWO.Middleware.Server.OpenApi;

/// <summary>
/// Documents a request property as required in the generated OpenAPI schema without making the
/// deserializer or model binding enforce it.
/// </summary>
/// <remarks>
/// A contract that reports every key-level error of a request together cannot let the deserializer
/// reject a missing key: <c>[JsonRequired]</c>, the <c>required</c> modifier and
/// <c>[Required]</c> all abort before the endpoint validator runs, so the caller receives that one
/// error, in the generic ProblemDetails shape, instead of the endpoint's own aggregated error
/// response. Such a property is therefore declared nullable and checked by the validator, which
/// leaves the generated schema with no record that it is required. This attribute restores that
/// record for API documentation and client code generation, and changes nothing at run time.
/// <para>
/// Marking a property with it is a statement about documentation only. The validator of the
/// endpoint remains the single place that enforces the rule.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class OpenApiRequiredAttribute : Attribute;
