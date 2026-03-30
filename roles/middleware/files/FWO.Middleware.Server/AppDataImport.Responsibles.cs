using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Data;
using FWO.Logging;
using FWO.Services;
using Novell.Directory.Ldap;
using System.Text.Json;

namespace FWO.Middleware.Server
{
    public partial class AppDataImport
    {
        private List<OwnerResponsible> BuildOwnerResponsibles(ModellingImportAppData incomingApp)
        {
            List<OwnerResponsible> responsibles = [];
            HashSet<string> seenTypeDn = new(StringComparer.OrdinalIgnoreCase);

            if (incomingApp.Responsibles != null && incomingApp.Responsibles.Count > 0)
            {
                if (!AreResponsibleKeysNumeric(incomingApp.Responsibles))
                {
                    Log.WriteWarning(LogMessageTitle,
                        $"Skipping responsibles import for app \"{incomingApp.Name}\" ({incomingApp.ExtAppId}) because at least one responsible key is non-numeric.");
                }
                else
                {
                    Dictionary<string, int> responsibleTypeIdByKey = BuildResponsibleTypeIdByIncomingKey(incomingApp.Responsibles);
                    foreach ((string rawTypeKey, List<string> dns) in incomingApp.Responsibles)
                    {
                        string typeKey = string.IsNullOrWhiteSpace(rawTypeKey) ? "" : rawTypeKey.Trim();
                        if (!responsibleTypeIdByKey.TryGetValue(typeKey, out int responsibleTypeId))
                        {
                            Log.WriteWarning(LogMessageTitle,
                                $"Unknown owner responsible key \"{typeKey}\" (key \"{rawTypeKey}\") for app \"{incomingApp.Name}\" ({incomingApp.ExtAppId}). Skipping responsibles of this type.");
                            continue;
                        }

                        foreach (string dn in dns.Where(dn => !string.IsNullOrWhiteSpace(dn)))
                        {
                            TryAddResponsible(responsibles, seenTypeDn, dn, responsibleTypeId);
                        }
                    }
                }
            }

            if (IsResponsibleTypeActive(GlobalConst.kOwnerResponsibleTypeMain))
            {
                TryAddResponsible(responsibles, seenTypeDn, incomingApp.MainUser ?? "", GlobalConst.kOwnerResponsibleTypeMain);
            }

            return responsibles;
        }

        private static void TryAddResponsible(List<OwnerResponsible> responsibles, HashSet<string> seenTypeDn, string dn, int responsibleTypeId)
        {
            if (string.IsNullOrWhiteSpace(dn))
            {
                return;
            }

            string normalizedDn = dn.Trim();
            string dedupKey = $"{responsibleTypeId}|{normalizedDn}";
            if (seenTypeDn.Add(dedupKey))
            {
                responsibles.Add(new OwnerResponsible
                {
                    Dn = normalizedDn,
                    ResponsibleTypeId = responsibleTypeId
                });
            }
        }

        private static bool AreResponsibleKeysNumeric(Dictionary<string, List<string>> incomingResponsibles)
        {
            return incomingResponsibles.Keys
                .Select(rawKey => string.IsNullOrWhiteSpace(rawKey) ? "" : rawKey.Trim())
                .All(key => int.TryParse(key, out _));
        }

        private Dictionary<string, int> BuildResponsibleTypeIdByIncomingKey(Dictionary<string, List<string>> incomingResponsibles)
        {
            Dictionary<string, int> result = [];
            List<int> typeIdsBySortOrder = [.. ownerResponsibleTypeById.Values
                .Where(type => type.Active)
                .OrderBy(type => type.SortOrder)
                .ThenBy(type => type.Id)
                .Select(type => type.Id)];
            List<(string key, int keyNumber)> numericKeys = [.. incomingResponsibles.Keys
                .Select(rawKey => string.IsNullOrWhiteSpace(rawKey) ? "" : rawKey.Trim())
                .Select(key => (key, int.Parse(key)))
                .OrderBy(entry => entry.Item2)
                .ThenBy(entry => entry.Item1, StringComparer.Ordinal)];

            for (int i = 0; i < numericKeys.Count && i < typeIdsBySortOrder.Count; ++i)
            {
                result[numericKeys[i].key] = typeIdsBySortOrder[i];
            }

            return result;
        }

