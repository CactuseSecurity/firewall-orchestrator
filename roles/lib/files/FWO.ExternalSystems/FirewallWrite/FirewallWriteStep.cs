using FWO.Data.DeviceSpecificSettings;
using System.Text.Json;

namespace FWO.ExternalSystems.FirewallWrite
{
    /// <summary>
    /// Value of one placeholder. Text values are escaped for the place they are inserted at, JSON values are inserted as they are.
    /// A value with <see cref="MissingReason"/> has no content: rendering a template that uses it fails with that reason, so
    /// that nothing is sent with an empty or widened value.
    /// </summary>
    public sealed record FirewallPlaceholderValue(string Value, bool IsJson, string? MissingReason = null)
    {
        /// <summary>A placeholder that has no value for the step, with the reason shown when a template uses it.</summary>
        public static FirewallPlaceholderValue Missing(string reason) => new("", false, reason);
    }

    /// <summary>
    /// One planned firewall write call: the operation and the placeholder values taken from the implementation task.
    /// </summary>
    public sealed class FirewallWriteStep(FirewallWriteOperation operation)
    {
        public FirewallWriteOperation Operation { get; } = operation;

        /// <summary>Placeholder values keyed by placeholder, e.g. "@@OBJECT_NAME@@".</summary>
        public Dictionary<string, FirewallPlaceholderValue> Values { get; } = [];

        /// <summary>Name of the object, group or rule the call changes; used by vendor clients that look up IDs by name.</summary>
        public string TargetName { get; set; } = "";

        /// <summary>Whether the step writes an IPv6 network object; selects the IPv6 defaults of vendors with separate IPv6 tables.</summary>
        public bool IsIpv6 { get; init; }

        /// <summary>Members to add to a group, as rendered into the request.</summary>
        public List<string> MembersToAdd { get; } = [];

        /// <summary>Members to remove from a group, as rendered into the request.</summary>
        public List<string> MembersToRemove { get; } = [];

        /// <summary>Sets a placeholder to a text value.</summary>
        public FirewallWriteStep SetText(string placeholder, string? value)
        {
            Values[placeholder] = new FirewallPlaceholderValue(value ?? "", false);
            return this;
        }

        /// <summary>Sets a placeholder to a text value, or marks it as missing with the given reason if the value is empty.</summary>
        public FirewallWriteStep SetRequiredText(string placeholder, string? value, string missingReason)
        {
            return string.IsNullOrWhiteSpace(value) ? SetMissing(placeholder, missingReason) : SetText(placeholder, value);
        }

        /// <summary>Marks a placeholder as having no value; a template that uses it fails with the reason.</summary>
        public FirewallWriteStep SetMissing(string placeholder, string reason)
        {
            Values[placeholder] = FirewallPlaceholderValue.Missing(reason);
            return this;
        }

        /// <summary>Sets a placeholder to a JSON array of strings.</summary>
        public FirewallWriteStep SetStringList(string placeholder, IEnumerable<string> values)
        {
            Values[placeholder] = new FirewallPlaceholderValue(JsonSerializer.Serialize(values.ToList()), true);
            return this;
        }

        /// <summary>Sets a placeholder to a JSON array of objects with a single "name" property, as used by Fortinet APIs.</summary>
        public FirewallWriteStep SetNameObjectList(string placeholder, IEnumerable<string> names)
        {
            List<Dictionary<string, string>> objects = [.. names.Select(name => new Dictionary<string, string> { { "name", name } })];
            Values[placeholder] = new FirewallPlaceholderValue(JsonSerializer.Serialize(objects), true);
            return this;
        }

        /// <summary>Sets a placeholder to a complete JSON value.</summary>
        public FirewallWriteStep SetJson(string placeholder, string json)
        {
            Values[placeholder] = new FirewallPlaceholderValue(json, true);
            return this;
        }

        /// <summary>Copies all values of another step that are not set in this step.</summary>
        public FirewallWriteStep AddMissing(IReadOnlyDictionary<string, FirewallPlaceholderValue> values)
        {
            foreach ((string placeholder, FirewallPlaceholderValue value) in values)
            {
                Values.TryAdd(placeholder, value);
            }
            return this;
        }
    }

    /// <summary>
    /// A rendered firewall write call: HTTP method, path and JSON body (null for calls without body).
    /// </summary>
    public sealed class RenderedFirewallRequest
    {
        public required FirewallWriteStep Step { get; init; }

        public FirewallWriteOperation Operation => Step.Operation;

        public required string Method { get; init; }

        public required string Path { get; set; }

        public string? Body { get; set; }

        /// <summary>Additional headers of the call, e.g. the If-Match condition of a read-modify-write.</summary>
        public Dictionary<string, string> Headers { get; } = [];
    }
}
