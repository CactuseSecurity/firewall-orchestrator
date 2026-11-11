using System.Text;

namespace FWO.Data
{
    /// <summary>
    /// Assigns every distinguished name to the one LDAP connection whose subtree contains it.
    /// A dn is unique only inside its directory, but roles, groups, owner responsibles, workflow
    /// visibility groups and workflow assignments refer to subjects by their bare dn. Each active
    /// connection therefore owns the subtrees below its search and write paths, and a dn is accepted
    /// only from the connection that owns it (GHSA-695m-f6mv-9g72). The most specific path wins; a dn
    /// that two active connections own at the same depth is ambiguous and owned by none of them.
    /// </summary>
    public static class LdapDnNamespace
    {
        /// <summary>
        /// Returns the paths spanning the namespace of a connection: its user, role and group search paths
        /// and its group write path, each split into normalized relative distinguished names.
        /// </summary>
        /// <param name="ldap">Connection to read the paths from.</param>
        /// <returns>The non-empty paths of the connection, without duplicates.</returns>
        public static List<List<string>> GetNamespaceRoots(LdapConnectionBase ldap)
        {
            List<List<string>> roots = [];
            foreach (string? path in GetPaths(ldap))
            {
                List<string> rdns = SplitDn(path);
                if (rdns.Count > 0 && !roots.Any(root => root.SequenceEqual(rdns)))
                {
                    roots.Add(rdns);
                }
            }
            return roots;
        }

        /// <summary>
        /// Determines how specifically a connection claims a dn.
        /// </summary>
        /// <param name="ldap">Connection to check.</param>
        /// <param name="dn">Distinguished name to locate.</param>
        /// <returns>The number of relative distinguished names of the longest path of the connection containing the dn, or 0 if none contains it.</returns>
        public static int GetClaimDepth(LdapConnectionBase ldap, string? dn)
        {
            List<string> rdns = SplitDn(dn);
            return GetNamespaceRoots(ldap)
                .Where(root => IsSuffix(root, rdns))
                .Select(root => root.Count)
                .DefaultIfEmpty(0)
                .Max();
        }

        /// <summary>
        /// Finds the active connections that claim a dn most specifically.
        /// </summary>
        /// <param name="ldaps">All connections; inactive ones are ignored.</param>
        /// <param name="dn">Distinguished name to locate.</param>
        /// <returns>The ids of the connections claiming the dn at the highest depth: one id for an owned dn, several for an ambiguous one, none for a foreign one.</returns>
        public static List<int> GetClaimingLdapIds(IEnumerable<LdapConnectionBase> ldaps, string? dn)
        {
            List<(int Id, int Depth)> claims = ldaps
                .Where(ldap => ldap.Active)
                .Select(ldap => (ldap.Id, Depth: GetClaimDepth(ldap, dn)))
                .Where(claim => claim.Depth > 0)
                .ToList();
            if (claims.Count == 0)
            {
                return [];
            }
            int maxDepth = claims.Max(claim => claim.Depth);
            return claims.Where(claim => claim.Depth == maxDepth).Select(claim => claim.Id).Distinct().ToList();
        }

        /// <summary>
        /// Checks whether a dn belongs to the given connection and to no other active connection.
        /// </summary>
        /// <param name="ldaps">All connections; inactive ones are ignored.</param>
        /// <param name="ldapId">Id of the connection the dn was read from.</param>
        /// <param name="dn">Distinguished name to check.</param>
        /// <returns>True if exactly the given connection owns the dn.</returns>
        public static bool IsOwnedBy(IEnumerable<LdapConnectionBase> ldaps, int ldapId, string? dn)
        {
            List<int> claimingIds = GetClaimingLdapIds(ldaps, dn);
            return claimingIds.Count == 1 && claimingIds[0] == ldapId;
        }

        /// <summary>
        /// Finds the active connections that share a search or write path with the given connection. A dn below such a
        /// path belongs to neither connection. Nested paths are no overlap: the more specific path owns its subtree,
        /// e.g. a connection to a child domain within a connection to the forest root.
        /// </summary>
        /// <param name="candidate">Connection to check, e.g. before it is saved.</param>
        /// <param name="ldaps">The other connections; inactive ones and the candidate itself are ignored.</param>
        /// <returns>The overlapping connections; empty if the candidate is inactive or nothing overlaps.</returns>
        public static List<T> GetOverlappingLdaps<T>(LdapConnectionBase candidate, IEnumerable<T> ldaps) where T : LdapConnectionBase
        {
            if (!candidate.Active)
            {
                return [];
            }
            List<List<string>> candidateRoots = GetNamespaceRoots(candidate);
            return ldaps
                .Where(ldap => ldap.Active && (candidate.Id <= 0 || ldap.Id != candidate.Id))
                .Where(ldap => GetNamespaceRoots(ldap).Any(root => candidateRoots.Any(candidateRoot => root.SequenceEqual(candidateRoot))))
                .ToList();
        }

        /// <summary>
        /// Splits a dn into its relative distinguished names, leaf first, each normalized for comparison.
        /// Escaped separators stay part of their value.
        /// </summary>
        /// <param name="dn">Distinguished name to split.</param>
        /// <returns>The normalized relative distinguished names; empty for an empty dn.</returns>
        public static List<string> SplitDn(string? dn)
        {
            List<string> rdns = [];
            if (string.IsNullOrWhiteSpace(dn))
            {
                return rdns;
            }

            StringBuilder currentRdn = new();
            bool escaped = false;
            foreach (char character in dn)
            {
                if (escaped || character == '\\')
                {
                    currentRdn.Append(character);
                    escaped = !escaped;
                }
                else if (character == ',' || character == ';')
                {
                    AddRdn(rdns, currentRdn.ToString());
                    currentRdn.Clear();
                }
                else
                {
                    currentRdn.Append(character);
                }
            }
            AddRdn(rdns, currentRdn.ToString());
            return rdns;
        }

        private static IEnumerable<string?> GetPaths(LdapConnectionBase ldap)
        {
            yield return ldap.UserSearchPath;
            yield return ldap.RoleSearchPath;
            yield return ldap.GroupSearchPath;
            yield return ldap.GroupWritePath;
        }

        private static void AddRdn(List<string> rdns, string rdn)
        {
            int separatorIndex = rdn.IndexOf('=');
            string normalizedRdn = separatorIndex < 0
                ? rdn.Trim()
                : rdn[..separatorIndex].Trim() + "=" + rdn[(separatorIndex + 1)..].Trim();
            rdns.Add(DistName.NormalizeDnForComparison(normalizedRdn));
        }

        private static bool IsSuffix(List<string> root, List<string> rdns)
        {
            if (root.Count == 0 || root.Count > rdns.Count)
            {
                return false;
            }
            int offset = rdns.Count - root.Count;
            for (int index = 0; index < root.Count; index++)
            {
                if (!string.Equals(root[index], rdns[offset + index], StringComparison.Ordinal))
                {
                    return false;
                }
            }
            return true;
        }
    }
}