        private bool IsResponsibleTypeActive(int typeId)
        {
            return ownerResponsibleTypeById.TryGetValue(typeId, out OwnerResponsibleType? type) && type.Active;
        }

        private async Task UpdateOwnerResponsibles(int ownerId, List<OwnerResponsible> responsibles, List<OwnerResponsible> existingResponsibles)
        {
            (List<OwnerResponsible> responsiblesToInsert, List<OwnerResponsible> responsiblesToDelete) =
                CheckResponsibles(existingResponsibles, responsibles);

            if (responsiblesToDelete.Count > 0)
            {
                var deletionObjects = responsiblesToDelete.ConvertAll(responsible => new
                {
                    dn = new { _eq = responsible.Dn },
                    responsible_type = new { _eq = responsible.ResponsibleTypeId }
                });
                await apiConnection.SendQueryAsync<object>(OwnerQueries.deleteSpecificOwnerResponsibles, new { ownerId, objects = deletionObjects });
                await RemoveRolesFromResponsibles(responsiblesToDelete, rolesToSetByType);
            }

            if (responsiblesToInsert.Count == 0)
            {
                return;
            }

            var objects = responsiblesToInsert.ConvertAll(responsible => new
            {
                owner_id = ownerId,
                dn = responsible.Dn,
                responsible_type = responsible.ResponsibleTypeId
            });
            await apiConnection.SendQueryAsync<object>(OwnerQueries.newOwnerResponsibles, new { responsibles = objects });
        }

        private (List<OwnerResponsible> toInsert, List<OwnerResponsible> toDelete) CheckResponsibles(List<OwnerResponsible> existingResponsibles, List<OwnerResponsible> incomingResponsibles)
        {
            Dictionary<string, OwnerResponsible> existingByKey = BuildResponsiblesByKey(existingResponsibles);
            Dictionary<string, OwnerResponsible> incomingByKey = BuildResponsiblesByKey(incomingResponsibles);

            List<OwnerResponsible> responsiblesToInsert = incomingByKey
                .Where(entry => !existingByKey.ContainsKey(entry.Key))
                .Select(entry => new OwnerResponsible(entry.Value))
                .ToList();

            List<OwnerResponsible> responsiblesToDelete = existingByKey
                .Where(entry => !incomingByKey.ContainsKey(entry.Key) && globalConfig.OwnerDataImportSyncUsers)
                .Select(entry => new OwnerResponsible(entry.Value))
                .ToList();

            return (responsiblesToInsert, responsiblesToDelete);
        }

        private static Dictionary<string, OwnerResponsible> BuildResponsiblesByKey(List<OwnerResponsible> responsibles)
        {
            Dictionary<string, OwnerResponsible> responsiblesByKey = new(StringComparer.Ordinal);
            foreach (OwnerResponsible responsible in responsibles)
            {
                if (string.IsNullOrWhiteSpace(responsible.Dn))
                {
                    continue;
                }

                string key = BuildResponsibleKey(responsible);
                if (!responsiblesByKey.ContainsKey(key))
                {
                    responsiblesByKey[key] = responsible;
                }
            }

            return responsiblesByKey;
        }

        private static string BuildResponsibleKey(OwnerResponsible responsible)
        {
            return $"{responsible.ResponsibleTypeId}|{NormalizeDn(responsible.Dn)}";
        }

        private static string NormalizeDn(string dn)
        {
            return dn.Trim().ToUpperInvariant();
        }

        private async Task ApplyRolesToResponsibles(List<OwnerResponsible> responsibles, Dictionary<int, List<string>> rolesByType)
        {
            await ForEachResponsibleRoleAssignment(responsibles, rolesByType, UpdateRoles);
        }

        private async Task RemoveRolesFromResponsibles(List<OwnerResponsible> responsibles, Dictionary<int, List<string>> rolesByType)
        {
            await ForEachResponsibleRoleAssignment(responsibles, rolesByType, RemoveRoles);
        }

