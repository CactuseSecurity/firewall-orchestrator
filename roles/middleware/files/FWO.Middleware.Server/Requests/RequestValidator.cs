using System.Collections;
using System.Reflection;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;

namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Validates request DTOs against request validation schemas.
/// </summary>
public static class RequestValidator
{
    /// <summary>
    /// Validates the request and returns a uniform bad-request result when validation fails.
    /// </summary>
    public static bool TryValidate(object? request, RequestValidationSchema schema, out ActionResult? errorResult)
    {
        ArgumentNullException.ThrowIfNull(schema);

        RequestValidationErrors errors = new();
        ValidateObject(request, schema, RequestFieldPath.Root, errors);
        if (!errors.HasErrors)
        {
            errorResult = null;
            return true;
        }

        errorResult = RequestValidationProblemDetailsFactory.BadRequest(errors);
        return false;
    }

    /// <summary>
    /// Validates the request and returns the collected validation errors.
    /// </summary>
    public static RequestValidationErrors Validate(object? request, RequestValidationSchema schema)
    {
        ArgumentNullException.ThrowIfNull(schema);

        RequestValidationErrors errors = new();
        ValidateObject(request, schema, RequestFieldPath.Root, errors);
        return errors;
    }

    private static void ValidateObject(
        object? requestObject,
        RequestValidationSchema schema,
        string objectPath,
        RequestValidationErrors errors)
    {
        if (requestObject is null)
        {
            errors.Add(objectPath, "Request body is required.");
            return;
        }

        AddUnknownFields(requestObject, schema, objectPath, errors);

        foreach (RequestValidationField field in schema.Fields)
        {
            ValidateField(requestObject, field, objectPath, errors);
        }
    }

    private static void ValidateField(
        object requestObject,
        RequestValidationField field,
        string parentPath,
        RequestValidationErrors errors)
    {
        string fieldPath = RequestFieldPath.Child(parentPath, field.JsonName);
        object? value = GetJsonPropertyValue(requestObject, field.JsonName);

        if (value is null)
        {
            if (field.Required)
            {
                errors.AddMissingRequiredField(fieldPath);
            }
            return;
        }

        if (field.Kind == RequestValidationFieldKind.Object)
        {
            ValidateNestedObject(value, field, fieldPath, errors);
            return;
        }

        if (field.Kind == RequestValidationFieldKind.List)
        {
            ValidateList(value, field, fieldPath, errors);
        }
    }

    private static void ValidateNestedObject(
        object value,
        RequestValidationField field,
        string fieldPath,
        RequestValidationErrors errors)
    {
        if (field.NestedSchema is null)
        {
            return;
        }

        if (value is string || value is IEnumerable)
        {
            errors.Add(fieldPath, $"Field '{fieldPath}' must be an object.");
            return;
        }

        ValidateObject(value, field.NestedSchema, fieldPath, errors);
    }

    private static void ValidateList(
        object value,
        RequestValidationField field,
        string fieldPath,
        RequestValidationErrors errors)
    {
        if (value is string || value is not IEnumerable items)
        {
            errors.Add(fieldPath, $"Field '{fieldPath}' must be a list.");
            return;
        }

        if (field.NestedSchema is null)
        {
            return;
        }

        int index = 0;
        foreach (object? item in items)
        {
            string itemPath = RequestFieldPath.Indexed(fieldPath, index);
            if (item is null)
            {
                errors.Add(itemPath, $"Field '{itemPath}' must be an object.");
            }
            else
            {
                ValidateObject(item, field.NestedSchema, itemPath, errors);
            }

            index++;
        }
    }

    private static void AddUnknownFields(
        object requestObject,
        RequestValidationSchema schema,
        string objectPath,
        RequestValidationErrors errors)
    {
        if (requestObject is not IRequestWithAdditionalData requestWithAdditionalData)
        {
            errors.Add(
                objectPath,
                $"Request object '{objectPath}' must implement {nameof(IRequestWithAdditionalData)} so unknown fields can be validated.");
            return;
        }

        if (requestWithAdditionalData.AdditionalData is not { Count: > 0 })
        {
            return;
        }

        foreach (string unknownFieldName in requestWithAdditionalData.AdditionalData.Keys)
        {
            if (schema.Fields.Any(field => string.Equals(field.JsonName, unknownFieldName, StringComparison.Ordinal)))
            {
                continue;
            }

            errors.AddUnknownField(RequestFieldPath.Child(objectPath, unknownFieldName));
        }
    }

    private static object? GetJsonPropertyValue(object requestObject, string jsonName)
    {
        PropertyInfo? property = requestObject
            .GetType()
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .FirstOrDefault(candidate => string.Equals(GetJsonName(candidate), jsonName, StringComparison.Ordinal));

        return property?.GetValue(requestObject);
    }

    private static string GetJsonName(PropertyInfo property)
    {
        return property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? property.Name;
    }
}
