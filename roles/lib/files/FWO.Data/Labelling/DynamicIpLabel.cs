using FWO.Data.Modelling;

namespace FWO.Data.Labelling
{
    /// <summary>
    /// Label with IP criteria that stands for a group on firewalls that segment by labels (Guardicore). Modelled app roles,
    /// app zones and network areas are written as labels of the keys AppRole, AppZone and NetworkArea with their id string
    /// as value; their import shows them as groups named "key: value".
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

        private static readonly Dictionary<ModellingTypes.ModObjectType, string> KeyByObjectType = new()
        {
            { ModellingTypes.ModObjectType.AppRole, kAppRoleKey },
            { ModellingTypes.ModObjectType.AppZone, kAppZoneKey },
            { ModellingTypes.ModObjectType.NetworkArea, kNetworkAreaKey }
        };

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

        /// <summary>
        /// Returns the label a group of a task stands for: a name "key: value" gives key and value, else the naming
        /// convention tells app roles, app zones and network areas by their prefix and the name is the value.
        /// </summary>
        /// <returns>Null if the name is neither "key: value" nor an id string of the naming convention.</returns>
        public static DynamicIpLabel? FromGroupName(string? groupName, ModellingNamingConvention namingConvention)
        {
            string name = groupName?.Trim() ?? "";
            if (name.Contains(kKeyValueSeparator, StringComparison.Ordinal))
            {
                return SplitKeyAndValue(name);
            }
            ModellingTypes.ModObjectType? objectType = PrefixObjectType(name, namingConvention);
            return objectType == null ? null : ForModelledGroup(objectType.Value, name);
        }

        /// <summary>
        /// Returns the modelling object type of a group name: by the label key of a name "key: value", else by the prefix
        /// the naming convention gives app zones, app roles and network areas (the longest matching prefix wins).
        /// </summary>
        /// <returns>Null if the type cannot be told from the name.</returns>
        public static ModellingTypes.ModObjectType? GroupObjectType(string? groupName, ModellingNamingConvention namingConvention)
        {
            string name = groupName?.Trim() ?? "";
            return name.Contains(kKeyValueSeparator, StringComparison.Ordinal)
                ? SplitKeyAndValue(name)?.ObjectType
                : PrefixObjectType(name, namingConvention);
        }

        /// <summary>Splits a name "key: value"; null if key or value is empty.</summary>
        private static DynamicIpLabel? SplitKeyAndValue(string name)
        {
            int separatorIndex = name.IndexOf(kKeyValueSeparator, StringComparison.Ordinal);
            string key = name[..separatorIndex].Trim();
            string value = name[(separatorIndex + kKeyValueSeparator.Length)..].Trim();
            return key.Length > 0 && value.Length > 0 ? new DynamicIpLabel(key, value) : null;
        }

        /// <summary>The object type the naming convention gives a name by its prefix; the longest matching prefix wins.</summary>
        private static ModellingTypes.ModObjectType? PrefixObjectType(string name, ModellingNamingConvention namingConvention)
        {
            List<(string Prefix, ModellingTypes.ModObjectType Type)> prefixes =
            [
                (namingConvention.AppZone, ModellingTypes.ModObjectType.AppZone),
                (namingConvention.AppRolePattern, ModellingTypes.ModObjectType.AppRole),
                (namingConvention.NetworkAreaPattern, ModellingTypes.ModObjectType.NetworkArea)
            ];
            return prefixes
                .Where(entry => !string.IsNullOrEmpty(entry.Prefix) && name.Length > entry.Prefix.Length && name.StartsWith(entry.Prefix, StringComparison.Ordinal))
                .OrderByDescending(entry => entry.Prefix.Length)
                .Select(entry => (ModellingTypes.ModObjectType?)entry.Type)
                .FirstOrDefault();
        }
    }
}
