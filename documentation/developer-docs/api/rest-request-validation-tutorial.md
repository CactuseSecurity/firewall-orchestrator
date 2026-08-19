# REST Request Validation Tutorial

This tutorial describes the request validation pattern for migrated middleware REST POST endpoints.

Use this pattern for new migrated endpoints and for future endpoint migrations. The current examples are based on the migrated flow catalog, flow compliance, policy ID, and zone resolution endpoints.

## Contract

Every migrated POST request DTO must expose an `options` property in C#.

The JSON `options` field is optional unless an endpoint explicitly documents a stricter rule. If the client omits `options`, the endpoint treats it like an empty object:

```json
{}
```

is equivalent to:

```json
{
  "options": {}
}
```

Endpoint business payload stays at the request root. Cross-cutting controls such as filters, paging, sort settings, and similar optional controls belong under `options`.

For example, a lookup request keeps its lookup fields at root and puts the visibility filter under `options.filter`:

```json
{
  "ipStart": "10.0.0.1",
  "ipEnd": "10.0.0.2",
  "options": {
    "filter": {
      "visibleInRequest": true
    }
  }
}
```

## Validation Response

Migrated endpoints return `400 Bad Request` with `ValidationProblemDetails` for request-shape and endpoint-local semantic validation failures.

Field paths use these rules:

- `$` for body-level errors.
- Dot notation for object fields, for example `options.filter.visibleInRequest`.
- Index notation for list items, for example `source[0].ipStart`.

Unknown JSON fields and missing required fields should be reported before downstream API or database calls run.

Malformed JSON and wrong CLR value types are still ASP.NET model binding errors in this version. Do not try to solve those in endpoint-local validators.

## Create the Request DTO

Derive the request body from `RequestDto<TOptions>`.

For an endpoint with no options yet, use the base options type:

```csharp
public sealed class GetPolicyIdsRequest : RequestDto<RequestOptionsDto>
{
}
```

For a simple filtered endpoint, use the concrete generic options type:

```csharp
public sealed class GetAddressObjectsRequest
    : RequestDto<RequestOptionsDto<VisibleInRequestFilter>>, IVisibleInRequestFilterRequest
{
}
```

Define filters by deriving from `RequestFilterDto`:

```csharp
public sealed class VisibleInRequestFilter : RequestFilterDto
{
    [JsonPropertyName("visibleInRequest")]
    public bool? VisibleInRequest { get; set; }
}
```

When an endpoint needs additional options beyond `filter`, create a named options type:

```csharp
public sealed class ApplicationAddressOptions : RequestOptionsDto<ApplicationAddressFilter>
{
    [JsonPropertyName("limit")]
    public int? Limit { get; set; }

    [JsonPropertyName("offset")]
    public int? Offset { get; set; }
}
```

Then use it from the request DTO:

```csharp
public sealed class GetApplicationAddressesRequest : RequestDto<ApplicationAddressOptions>
{
}
```

Do not create empty endpoint-specific options classes just to wrap a filter. Use `RequestOptionsDto<TFilter>` until the endpoint needs additional option fields or endpoint-specific option documentation.

## Capture Unknown Fields

The base request, options, and filter DTOs already implement `[JsonExtensionData]`.

Nested business objects still need to implement `IRequestWithAdditionalData` themselves. For example, list item DTOs in a request payload should capture unknown fields:

```csharp
public sealed class IpRangeRequest : IRequestWithAdditionalData
{
    [JsonPropertyName("ipStart")]
    public string? IpStart { get; set; }

    [JsonPropertyName("ipEnd")]
    public string? IpEnd { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }
}
```

Do not use `[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]` on migrated request DTOs or nested DTOs. It can fail during deserialization before the controller can return the uniform validation response.

## Define the Schema

Use `RequestValidationSchema.EndpointWithOptions(...)` for migrated POST endpoints.

For an endpoint with no current options:

```csharp
private static readonly RequestValidationSchema PolicyIdsSchema =
    RequestValidationSchema.EndpointWithOptions("getPolicyIds");
```

For an endpoint with `options.filter.visibleInRequest`:

```csharp
private static RequestValidationSchema CreateVisibleInRequestSchema(string endpointName)
{
    return RequestValidationSchema.EndpointWithOptions(endpointName, options => options
        .OptionalObject("filter", filter => filter
            .OptionalBool("visibleInRequest")));
}
```

For a lookup endpoint, keep business fields at root and add them after the options schema:

```csharp
private static readonly RequestValidationSchema AddressObjectIdSchema =
    CreateVisibleInRequestSchema(nameof(GetAddressObjectId))
        .RequiredString("ipStart")
        .RequiredString("ipEnd");
```

For list item validation, configure the list item object schema:

```csharp
private static readonly RequestValidationSchema FlowComplianceSchema =
    RequestValidationSchema.EndpointWithOptions("getFlowComplianceState")
        .OptionalList("source", item => item
            .RequiredString("ipStart")
            .RequiredString("ipEnd"))
        .OptionalList("destination", item => item
            .RequiredString("ipStart")
            .RequiredString("ipEnd"))
        .OptionalList("service", item => item
            .RequiredInt("portStart")
            .RequiredInt("portEnd")
            .RequiredString("protocol"))
        .OptionalList("policies");
```

Version 1 schema validation checks request shape only: allowed fields, required fields, optional objects, nested objects, lists, and unknown fields. Keep business behavior and database access outside the schema.

## Call the Validator

