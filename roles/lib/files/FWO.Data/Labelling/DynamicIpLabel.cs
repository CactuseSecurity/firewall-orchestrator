using System.Collections.Frozen;
using FWO.Data.Modelling;

namespace FWO.Data.Labelling
{
    /// <summary>
    /// Label with IP criteria that stands for a group on firewalls that segment by labels (Guardicore). Modelled app roles,
    /// app zones and network areas are written as labels of the keys AppRole, AppZone and NetworkArea with a value in the
    /// configured format (see <see cref="LabelValueFormat"/>, by default the id string); their import shows them as groups
    /// named "key: value".
    /// </summary>
    /// <param name="Key">Label key, e.g. AppRole.</param>
    /// <param name="Value">Label value, e.g. AR4711-001.</param>
    public sealed record DynamicIpLabel(string Key, string Value)
    {
        /// <summary>Separator of key and value in the name of a label, as the import names label groups.</summary>
        public const string kKeyValueSeparator = ": ";

        /// <summary>Label key of app roles.</summary>
        public const string kAppRoleKey = "AppRole";

        /// <summary>Label key of app zones.</summary>
        public const string kAppZoneKey = "AppZone";

        /// <summary>Label key of network areas.</summary>
        public const string kNetworkAreaKey = "NetworkArea";

        private static readonly FrozenDictionary<ModellingTypes.ModObjectType, string> KeyByObjectType =
            new Dictionary<ModellingTypes.ModObjectType, string>
            {
                { ModellingTypes.ModObjectType.AppRole, kAppRoleKey },
                { ModellingTypes.ModObjectType.AppZone, kAppZoneKey },
                { ModellingTypes.ModObjectType.NetworkArea, kNetworkAreaKey }
            }.ToFrozenDictionary();

        /// <summary>Name of the label as imported: "key: value".</summary>
        public string Name => Key + kKeyValueSeparator + Value;

        /// <summary>Modelling object type of the label key; null for keys of other labels.</summary>
        public ModellingTypes.ModObjectType? ObjectType =>
            KeyByObjectType.Where(entry => entry.Value == Key).Select(entry => (ModellingTypes.ModObjectType?)entry.Key).FirstOrDefault();

        /// <summary>Whether modelled objects of the type are written as dynamic IP labels.</summary>
        public static bool IsLabelType(ModellingTypes.ModObjectType objectType) => KeyByObjectType.ContainsKey(objectType);

        /// <summary>Returns the label of a modelled app role, app zone or network area.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown for other object types.</exception>
        public static DynamicIpLabel ForModelledGroup(ModellingTypes.ModObjectType objectType, string idString)
        {
            return KeyByObjectType.TryGetValue(objectType, out string? key)
                ? new DynamicIpLabel(key, idString)
                : throw new ArgumentOutOfRangeException(nameof(objectType), objectType, "Only app roles, app zones and network areas are dynamic IP labels.");
        }

        /// <summary>Returns the label of a modelled app role, app zone or network area with the value in the configured format.</summary>
        /// <param name="objectType">App role, app zone or network area.</param>
        /// <param name="idString">Id string of the group.</param>
        /// <param name="name">Name of the group.</param>
        /// <param name="valueFormat">Configured format of the label values.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown for other object types.</exception>
        public static DynamicIpLabel ForModelledGroup(ModellingTypes.ModObjectType objectType, string idString, string? name, LabelValueFormat valueFormat)
        {
            ArgumentNullException.ThrowIfNull(valueFormat);
            DynamicIpLabel label = ForModelledGroup(objectType, idString);
            return label with { Value = valueFormat.Value(name, idString) };
        }

        /// <summary>Reads a label name "key: value"; null if it has no separator or an empty key or value.</summary>
        /// <param name="name">Label name.</param>
        public static DynamicIpLabel? FromName(string? name)
        {
            string trimmed = name?.Trim() ?? "";
            return trimmed.Contains(kKeyValueSeparator, StringComparison.Ordinal) ? SplitKeyAndValue(trimmed) : null;
        }