        private async Task ForEachResponsibleRoleAssignment(
            List<OwnerResponsible> responsibles,
            Dictionary<int, List<string>> rolesByType,
            Func<string, List<string>, Task> roleHandler)
        {
            foreach (OwnerResponsible responsible in responsibles)
            {
                if (!rolesByType.TryGetValue(responsible.ResponsibleTypeId, out List<string>? roles) || roles.Count == 0)
                {
                    continue;
                }

                bool allowModelling = ownerResponsibleTypeById.TryGetValue(responsible.ResponsibleTypeId, out OwnerResponsibleType? type) && type.AllowModelling;
                bool allowRecertification = ownerResponsibleTypeById.TryGetValue(responsible.ResponsibleTypeId, out type) && type.AllowRecertification;
                List<string> filteredRoles = OwnerResponsibleRoleHelper.FilterRoles(roles, allowModelling, allowRecertification);
                if (filteredRoles.Count == 0)
                {
                    continue;
                }

                await roleHandler(responsible.Dn, filteredRoles);
            }
        }

        private string GetRoleDn(string role)
        {
            return $"cn={role},{internalLdap.RoleSearchPath}";
        }

        private List<string> GetRolesForType(int typeId)
        {
            return rolesToSetByType.TryGetValue(typeId, out List<string>? roles) ? roles : [];
        }

        private static Dictionary<int, List<string>> ParseRolesWithImport(string rolesJson)
        {
            Dictionary<int, List<string>> rolesByType = [];
            if (string.IsNullOrWhiteSpace(rolesJson))
            {
                return rolesByType;
            }

            string trimmed = rolesJson.TrimStart();
            if (trimmed.StartsWith("["))
            {
                List<string> roles = JsonSerializer.Deserialize<List<string>>(rolesJson) ?? [];
                rolesByType[GlobalConst.kOwnerResponsibleTypeSupporting] = roles;
                return rolesByType;
            }

            Dictionary<string, List<string>>? parsed = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(rolesJson);
            if (parsed != null)
            {
                foreach (var entry in parsed)
                {
                    if (int.TryParse(entry.Key, out int typeId))
                    {
                        rolesByType[typeId] = entry.Value;
                    }
                }
            }

            return rolesByType;
        }

