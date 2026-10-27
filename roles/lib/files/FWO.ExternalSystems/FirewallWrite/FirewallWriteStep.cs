using FWO.Data.DeviceSpecificSettings;
using System.Text.Json;

namespace FWO.ExternalSystems.FirewallWrite
{
    /// <summary>
    /// Value of one placeholder. Text values are escaped for the place they are inserted at, JSON values are inserted as they are.
    /// </summary>
    public sealed record FirewallPlaceholderValue(string Value, bool IsJson);

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
    }
}
