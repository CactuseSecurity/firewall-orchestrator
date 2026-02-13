using FWO.Basics;
using FWO.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FWO.Data
{
    /// <summary>
    /// Represents the selectable recipient options for modelling request/decommission emails.
    /// </summary>
    public class ModellingEmailRecipientSelection
    {
        [JsonPropertyName("none")]
        public bool None { get; set; } = true;

        [JsonPropertyName("other_addresses")]
        public bool OtherAddresses { get; set; } = false;

        [JsonPropertyName("ensure_at_least_one_notification")]
        public bool EnsureAtLeastOneNotification { get; set; } = false;

        [JsonPropertyName("owner_responsible_type_ids")]
        public List<int> OwnerResponsibleTypeIds { get; set; } = [];

        /// <summary>
        /// Parses a config value into a modelling recipient selection.
        /// Supports new JSON values and legacy enum names.
        /// </summary>
        public static ModellingEmailRecipientSelection Parse(string? configValue, IEnumerable<int>? activeOwnerResponsibleTypeIds = null)
        {
            string rawValue = configValue?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return new ModellingEmailRecipientSelection();
            }

            ModellingEmailRecipientSelection? parsedSelection = ParseJson(rawValue);
            if (parsedSelection == null)
            {
                parsedSelection = ParseLegacy(rawValue, activeOwnerResponsibleTypeIds);
            }

            return parsedSelection.Sanitize(activeOwnerResponsibleTypeIds);
        }

        /// <summary>
        /// Converts the selection into a persisted config value.
        /// </summary>
        public string ToConfigValue(IEnumerable<int>? activeOwnerResponsibleTypeIds = null)
        {
            ModellingEmailRecipientSelection sanitized = Sanitize(activeOwnerResponsibleTypeIds);
            if (!sanitized.HasAnyRecipientOption())
            {
                return nameof(EmailRecipientOption.None);
            }
            return JsonSerializer.Serialize(sanitized);
        }

        /// <summary>
        /// Returns true when at least one effective recipient option is selected.
        /// </summary>
        public bool HasAnyRecipientOption()
        {
            return !None && (OtherAddresses || OwnerResponsibleTypeIds.Count > 0);
        }

        /// <summary>
        /// Returns selected owner responsible types in fallback order.
        /// Highest sort order is tried first.
        /// </summary>
        public IEnumerable<int> GetOwnerResponsibleTypeFallbackOrder(IEnumerable<OwnerResponsibleType>? ownerResponsibleTypes)
        {
            HashSet<int> selectedTypeIds = OwnerResponsibleTypeIds.ToHashSet();
            if (selectedTypeIds.Count == 0)
            {
                return [];
            }

            if (ownerResponsibleTypes != null)
            {
                List<int> orderedTypeIds = ownerResponsibleTypes
                    .Where(type => type.Active && selectedTypeIds.Contains(type.Id))
                    .OrderByDescending(type => type.SortOrder)
                    .ThenByDescending(type => type.Id)
                    .Select(type => type.Id)
                    .ToList();

                if (orderedTypeIds.Count > 0)
                {
                    return orderedTypeIds;
                }
            }

            return selectedTypeIds.OrderByDescending(typeId => typeId).ToList();
        }

        private ModellingEmailRecipientSelection Sanitize(IEnumerable<int>? activeOwnerResponsibleTypeIds)
        {
            ModellingEmailRecipientSelection sanitized = new()
            {
                None = None,
                OtherAddresses = OtherAddresses,
                EnsureAtLeastOneNotification = EnsureAtLeastOneNotification,
                OwnerResponsibleTypeIds = OwnerResponsibleTypeIds
                    .Where(id => id > 0)
                    .Distinct()
                    .ToList()
            };

            if (activeOwnerResponsibleTypeIds != null)
            {
                HashSet<int> activeTypeIds = activeOwnerResponsibleTypeIds.ToHashSet();
                sanitized.OwnerResponsibleTypeIds = sanitized.OwnerResponsibleTypeIds
                    .Where(activeTypeIds.Contains)
                    .ToList();
            }

            if (sanitized.None)
            {
                sanitized.OtherAddresses = false;
                sanitized.EnsureAtLeastOneNotification = false;
                sanitized.OwnerResponsibleTypeIds = [];
            }
            else if (!sanitized.OtherAddresses && sanitized.OwnerResponsibleTypeIds.Count == 0)
            {
                sanitized.None = true;
            }

            return sanitized;
        }

        private static ModellingEmailRecipientSelection? ParseJson(string rawValue)
        {
            try
            {
                ModellingEmailRecipientSelection? parsedSelection = JsonSerializer.Deserialize<ModellingEmailRecipientSelection>(rawValue);
                if (parsedSelection != null)
                {
                    parsedSelection.OwnerResponsibleTypeIds ??= [];
                    return parsedSelection;
                }
            }
            catch
            {
                Log.WriteWarning(
                    "Parse ModellingEmailRecipientSelection",
                    $"Could not parse recipient selection JSON value \"{rawValue}\". Falling back to legacy parsing.");
            }

            return null;
        }

        private static ModellingEmailRecipientSelection ParseLegacy(string rawValue, IEnumerable<int>? activeOwnerResponsibleTypeIds)
        {
            ModellingEmailRecipientSelection selection = new();
            IEnumerable<int> activeTypeIds = activeOwnerResponsibleTypeIds ?? [GlobalConst.kOwnerResponsibleTypeMain, GlobalConst.kOwnerResponsibleTypeSupporting, GlobalConst.kOwnerResponsibleTypeOptionalEscalation];

            switch (rawValue)
            {
                case nameof(EmailRecipientOption.None):
                case "0":
                    selection.None = true;
                    break;
                case nameof(EmailRecipientOption.OtherAddresses):
                    selection.None = false;
                    selection.OtherAddresses = true;
                    break;
                case nameof(EmailRecipientOption.OwnerMainResponsible):
                    selection.None = false;
                    selection.OwnerResponsibleTypeIds = [GlobalConst.kOwnerResponsibleTypeMain];
                    break;
                case nameof(EmailRecipientOption.OwnerGroupOnly):
                    selection.None = false;
                    selection.OwnerResponsibleTypeIds = [GlobalConst.kOwnerResponsibleTypeSupporting];
                    break;
                case nameof(EmailRecipientOption.AllOwnerResponsibles):
                    selection.None = false;
                    selection.OwnerResponsibleTypeIds = activeTypeIds.ToList();
                    break;
                case nameof(EmailRecipientOption.FallbackToMainResponsibleIfOwnerGroupEmpty):
                    selection.None = false;
                    selection.EnsureAtLeastOneNotification = true;
                    selection.OwnerResponsibleTypeIds = [GlobalConst.kOwnerResponsibleTypeSupporting, GlobalConst.kOwnerResponsibleTypeMain];
                    break;
                default:
                    selection.None = true;
                    break;
            }

            return selection;
        }
    }
}