Call the validator explicitly near the start of the controller action:

```csharp
public async Task<ActionResult<List<AddressObjectResponse>>> GetAddressObjects(
    [FromBody] GetAddressObjectsRequest request)
{
    if (!RequestValidator.TryValidate(request, AddressObjectsSchema, out ActionResult? errorResult))
    {
        return errorResult!;
    }

    return Ok(await flowCatalogService.GetAddressObjectsAsync(
        request.Options?.Filter?.VisibleInRequest));
}
```

The controller may access `request.Options?.Filter` because `options` is optional in JSON. If the endpoint wants a local default object before using several option values, create the default after validation:

```csharp
RequestOptionsDto options = request.Options ?? new RequestOptionsDto();
```

## Preserve Semantic Validation

Keep endpoint-specific semantic checks after shape validation. 
In a second version this can be extended and done by the validator directly. This is the reason we use e.g. `.RequiredString()` instead of implicitly inferring from DTO types.

Example:

```csharp
if (string.IsNullOrWhiteSpace(request.Protocol))
{
    return BuildValidationError("protocol", "'protocol' is required.");
}
```

Return semantic errors through the same response factory:

```csharp
private static BadRequestObjectResult BuildValidationError(string fieldPath, string message)
{
    RequestValidationErrors errors = new();
    errors.Add(fieldPath, message);
    return RequestValidationProblemDetailsFactory.BadRequest(errors);
}
```

Do not query Hasura, LDAP, or other downstream systems before request validation succeeds.

Later semantic rules should fit into the same structure as structure validation. Here is a possible example:

```csharp
.RequiredString("protocol", rules => rules.OneOf("tcp", "udp", "icmp"))
.RequiredInt("portStart", rules => rules.Min(0).Max(65535))
.Custom("portStart", "portEnd", ValidatePortRange)
```


## Normalize After Validation

Do not mutate DTOs during generic shape validation.

Endpoint-local semantic validation may normalize values after the shape is known to be valid. For example, the flow catalog address lookup removes allowed `/32` masks after validation and before querying:

```csharp
if (!FlowComplianceRequestValidator.TryValidateAndNormalizeIpRange( request.IpStart, request.IpEnd, "address", 0, out string normalizedIpStart, out string normalizedIpEnd, out string? addressErrorMessage))
{
    return BuildValidationError("address[0]", addressErrorMessage!);
}

request.IpStart = normalizedIpStart;
request.IpEnd = normalizedIpEnd;
```

## Update OpenAPI Examples

### This is temporary and should in the future be done automatically

Add or update examples in `roles/middleware/files/FWO.Middleware.Server/OpenApi/ApiExampleServiceCollectionExtensions.cs` when the fallback example is not good enough.

For migrated validation failures, use paths under `options` when the failing field is an option:

```csharp
["options.filter.visibleInRequestTypo"] =
    ["Unknown field 'options.filter.visibleInRequestTypo'."]
```

Add endpoint-specific OpenAPI documentation through an `IOpenApiEndpointDocumentationProvider` when the endpoint needs validation remarks, role details, or examples beyond the DTO fallback.

Document these points for each migrated endpoint:

- Which roles can call it.
- Which request fields are required.
- Which options exist and what defaults apply when `options` is omitted.
- Which semantic checks run after shape validation.
- That malformed JSON and wrong JSON value types are handled by ASP.NET model binding.

## Add Tests

Add validator-level tests that deserialize realistic JSON and call `RequestValidator`.

Important cases:

- `{}` is accepted when the endpoint has no required root business fields.
- `{"options":{}}` is accepted.
- `{"options":{"filter":{}}}` is accepted for filtered endpoints.
- `{"options":{"filter":{"visibleInRequest":true}}}` is accepted for visible-in-request endpoints.
- Legacy root filter JSON is rejected on migrated endpoints:

```json
{
  "filter": {
    "visibleInRequest": true
  }
}
```

- Unknown root fields are rejected.
- Unknown `options` fields are rejected.
- Unknown `options.filter` fields are rejected.
- Missing required root business fields are rejected.
- Multiple detectable shape errors are returned together.

Add controller-level tests for behavior that depends on action code:

- Invalid requests return before downstream API calls.
- Semantic validation returns `ValidationProblemDetails`.
- Normalization happens after validation.

Add OpenAPI/example tests when examples or documentation providers are touched:

- Request examples include `options`.
- Validation examples use the expected field path.
- Migrated actions declare `400 Bad Request` with `ValidationProblemDetails`.

## Migration Checklist

Use this checklist when migrating another endpoint:

- [ ] Move the request DTO to `RequestDto<TOptions>`.
- [ ] Put filters under `options.filter`.
- [ ] Keep endpoint business payload at root.
- [ ] Use `RequestOptionsDto<TFilter>` for simple filtered endpoints.
- [ ] Create a named options DTO only when extra option fields are needed.
- [ ] Derive filter DTOs from `RequestFilterDto`.
- [ ] Ensure nested request objects implement `IRequestWithAdditionalData`.
- [ ] Remove `[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]` from migrated DTOs.
- [ ] Define the schema with `RequestValidationSchema.EndpointWithOptions(...)`.
- [ ] Call `RequestValidator.TryValidate(...)` before downstream work.
- [ ] Return semantic validation through `RequestValidationProblemDetailsFactory`.
- [ ] Update OpenAPI examples and endpoint documentation.
- [ ] Add validator, controller, and OpenAPI tests for the migrated shape.
