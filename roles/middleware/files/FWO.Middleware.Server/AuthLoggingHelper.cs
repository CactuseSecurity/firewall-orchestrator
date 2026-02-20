using FWO.Data;

namespace FWO.Middleware.Server
{
    /// <summary>
    /// Helper methods for consistent authentication logging.
    /// </summary>
    public static class AuthLoggingHelper
    {
        private const int kMaxGroupDnsToLog = 50;

        /// <summary>
        /// Formats selected LDAP connection information for login logs.
        /// </summary>
        /// <param name="ldap">LDAP connection object.</param>
        /// <returns>Single-line LDAP descriptor for logs.</returns>
        public static string FormatSelectedLdap(LdapConnectionBase? ldap)
        {
            if (ldap == null)
            {
                return "ldap=<null>";
            }

            string tenantInfo = ldap.TenantId.HasValue ? ldap.TenantId.Value.ToString() : "<dynamic>";
            return $"id={ldap.Id}, host={ldap.Host()}, type={ldap.Type}, tenant={tenantInfo}";
        }

        /// <summary>
        /// Formats resolved group DNs for login logs.
        /// </summary>
        /// <param name="groups">Resolved group DNs.</param>
        /// <returns>Single-line group summary for logs.</returns>
        public static string FormatResolvedGroups(IEnumerable<string>? groups)
        {
            if (groups == null)
            {
                return "count=0, groups=[]";
            }

            List<string> normalizedGroups = groups
                .Where(groupDn => !string.IsNullOrWhiteSpace(groupDn))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(groupDn => groupDn, StringComparer.OrdinalIgnoreCase)
                .ToList();
            int totalGroups = normalizedGroups.Count;
            if (totalGroups > kMaxGroupDnsToLog)
            {
                normalizedGroups = normalizedGroups.Take(kMaxGroupDnsToLog).ToList();
            }

            return $"count={totalGroups}, groups=[{string.Join("; ", normalizedGroups)}]";
        }
    }
}
