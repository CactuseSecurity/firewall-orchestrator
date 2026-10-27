using FWO.Basics.Exceptions;
using FWO.Data.DeviceSpecificSettings;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace FWO.ExternalSystems.FirewallWrite
{
    /// <summary>
    /// Replaces the placeholders of an endpoint and a body template with the values of a step. Values are inserted in one
    /// pass and with '@' escaped, so data from the implementation task can never introduce a placeholder. The result
    /// is checked for unresolved placeholders and valid JSON before anything is sent.
    /// </summary>
    public static partial class FirewallTemplateRenderer
    {
        private const string kAtSign = "@";
        private const string kJsonEscapedAtSign = "\\u0040";
        private const string kRuntimeValidationValue = "0";

        [GeneratedRegex("@@[A-Z0-9_]+@@")]
        private static partial Regex PlaceholderPattern();

        /// <summary>
        /// Renders one step. Placeholders resolved by the vendor client at execution time
        /// (<see cref="FirewallWritePlaceholders.RuntimePlaceholders"/>) are kept.
        /// </summary>
        /// <exception cref="ConfigException">Thrown for an invalid endpoint, an unresolved placeholder or an invalid JSON body.</exception>
        public static RenderedFirewallRequest Render(FirewallWriteStep step, string endpointSetting, string bodyTemplate)
        {
            ArgumentNullException.ThrowIfNull(step);
            FirewallWriteEndpoint endpoint = ParseEndpoint(step.Operation, endpointSetting);

            List<string> unresolved = [];
            string path = Replace(endpoint.Path, step.Values, EncodeForPath, unresolved);
            string? body = string.IsNullOrWhiteSpace(bodyTemplate) ? null : Replace(bodyTemplate, step.Values, EncodeForJson, unresolved);

            List<string> missing = [.. unresolved.Where(placeholder => !FirewallWritePlaceholders.RuntimePlaceholders.Contains(placeholder)).Distinct()];
            if (missing.Count > 0)
            {
                throw new ConfigException($"Template error in {step.Operation}. Unhandled placeholder found: {string.Join(", ", missing)}.");
            }

            if (body != null)
            {
                CheckJson(step.Operation, ReplaceRuntimePlaceholdersForValidation(body));
            }
            return new RenderedFirewallRequest { Step = step, Method = endpoint.Method, Path = path, Body = body };
        }

        /// <summary>
        /// Resolves the runtime placeholders of a rendered request with values read from the firewall, then checks again
        /// that no placeholder is left and the body is valid JSON.
        /// </summary>
        /// <exception cref="ConfigException">Thrown for an unresolved placeholder or an invalid JSON body.</exception>
        public static void ResolveRuntimePlaceholders(RenderedFirewallRequest request, IReadOnlyDictionary<string, FirewallPlaceholderValue> values)
        {
            ArgumentNullException.ThrowIfNull(request);
            List<string> unresolved = [];
            request.Path = Replace(request.Path, values, EncodeForPath, unresolved);
            if (request.Body != null)
            {
                request.Body = Replace(request.Body, values, EncodeForJson, unresolved);
            }
            EnsureResolved(request, unresolved);
        }

        /// <summary>Checks that a request contains no placeholder any more and has a valid JSON body.</summary>
        /// <exception cref="ConfigException">Thrown for an unresolved placeholder or an invalid JSON body.</exception>
        public static void EnsureResolved(RenderedFirewallRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            List<string> unresolved = [.. PlaceholderPattern().Matches(request.Path).Select(match => match.Value)];
            if (request.Body != null)
            {
                unresolved.AddRange(PlaceholderPattern().Matches(request.Body).Select(match => match.Value));
            }
            EnsureResolved(request, unresolved);
        }

        /// <summary>Whether the rendered request still contains the given runtime placeholder.</summary>
        public static bool Contains(RenderedFirewallRequest request, string placeholder)
        {
            return request.Path.Contains(placeholder) || (request.Body?.Contains(placeholder) ?? false);
        }

        private static void EnsureResolved(RenderedFirewallRequest request, List<string> unresolved)
        {
            if (unresolved.Count > 0)
            {
                throw new ConfigException($"Template error in {request.Operation}. Unhandled placeholder found: {string.Join(", ", unresolved.Distinct())}.");
            }
            if (request.Body != null)
            {
                CheckJson(request.Operation, request.Body);
            }
        }

        private static FirewallWriteEndpoint ParseEndpoint(FirewallWriteOperation operation, string endpointSetting)
        {
            try
            {
                return FirewallWriteEndpoint.Parse(endpointSetting ?? "");
            }
            catch (ArgumentException exception)
            {
                throw new ConfigException($"Endpoint error in {operation}. {exception.Message}", exception);
            }
        }

        private static string Replace(string template, IReadOnlyDictionary<string, FirewallPlaceholderValue> values,
            Func<FirewallPlaceholderValue, string> encode, List<string> unresolved)
        {
            return PlaceholderPattern().Replace(template, match =>
            {
                if (values.TryGetValue(match.Value, out FirewallPlaceholderValue? value))
                {
                    return encode(value);
                }
                unresolved.Add(match.Value);
                return match.Value;
            });
        }

        /// <summary>Text values are inserted into JSON strings, JSON values as they are; '@' only occurs inside JSON strings.</summary>
        private static string EncodeForJson(FirewallPlaceholderValue value)
        {
            string encoded = value.IsJson ? value.Value : JsonEncodedText.Encode(value.Value).Value;
            return encoded.Replace(kAtSign, kJsonEscapedAtSign);
        }

        private static string EncodeForPath(FirewallPlaceholderValue value)
        {
            return Uri.EscapeDataString(value.Value);
        }

        /// <summary>Runtime placeholders are valid JSON both inside a string and as a value when replaced by a number.</summary>
        private static string ReplaceRuntimePlaceholdersForValidation(string body)
        {
            foreach (string placeholder in FirewallWritePlaceholders.RuntimePlaceholders)
            {
                body = body.Replace(placeholder, kRuntimeValidationValue);
            }
            return body;
        }

        private static void CheckJson(FirewallWriteOperation operation, string body)
        {
            try
            {
                _ = JsonNode.Parse(body) ?? throw new ConfigException($"Template error in {operation}. The rendered body is null.");
            }
            catch (JsonException exception)
            {
                throw new ConfigException($"Template error in {operation}. The rendered body is no valid JSON: {exception.Message}", exception);
            }
        }
    }
}
