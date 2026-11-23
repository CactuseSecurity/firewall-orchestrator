using FWO.Basics;
using FWO.Data.Workflow;
using Newtonsoft.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace FWO.Data
{
    /// <summary>
    /// Type IDs of the external ticket system copy stored with an external request. A copy without type is generic, i.e.
    /// no Tufin SecureChange system.
    /// </summary>
    public static class BuiltInExternalTicketSystemTypes
    {
        public const int GenericId = 1;
        public const int TufinSecureChangeId = 2;
    }



    public partial class ExternalTicketSystem
    {
        private const string kRedactedAuthorization = "****";
        private const string kTufinSecureChangeName = "Tufin SecureChange";

        [JsonProperty(nameof(Id)), JsonPropertyName(nameof(Id))]
        public int Id { get; set; } = 0;

        [JsonProperty(nameof(TypeId)), JsonPropertyName(nameof(TypeId))]
        public int TypeId { get; set; } = BuiltInExternalTicketSystemTypes.GenericId;

        /// <summary>
        /// Credential used to log into the ticket system (basic authentication)
        /// </summary>
        [JsonProperty(nameof(CredentialId)), JsonPropertyName(nameof(CredentialId))]
        public int? CredentialId { get; set; }

        /// <summary>
        /// Legacy: Authorization header string, encrypted with the main key, of an external request created before the
        /// ticket systems referenced credentials (kept by the upgrade only where no credential could be assigned).
        /// Never set in the ticket system configuration or in new requests.
        /// </summary>
        [JsonProperty(nameof(Authorization), NullValueHandling = NullValueHandling.Ignore), JsonPropertyName(nameof(Authorization))]
        [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Authorization { get; set; }

        /// <summary>
        /// Authorization header string in plain text, built right before calls to the ticket system. Never serialized.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore, System.Text.Json.Serialization.JsonIgnore]
        public string AuthorizationHeader { get; set; } = "";

        [JsonProperty(nameof(Name)), JsonPropertyName(nameof(Name))]
        public string Name { get; set; } = "";

        [JsonProperty(nameof(Url)), JsonPropertyName(nameof(Url))]
        public string Url { get; set; } = "";

        [JsonProperty(nameof(LookupRequesterId)), JsonPropertyName(nameof(LookupRequesterId))]
        public bool LookupRequesterId { get; set; } = false;

        [JsonProperty(nameof(Templates)), JsonPropertyName(nameof(Templates))]
        public List<ExternalTicketTemplate> Templates { get; set; } = [];

        // just for backward compatibility
        [JsonProperty(nameof(TicketTemplate)), JsonPropertyName(nameof(TicketTemplate))]
        public string TicketTemplate { get; set; } = "";

        [JsonProperty(nameof(TasksTemplate)), JsonPropertyName(nameof(TasksTemplate))]
        public string TasksTemplate { get; set; } = "";

        [JsonProperty(nameof(ResponseTimeout)), JsonPropertyName(nameof(ResponseTimeout))]
        public int ResponseTimeout { get; set; } = 300;

        [JsonProperty(nameof(MaxAttempts)), JsonPropertyName(nameof(MaxAttempts))]
        public int MaxAttempts { get; set; } = 3;

        [JsonProperty(nameof(CyclesBetweenAttempts)), JsonPropertyName(nameof(CyclesBetweenAttempts))]
        public int CyclesBetweenAttempts { get; set; } = 5;

        /// <summary>
        /// Builds the Tufin SecureChange system a management hands its tasks to from its device specific settings:
        /// connection, requester lookup, response timeout and retries. The ticket templates are added separately.
        /// </summary>
        /// <param name="settings">Device specific settings in effect for the management.</param>
        public static ExternalTicketSystem FromSecureChangeSettings(DeviceSpecificSettings.GlobalDeviceSpecificSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            return new ExternalTicketSystem
            {
                TypeId = BuiltInExternalTicketSystemTypes.TufinSecureChangeId,
                Name = kTufinSecureChangeName,
                Url = settings.ScUrl,
                CredentialId = settings.ScCredential,
                LookupRequesterId = settings.ScLookupRequesterId,
                ResponseTimeout = settings.RequestResponseTimeout,
                MaxAttempts = settings.RequestMaxAttempts,
                CyclesBetweenAttempts = settings.RequestCyclesBetweenAttempts
            };
        }

        /// <summary>
        /// Hides the (encrypted) authorization of a ticket system stored with an external request, for display.
        /// </summary>
        /// <param name="ticketSystem">The ticket system as stored with the external request (JSON).</param>
        /// <returns>The ticket system with the value of the authorization replaced by "****".</returns>
        public static string RedactAuthorization(string ticketSystem)
        {
            return AuthorizationValueRegex().Replace(ticketSystem, $"$1\"{kRedactedAuthorization}\"");
        }

        /// <summary>
        /// Matches the non-empty string value of the "Authorization" key in a serialized ticket system.
        /// </summary>
        [GeneratedRegex(@"(""Authorization""\s*:\s*)""(?:[^""\\]|\\.)+""")]
        private static partial Regex AuthorizationValueRegex();

        public int MaxBundledTasks()
        {
            return TypeId switch
            {
                BuiltInExternalTicketSystemTypes.TufinSecureChangeId => SCConstants.SCMaxBundledTasks,
                _ => 1
            };
        }

        public bool BundleGateways()
        {
            return TypeId switch
            {
                BuiltInExternalTicketSystemTypes.TufinSecureChangeId => SCConstants.SCBundleGateways,
                _ => false
            };
        }

        public List<string> TaskTypesToBundleGateways()
        {
            return TypeId switch
            {
                BuiltInExternalTicketSystemTypes.TufinSecureChangeId => [WfTaskType.rule_modify.ToString(), WfTaskType.rule_delete.ToString()],
                _ => []
            };
        }

        public bool IsTufinSecureChange()
        {
            return TypeId == BuiltInExternalTicketSystemTypes.TufinSecureChangeId;
        }

    }

    public class ExternalTicketTemplate
    {
        [JsonProperty(nameof(TaskType)), JsonPropertyName(nameof(TaskType))]
        public string TaskType { get; set; } = "";

        [JsonProperty(nameof(TicketTemplate)), JsonPropertyName(nameof(TicketTemplate))]
        public string TicketTemplate { get; set; } = "";

        [JsonProperty(nameof(TasksTemplate)), JsonPropertyName(nameof(TasksTemplate))]
        public string TasksTemplate { get; set; } = "";

        [JsonProperty(nameof(ObjectTemplate)), JsonPropertyName(nameof(ObjectTemplate))]
        public string ObjectTemplate { get; set; } = "";

        [JsonProperty(nameof(ObjectTemplateShort)), JsonPropertyName(nameof(ObjectTemplateShort))]
        public string ObjectTemplateShort { get; set; } = "";

        [JsonProperty(nameof(IpTemplate)), JsonPropertyName(nameof(IpTemplate))]
        public string IpTemplate { get; set; } = "";

        [JsonProperty(nameof(NwObjGroupTemplate)), JsonPropertyName(nameof(NwObjGroupTemplate))]
        public string NwObjGroupTemplate { get; set; } = "";

        [JsonProperty(nameof(ServiceTemplate)), JsonPropertyName(nameof(ServiceTemplate))]
        public string ServiceTemplate { get; set; } = "";

        [JsonProperty(nameof(IcmpTemplate)), JsonPropertyName(nameof(IcmpTemplate))]
        public string IcmpTemplate { get; set; } = "";

        [JsonProperty(nameof(IpProtocolTemplate)), JsonPropertyName(nameof(IpProtocolTemplate))]
        public string IpProtocolTemplate { get; set; } = "";


        public bool Sanitize()
        {
            bool shortened = false;
            TaskType = TaskType.SanitizeMand(ref shortened);
            TicketTemplate = TicketTemplate.SanitizeJsonMand(ref shortened);
            TasksTemplate = TasksTemplate.SanitizeJsonMand(ref shortened);
            ObjectTemplate = ObjectTemplate.SanitizeJsonMand(ref shortened);
            ObjectTemplateShort = ObjectTemplateShort.SanitizeJsonMand(ref shortened);
            IpTemplate = IpTemplate.SanitizeJsonMand(ref shortened);
            NwObjGroupTemplate = NwObjGroupTemplate.SanitizeJsonMand(ref shortened);
            ServiceTemplate = ServiceTemplate.SanitizeJsonMand(ref shortened);
            IcmpTemplate = IcmpTemplate.SanitizeJsonMand(ref shortened);
            IpProtocolTemplate = IpProtocolTemplate.SanitizeJsonMand(ref shortened);
            return shortened;
        }
    }
}
