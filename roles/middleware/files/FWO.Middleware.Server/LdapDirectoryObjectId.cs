using Novell.Directory.Ldap;

namespace FWO.Middleware.Server
{
    /// <summary>
    /// Reads the immutable id of a directory entry. Unlike the dn, which can be handed to another person when an
    /// account is deleted and a later one gets the same name, this id stays with the entry for its whole life and is
    /// never reused, so it keeps a local user bound to one person (GHSA-695m-f6mv-9g72).
    /// </summary>
    public static class LdapDirectoryObjectId
    {
        /// <summary>Active Directory (binary GUID).</summary>
        public const string kObjectGuid = "objectGUID";
        /// <summary>OpenLDAP, including the internal LDAP, and newer 389 Directory Server versions.</summary>
        public const string kEntryUuid = "entryUUID";
        /// <summary>389 Directory Server and Red Hat Directory Server.</summary>
        public const string kNsUniqueId = "nsUniqueId";
        /// <summary>FreeIPA.</summary>
        public const string kIpaUniqueId = "ipaUniqueID";
        /// <summary>eDirectory (binary GUID).</summary>
        public const string kGuid = "GUID";

        /// <summary>
        /// The attributes holding the immutable id, in order of preference. Several of them are operational attributes,
        /// which a directory only returns when they are requested by name.
        /// </summary>
        public static readonly List<string> AttributeNames = [kObjectGuid, kEntryUuid, kNsUniqueId, kIpaUniqueId, kGuid];

        private static readonly List<string> kBinaryAttributeNames = [kObjectGuid, kGuid];
        private const int kGuidLength = 16;

        /// <summary>
        /// Builds the attribute list for reading a user entry: all user attributes, the given extra attributes and the
        /// attributes holding the immutable id.
        /// </summary>
        /// <param name="extraAttributes">Further operational attributes to read, e.g. memberOf.</param>
        /// <returns>The attribute list to pass to a read or search.</returns>
        public static string[] BuildUserAttributeList(params string[] extraAttributes)
        {
            List<string> attributes = ["*"];
            attributes.AddRange(extraAttributes);
            attributes.AddRange(AttributeNames);
            return attributes.ToArray();
        }

        /// <summary>
        /// Gets the immutable id of an entry from the first attribute in <see cref="AttributeNames"/> it carries.
        /// </summary>
        /// <param name="entry">Directory entry, read with <see cref="BuildUserAttributeList"/>.</param>
        /// <returns>The id prefixed with its attribute name, e.g. "entryUUID:2b1f...", or null if the entry has none.</returns>
        public static string? GetFrom(LdapEntry entry)
        {
            List<LdapAttribute> attributes = entry.GetAttributeSet().Values.ToList();
            foreach (string attributeName in AttributeNames)
            {
                LdapAttribute? attribute = attributes.FirstOrDefault(candidate => string.Equals(candidate.Name, attributeName, StringComparison.OrdinalIgnoreCase));
                string? value = attribute == null ? null : ReadValue(attribute, attributeName);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return $"{attributeName}:{value}";
                }
            }
            return null;
        }

        private static string? ReadValue(LdapAttribute attribute, string attributeName)
        {
            if (!kBinaryAttributeNames.Contains(attributeName))
            {
                return attribute.StringValue?.Trim().ToLowerInvariant();
            }
            byte[]? bytes = attribute.ByteValue;
            if (bytes == null || bytes.Length == 0)
            {
                return null;
            }
            return bytes.Length == kGuidLength ? new Guid(bytes).ToString() : Convert.ToHexString(bytes).ToLowerInvariant();
        }
    }
}
