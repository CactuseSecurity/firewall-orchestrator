using FWO.Basics;
using FWO.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FWO.Data
{
    /// <summary>
    /// Represents selectable email recipient options.
    /// </summary>
    public class EmailRecipientSelection
    {
        [JsonPropertyName("none")]
        public bool None { get; set; } = true;

        [JsonPropertyName("other_addresses")]
        public bool OtherAddresses { get; set; } = false;

        [JsonPropertyName("other_address_list")]
        public List<string> OtherAddressList { get; set; } = [];

        [JsonPropertyName("ensure_at_least_one_notification")]
        public bool EnsureAtLeastOneNotification { get; set; } = false;

        [JsonPropertyName("owner_responsible_type_ids")]
        public List<int> OwnerResponsibleTypeIds { get; set; } = [];

        /// <summary>
        /// Parses a config value into an email recipient selection.
        /// Supports new JSON values and legacy enum names.
        /// </summary>
        public static EmailRecipientSelection Parse(string? configValue, IEnumerable<int>? activeOwnerResponsibleTypeIds = null)
        {
            string rawValue = configValue?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return CreateDefault().Sanitize(activeOwnerResponsibleTypeIds);
            }

            EmailRecipientSelection parsedSelection = TryParseValue(rawValue, activeOwnerResponsibleTypeIds)
                ?? CreateDefault();
            return parsedSelection.Sanitize(activeOwnerResponsibleTypeIds, HasJsonOtherAddressList(rawValue));
        }

        /// <summary>
        /// Converts the selection into a persisted config value.
        /// </summary>
        public string ToConfigValue(IEnumerable<int>? activeOwnerResponsibleTypeIds = null)
        {
            EmailRecipientSelection sanitized = Sanitize(activeOwnerResponsibleTypeIds);
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
            return OtherAddresses || OwnerResponsibleTypeIds.Count > 0;
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

        private EmailRecipientSelection Sanitize(IEnumerable<int>? activeOwnerResponsibleTypeIds, bool clearEmptyOtherAddresses = true)
        {
            EmailRecipientSelection sanitized = new()
            {
                None = false,
                OtherAddresses = OtherAddresses,
                OtherAddressList = OtherAddressList
                    .Where(address => !string.IsNullOrWhiteSpace(address))
                    .Select(address => address.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList(),
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

            if (!sanitized.OtherAddresses)
            {
                sanitized.OtherAddressList = [];
            }
            else if (clearEmptyOtherAddresses && sanitized.OtherAddressList.Count == 0)
            {
                sanitized.OtherAddresses = false;
            }

            sanitized.None = !sanitized.OtherAddresses && sanitized.OwnerResponsibleTypeIds.Count == 0;
            if (sanitized.None)
            {
                sanitized.EnsureAtLeastOneNotification = false;
            }
            return sanitized;
        }

        private static EmailRecipientSelection? TryParseValue(string rawValue, IEnumerable<int>? activeOwnerResponsibleTypeIds)
        {
            if (LooksLikeJson(rawValue) && TryParseJson(rawValue, out EmailRecipientSelection jsonSelection))
            {
                return jsonSelection;
            }

            if (TryParseLegacy(rawValue, activeOwnerResponsibleTypeIds, out EmailRecipientSelection legacySelection))
            {
                return legacySelection;
            }

            return null;
        }

        private static bool TryParseJson(string rawValue, out EmailRecipientSelection selection)
        {
            selection = CreateDefault();
            try
            {
                EmailRecipientSelection? parsedSelection = JsonSerializer.Deserialize<EmailRecipientSelection>(rawValue);
                if (parsedSelection != null)
                {
                    parsedSelection.OwnerResponsibleTypeIds ??= [];
                    parsedSelection.OtherAddressList ??= [];
                    selection = parsedSelection;
                    return true;
                }
            }
            catch
            {
                Log.WriteWarning(
                    "Parse EmailRecipientSelection",
                    $"Could not parse recipient selection JSON value \"{rawValue}\". Falling back to legacy parsing.");
            }

            return false;
        }

        private static bool TryParseLegacy(
            string rawValue,
            IEnumerable<int>? activeOwnerResponsibleTypeIds,
            out EmailRecipientSelection selection)
        {
            List<int> activeTypeIds = ResolveActiveOwnerResponsibleTypeIds(activeOwnerResponsibleTypeIds);

            switch (rawValue)
            {
                case nameof(EmailRecipientOption.None):
                case "0":
                    selection = CreateNoneSelection();
                    return true;
                case nameof(EmailRecipientOption.OtherAddresses):
                    selection = CreateOtherAddressSelection();
                    return true;
                case nameof(EmailRecipientOption.OwnerMainResponsible):
                    selection = CreateOwnerResponsibleTypeSelection(GlobalConst.kOwnerResponsibleTypeMain);
                    return true;
                case nameof(EmailRecipientOption.OwnerGroupOnly):
                    selection = CreateOwnerResponsibleTypeSelection(GlobalConst.kOwnerResponsibleTypeSupporting);
                    return true;
                case nameof(EmailRecipientOption.AllOwnerResponsibles):
                    selection = CreateOwnerResponsibleTypeSelection(activeTypeIds);
                    return true;
                case nameof(EmailRecipientOption.FallbackToMainResponsibleIfOwnerGroupEmpty):
                    selection = CreateFallbackSelection();
                    return true;
                default:
                    selection = CreateDefault();
                    return false;
            }
        }

        private static bool LooksLikeJson(string rawValue)
        {
            return rawValue.StartsWith('{');
        }

        private static bool HasJsonOtherAddressList(string rawValue)
        {
            if (!LooksLikeJson(rawValue))
            {
                return false;
            }

            try
            {
                using JsonDocument document = JsonDocument.Parse(rawValue);
                return document.RootElement.ValueKind == JsonValueKind.Object
                    && document.RootElement.TryGetProperty("other_address_list", out _);
            }
            catch
            {
                return false;
            }
        }

        private static List<int> ResolveActiveOwnerResponsibleTypeIds(IEnumerable<int>? activeOwnerResponsibleTypeIds)
        {
            if (activeOwnerResponsibleTypeIds != null)
            {
                return activeOwnerResponsibleTypeIds.ToList();
            }

            return [GlobalConst.kOwnerResponsibleTypeMain, GlobalConst.kOwnerResponsibleTypeSupporting, GlobalConst.kOwnerResponsibleTypeOptionalEscalation];
        }

        private static EmailRecipientSelection CreateNoneSelection()
        {
            return CreateDefault();
        }

        private static EmailRecipientSelection CreateOtherAddressSelection()
        {
            return new EmailRecipientSelection
            {
                None = false,
                OtherAddresses = true
            };
        }

        private static EmailRecipientSelection CreateOwnerResponsibleTypeSelection(params int[] ownerResponsibleTypeIds)
        {
            return CreateOwnerResponsibleTypeSelection((IEnumerable<int>)ownerResponsibleTypeIds);
        }

        private static EmailRecipientSelection CreateOwnerResponsibleTypeSelection(IEnumerable<int> ownerResponsibleTypeIds)
        {
            return new EmailRecipientSelection
            {
                None = false,
                OwnerResponsibleTypeIds = ownerResponsibleTypeIds.ToList()
            };
        }

        private static EmailRecipientSelection CreateFallbackSelection()
        {
            return new EmailRecipientSelection
            {
                None = false,
                EnsureAtLeastOneNotification = true,
                OwnerResponsibleTypeIds = [GlobalConst.kOwnerResponsibleTypeSupporting, GlobalConst.kOwnerResponsibleTypeMain]
            };
        }

        private static EmailRecipientSelection CreateDefault()
        {
            return new EmailRecipientSelection();
        }
    }
}