        /// <summary>
        /// For every imported responsible DN (user or group), ensure all referenced users exist in uiuser.
        /// This is necessary to resolve email addresses for users who have never logged in.
        /// </summary>
        private async Task AddAllResponsiblesToUiUser(IEnumerable<OwnerResponsible> responsibles)
        {
            HashSet<string> handledUserDns = new(StringComparer.OrdinalIgnoreCase);
            HashSet<string> handledGroupDnsByLdap = new(StringComparer.OrdinalIgnoreCase);

            foreach (string responsibleDn in responsibles
                .Where(responsible => !string.IsNullOrWhiteSpace(responsible.Dn))
                .Select(responsible => responsible.Dn.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                await AddResponsibleDnToUiUser(responsibleDn, handledUserDns, handledGroupDnsByLdap);
            }
        }

        private async Task AddResponsibleDnToUiUser(string responsibleDn, HashSet<string> handledUserDns, HashSet<string> handledGroupDnsByLdap)
        {
            string normalizedResponsibleDn = responsibleDn.Trim();
            if (await TryResolveAndUpsertImportedUiUser(normalizedResponsibleDn, handledUserDns))
            {
                return;
            }

            foreach (Ldap ldap in connectedLdaps)
            {
                string groupKey = $"{ldap.Id}|{normalizedResponsibleDn}";
                if (!handledGroupDnsByLdap.Add(groupKey))
                {
                    continue;
                }

                foreach (string memberDn in await ResolveImportedGroupMembers(ldap, normalizedResponsibleDn))
                {
                    if (!string.IsNullOrWhiteSpace(memberDn))
                    {
                        await AddResponsibleDnToUiUser(memberDn.Trim(), handledUserDns, handledGroupDnsByLdap);
                    }
                }
            }
        }

        private async Task<bool> TryResolveAndUpsertImportedUiUser(string responsibleDn, HashSet<string> handledUserDns)
        {
            UiUser? uiUser = await ResolveImportedUiUser(responsibleDn);
            if (uiUser == null || string.IsNullOrWhiteSpace(uiUser.Dn))
            {
                return false;
            }

            if (!handledUserDns.Add(uiUser.Dn))
            {
                return true;
            }

            await UiUserHandler.UpsertUiUser(apiConnection, uiUser, false);
            if (uiUser.DbId <= 0)
            {
                Log.WriteWarning(LogMessageTitle, $"Resolved imported user \"{uiUser.Dn}\" could not be written to uiuser.");
            }

            return true;
        }

        private async Task<UiUser?> ConvertLdapToUiUser(string userDn)
        {
            bool inputLooksLikeDn = LooksLikeDistinguishedName(userDn);
            foreach (Ldap ldap in connectedLdaps)
            {
                if (!inputLooksLikeDn
                    && (string.IsNullOrEmpty(ldap.UserSearchPath)
                        || !userDn.Contains(ldap.UserSearchPath, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                LdapEntry? ldapUser = await ldap.GetUserDetailsFromLdap(userDn);
                if (ldapUser != null && !Ldap.IsGroupEntry(ldapUser))
                {
                    return new()
                    {
                        LdapConnection = new UiLdapConnection() { Id = ldap.Id },
                        Dn = ldapUser.Dn,
                        Name = Ldap.GetName(ldapUser),
                        Firstname = Ldap.GetFirstName(ldapUser),
                        Lastname = Ldap.GetLastName(ldapUser),
                        Email = Ldap.GetEmail(ldapUser),
                        Tenant = await DeriveTenantFromLdap(ldap, ldapUser)
                    };
                }
            }

            return null;
        }

        /// <summary>
        /// Resolves an imported responsible DN to a UI user if it references a user entry.
        /// </summary>
        /// <param name="responsibleDn">Imported responsible distinguished name.</param>
        /// <returns>UI user when the DN is a user entry; otherwise null.</returns>
        protected virtual async Task<UiUser?> ResolveImportedUiUser(string responsibleDn)
        {
            return await ConvertLdapToUiUser(responsibleDn);
        }

        /// <summary>
        /// Resolves members of an imported group DN, including groups from non-standard LDAP paths.
        /// </summary>
        /// <param name="ldap">LDAP connection to query.</param>
        /// <param name="groupDn">Imported group distinguished name.</param>
        /// <returns>List of member user or group DNs.</returns>
        protected virtual async Task<List<string>> ResolveImportedGroupMembers(Ldap ldap, string groupDn)
        {
            return await ldap.GetGroupMembers(groupDn);
        }

        private async Task<Tenant> DeriveTenantFromLdap(Ldap ldap, LdapEntry ldapUser)
        {
            Tenant tenant = new()
            {
                Id = GlobalConst.kTenant0Id
            };

            string tenantName = "";
            if (!string.IsNullOrEmpty(ldap.GlobalTenantName) || ldap.TenantLevel > 0)
            {
                if (ldap.TenantLevel > 0)
                {
                    tenantName = ldap.GetTenantName(ldapUser);
                }
                else if (!string.IsNullOrEmpty(ldap.GlobalTenantName))
                {
                    tenantName = ldap.GlobalTenantName ?? "";
                }

                var variables = new { tenant_name = tenantName };
                Tenant[] tenants = await apiConnection.SendQueryAsync<Tenant[]>(AuthQueries.getTenantId, variables, "getTenantId");
                if (tenants.Length == 1)
                {
                    tenant.Id = tenants[0].Id;
                }
            }

            return tenant;
        }

        private async Task UpdateRoles(string dn, List<string> rolesToApply)
        {
            List<string> roles = await internalLdap.GetRoles([dn]);
            foreach (var role in rolesToApply)
            {
                if (!roles.Contains(role, StringComparer.OrdinalIgnoreCase))
                {
                    await internalLdap.AddUserToEntry(dn, GetRoleDn(role));
                }
            }
        }

        private async Task RemoveRoles(string dn, List<string> rolesToRemove)
        {
            foreach (string role in rolesToRemove)
            {
                await RemoveRoleFromDn(dn, role);
            }
        }

        /// <summary>
        /// Removes a role assignment from a user DN.
        /// </summary>
        /// <param name="dn">User distinguished name.</param>
        /// <param name="role">Role name.</param>
        protected virtual async Task RemoveRoleFromDn(string dn, string role)
        {
            await internalLdap.RemoveUserFromEntry(dn, GetRoleDn(role));
        }
    }
}