        /// <summary>
        /// True if a label name stands for the same modelled group as this label: same key, and its value stands for the id
        /// of the group (see <see cref="LabelValueFormat.Matches"/>), even if the name of the group or the format changed.
        /// </summary>
        /// <param name="labelName">Name of an existing label "key: value".</param>
        /// <param name="idString">Id string of the modelled group of this label.</param>
        /// <param name="valueFormat">Configured format of the label values.</param>
        public bool IsSameGroup(string? labelName, string idString, LabelValueFormat valueFormat)
        {
            ArgumentNullException.ThrowIfNull(valueFormat);
            DynamicIpLabel? other = FromName(labelName);
            return other != null && string.Equals(other.Key, Key, StringComparison.Ordinal) && valueFormat.Matches(other.Value, idString);
        }

        /// <summary>
        /// Returns the label a group of a task stands for: a name "key: value" gives key and value, else the object type
        /// the request task names for its group (app role, app zone or network area) gives the key and the name is the
        /// value. The type is not guessed from the name, as a prefix of the naming convention may be empty or also start
        /// the names of other groups.
        /// </summary>
        /// <param name="groupName">Name of the group of the task.</param>
        /// <param name="objectType">Object type of the group as the request task names it (<c>GrpType</c>), null if it names none.</param>
        /// <returns>Null if the name is not "key: value" and the type is none of app role, app zone and network area.</returns>
        public static DynamicIpLabel? FromGroupName(string? groupName, ModellingTypes.ModObjectType? objectType)
        {
            string name = groupName?.Trim() ?? "";
            if (name.Contains(kKeyValueSeparator, StringComparison.Ordinal))
            {
                return SplitKeyAndValue(name);
            }
            return name.Length > 0 && objectType != null && IsLabelType(objectType.Value) ? ForModelledGroup(objectType.Value, name) : null;
        }

        /// <summary>
        /// Returns the modelling object type of a group: by the label key of a name "key: value", else the type the request
        /// task names for its group.
        /// </summary>
        /// <param name="groupName">Name of the group of the task.</param>
        /// <param name="objectType">Object type of the group as the request task names it (<c>GrpType</c>), null if it names none.</param>
        /// <returns>Null if the type is neither in the name nor named by the task.</returns>
        public static ModellingTypes.ModObjectType? GroupObjectType(string? groupName, ModellingTypes.ModObjectType? objectType)
        {
            string name = groupName?.Trim() ?? "";
            return name.Contains(kKeyValueSeparator, StringComparison.Ordinal)
                ? SplitKeyAndValue(name)?.ObjectType
                : objectType;
        }

        /// <summary>
        /// Reads the object type a request task names for its group (<c>GrpType</c>), e.g. "AppRole".
        /// </summary>
        /// <returns>Null for an empty or unknown type.</returns>
        public static ModellingTypes.ModObjectType? ParseObjectType(string? objectType)
        {
            string name = objectType?.Trim() ?? "";
            bool isNumber = int.TryParse(name, out _);
            if (name.Length == 0 || isNumber)
            {
                return null;
            }
            return Enum.TryParse(name, false, out ModellingTypes.ModObjectType parsed) && Enum.IsDefined(parsed) ? parsed : null;
        }

        /// <summary>Splits a name "key: value"; null if key or value is empty.</summary>
        private static DynamicIpLabel? SplitKeyAndValue(string name)
        {
            int separatorIndex = name.IndexOf(kKeyValueSeparator, StringComparison.Ordinal);
            string key = name[..separatorIndex].Trim();
            string value = name[(separatorIndex + kKeyValueSeparator.Length)..].Trim();
            return key.Length > 0 && value.Length > 0 ? new DynamicIpLabel(key, value) : null;
        }
    }
}
