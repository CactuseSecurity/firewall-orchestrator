using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Logging;
using FWO.Recert;
using FWO.Services;
using FWO.Services.Modelling;
using Novell.Directory.Ldap;
using System.Data;
using System.Text.Json;

namespace FWO.Middleware.Server
{
    /// <summary>
    /// Class handling the App Data Import
    /// </summary>
    public class AppDataImport : DataImportBase
    {
        private List<ModellingImportAppData> importedApps = [];
        private List<FwoOwner> existingApps = [];
        private List<ModellingAppServer> existingAppServers = [];

        private Ldap internalLdap = new();

        private List<Ldap> connectedLdaps = [];
        private Dictionary<int, List<string>> rolesToSetByType = [];
        private Dictionary<int, OwnerResponsibleType> ownerResponsibleTypeById = [];
        private Dictionary<string, int> ownerResponsibleTypeIdByName = new(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, int> ownerLifeCycleStateIdsByName = new(StringComparer.OrdinalIgnoreCase);
        private ModellingNamingConvention NamingConvention = new();
        private UserConfig userConfig = new();
        private const string LogMessageTitle = "Import App Data";
        private const string LevelFile = "Import File";
        private const string LevelApp = "App";
        private const string LevelAppServer = "App Server";

        /// <summary>
        /// Constructor for App Data Import
        /// </summary>
        public AppDataImport(ApiConnection apiConnection, GlobalConfig globalConfig) : base(apiConnection, globalConfig)
        { }

        /// <summary>
        /// Run the App Data Import
        /// </summary>
        public async Task<List<string>> Run()
        {
            NamingConvention = JsonSerializer.Deserialize<ModellingNamingConvention>(globalConfig.ModNamingConvention) ?? new();
            List<string> importfilePathAndNames = JsonSerializer.Deserialize<List<string>>(globalConfig.ImportAppDataPath) ?? throw new JsonException("Config Data could not be deserialized.");
            userConfig = new(globalConfig);
            userConfig.User.Name = Roles.MiddlewareServer;
            userConfig.AutoReplaceAppServer = globalConfig.AutoReplaceAppServer;
            await InitLdap();
            await InitResponsibleTypes();
            await InitOwnerLifeCycleStates();
            List<string> failedImports = [];
            var ownerChangeTracker = new OwnerChangeImportTracker(apiConnection);

            foreach (var importfilePathAndName in importfilePathAndNames)
            {
                if (!RunImportScript(importfilePathAndName + ".py", globalConfig.ImportAppDataScriptArgs))
                {
                    Log.WriteInfo(LogMessageTitle, $"Script {importfilePathAndName}.py failed but trying to import from existing file.");
                }
                await ImportSingleSource(importfilePathAndName + ".json", failedImports, ownerChangeTracker);
            }

            await ownerChangeTracker.CompleteImport(failedImports.Count == 0);
            return failedImports;
        }

        private async Task InitLdap()
        {
            connectedLdaps = await apiConnection.SendQueryAsync<List<Ldap>>(AuthQueries.getLdapConnections);
            internalLdap = connectedLdaps.FirstOrDefault(x => x.IsInternal() && x.HasGroupHandling()) ?? throw new KeyNotFoundException("No internal Ldap with group handling found.");
            rolesToSetByType = ParseRolesWithImport(globalConfig.RolesWithAppDataImport);
        }

        private async Task InitResponsibleTypes()
        {
            List<OwnerResponsibleType> responsibleTypes = (await apiConnection.SendQueryAsync<List<OwnerResponsibleType>>(OwnerQueries.getOwnerResponsibleTypes))
                .Where(type => type.Active).ToList();
            ownerResponsibleTypeById = responsibleTypes.ToDictionary(type => type.Id, type => type);
            ownerResponsibleTypeIdByName = new(StringComparer.OrdinalIgnoreCase);
            foreach (OwnerResponsibleType type in responsibleTypes)
            {
                if (!string.IsNullOrWhiteSpace(type.Name))
                {
                    ownerResponsibleTypeIdByName[type.Name.Trim()] = type.Id;
                }
            }
        }

        private async Task InitOwnerLifeCycleStates()
        {
            List<OwnerLifeCycleState> lifeCycleStates = await apiConnection.SendQueryAsync<List<OwnerLifeCycleState>>(OwnerQueries.getOwnerLifeCycleStates);
            ownerLifeCycleStateIdsByName = new(StringComparer.OrdinalIgnoreCase);
            foreach (OwnerLifeCycleState state in lifeCycleStates)
            {
                if (!string.IsNullOrWhiteSpace(state.Name))
                {
                    ownerLifeCycleStateIdsByName[state.Name] = state.Id;
                }
            }
        }

        private async Task ImportSingleSource(string importfileName, List<string> failedImports, OwnerChangeImportTracker ownerChangeTracker)
        {
            try
            {
                ReadFile(importfileName);
                ModellingImportOwnerData? importedOwnerData = JsonSerializer.Deserialize<ModellingImportOwnerData>(importFile) ?? throw new JsonException("File could not be parsed.");
                if (importedOwnerData != null && importedOwnerData.Owners != null)
                {
                    importedApps = importedOwnerData.Owners;
                    await ImportApps(importfileName, ownerChangeTracker);
                }
            }
            catch (Exception exc)
            {
                string errorText = $"File {importfileName} could not be processed.";
                Log.WriteError(LogMessageTitle, errorText, exc);
                await AddLogEntry(2, LevelFile, errorText);
                failedImports.Add(importfileName);
            }
        }

        private async Task ImportApps(string importfileName, OwnerChangeImportTracker ownerChangeTracker)
        {
            int successCounter = 0;
            int failCounter = 0;
            int deleteCounter = 0;
            int deleteFailCounter = 0;

            existingApps = await apiConnection.SendQueryAsync<List<FwoOwner>>(OwnerQueries.getOwners);
            foreach (var incomingApp in importedApps)
            {
                if (await SaveApp(incomingApp, ownerChangeTracker))
                {
                    ++successCounter;
                }
                else
                {
                    ++failCounter;
                }
            }
            string? importSource = importedApps.FirstOrDefault()?.ImportSource;
            if (importSource != null)
            {
                (deleteCounter, deleteFailCounter) = await DeactivateMissingApps(importSource, existingApps, importedApps, ownerChangeTracker);
            }
            string messageText = $"Imported from {importfileName}: {successCounter} apps, {failCounter} failed. Deactivated {deleteCounter} apps, {deleteFailCounter} failed.";
            Log.WriteInfo(LogMessageTitle, messageText);
            await AddLogEntry(0, LevelFile, messageText);

        }

        private async Task<bool> SaveApp(ModellingImportAppData incomingApp, OwnerChangeImportTracker ownerChangeTracker)
        {
            try
            {
                incomingApp = await NormalizeImportedUserReferences(incomingApp);
                int appId;
                if (!TryResolveOwnerLifeCycleStateId(incomingApp, out int? ownerLifeCycleStateId))
                {
                    string errorText = $"App {incomingApp.Name} could not be processed because owner lifecycle state \"{incomingApp.OwnerLifecycleState}\" is missing.";
                    Log.WriteWarning(LogMessageTitle, errorText);
                    await AddLogEntry(1, LevelApp, errorText);
                    return false;
                }
                List<OwnerResponsible> responsibles = BuildOwnerResponsibles(incomingApp);

                FwoOwner? existingApp = existingApps.FirstOrDefault(x => x.ExtAppId == incomingApp.ExtAppId);

                if (existingApp == null)
                {
                    appId = await NewApp(incomingApp, ownerLifeCycleStateId, responsibles);
                    await ownerChangeTracker.AddOwnerChange(null, appId, ChangelogActionType.INSERT, incomingApp.ImportSource);
                }
                else
                {
                    appId = existingApp.Id;
                    await UpdateApp(incomingApp, existingApp, ownerLifeCycleStateId, responsibles);
                    if (!existingApp.Active)
                    {
                        await ownerChangeTracker.AddOwnerChange(appId, appId, ChangelogActionType.CHANGE, incomingApp.ImportSource);
                    }
                }
                if (!string.IsNullOrWhiteSpace(incomingApp.MainUser) && IsResponsibleTypeActive(GlobalConst.kOwnerResponsibleTypeMain))
                {
                    await UpdateRoles(incomingApp.MainUser, GetRolesForType(GlobalConst.kOwnerResponsibleTypeMain));
                }
                // Store users from all imported responsibles in uiuser for email notifications.
                await AddAllResponsiblesToUiUser(responsibles);
                await InitRecert(incomingApp, existingApp, appId);
            }
            catch (Exception exc)
            {
                string errorText = $"App {incomingApp.Name} could not be processed.";
                Log.WriteError(LogMessageTitle, errorText, exc);
                await AddLogEntry(2, LevelApp, errorText);
                return false;
            }
            return true;
        }

        private async Task<ModellingImportAppData> NormalizeImportedUserReferences(ModellingImportAppData incomingApp)
        {
            ModellingImportAppData normalizedApp = new()
            {
                Name = incomingApp.Name,
                ExtAppId = incomingApp.ExtAppId,
                MainUser = await NormalizeImportedUserReference(incomingApp, incomingApp.MainUser, "main_user"),
                Criticality = incomingApp.Criticality,
                OwnerLifecycleState = incomingApp.OwnerLifecycleState,
                ImportSource = incomingApp.ImportSource,
                RecertInterval = incomingApp.RecertInterval,
                FirstRecertInterval = incomingApp.FirstRecertInterval,
                RecertActive = incomingApp.RecertActive,
                AppServers = [.. incomingApp.AppServers]
            };

            if (incomingApp.Responsibles == null)
            {
                normalizedApp.Responsibles = null;
                return normalizedApp;
            }

            normalizedApp.Responsibles = [];
            foreach ((string typeKey, List<string> identifiers) in incomingApp.Responsibles)
            {
                List<string> normalizedIdentifiers = [];
                foreach (string identifier in identifiers)
                {
                    string? normalizedIdentifier = await NormalizeImportedUserReference(incomingApp, identifier, $"responsibles[{typeKey}]");
                    if (!string.IsNullOrWhiteSpace(normalizedIdentifier))
                    {
                        normalizedIdentifiers.Add(normalizedIdentifier);
                    }
                }
                normalizedApp.Responsibles[typeKey] = normalizedIdentifiers;
            }
            return normalizedApp;
        }

        private async Task<string?> NormalizeImportedUserReference(ModellingImportAppData incomingApp, string? importedIdentifier, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(importedIdentifier))
            {
                return importedIdentifier;
            }

            string trimmedIdentifier = importedIdentifier.Trim();
            if (LooksLikeDistinguishedName(trimmedIdentifier))
            {
                return trimmedIdentifier;
            }

            string? resolvedDn = await ResolveImportedUserIdentifierToDn(trimmedIdentifier);
            if (!string.IsNullOrWhiteSpace(resolvedDn))
            {
                return resolvedDn.Trim();
            }

            string appLabel = string.IsNullOrWhiteSpace(incomingApp.Name)
                ? incomingApp.ExtAppId
                : $"{incomingApp.Name} ({incomingApp.ExtAppId})";
            string warningText = $"App \"{appLabel}\": could not resolve imported user id \"{trimmedIdentifier}\" from field \"{fieldName}\". Skipping entry.";
            Log.WriteWarning(LogMessageTitle, warningText);
            await AddLogEntry(1, LevelApp, warningText);
            return null;
        }

        /// <summary>
        /// Resolves a plain imported user identifier to an LDAP distinguished name.
        /// </summary>
        /// <param name="userIdentifier">Imported user identifier such as uid, cn, or login name.</param>
        /// <returns>Resolved user DN if found in any connected LDAP; otherwise null.</returns>
        protected virtual async Task<string?> ResolveImportedUserIdentifierToDn(string userIdentifier)
        {
            UiUser userToResolve = new() { Name = userIdentifier };
            foreach (Ldap ldap in connectedLdaps)
            {
                if (string.IsNullOrWhiteSpace(ldap.UserSearchPath))
                {
                    continue;
                }

                LdapEntry? ldapUser = await ldap.GetLdapEntry(userToResolve, false);
                if (!string.IsNullOrWhiteSpace(ldapUser?.Dn))
                {
                    return ldapUser.Dn;
                }
            }
            return null;
        }

        private static bool LooksLikeDistinguishedName(string identifier)
        {
            return identifier.Contains('=') && identifier.Contains(',');
        }

        private async Task<int> NewApp(ModellingImportAppData incomingApp, int? ownerLifeCycleStateId, List<OwnerResponsible> responsibles)
        {
            int appId = 0;
            var variables = new
            {
                name = incomingApp.Name,
                appIdExternal = incomingApp.ExtAppId,
                criticality = incomingApp.Criticality,
                recertInterval = incomingApp.RecertInterval ?? globalConfig.RecertificationPeriod,
                ownerLifeCycleStateId,
                importSource = incomingApp.ImportSource,
                commSvcPossible = false,
                recertActive = false
            };
            ReturnId[]? returnIds = (await apiConnection.SendQueryAsync<ReturnIdWrapper>(OwnerQueries.newOwner, variables)).ReturnIds;
            if (returnIds != null)
            {
                appId = returnIds[0].NewId;
                await UpdateOwnerResponsibles(appId, responsibles, []);
                await ApplyRolesToResponsibles(responsibles, rolesToSetByType);
                foreach (var appServer in incomingApp.AppServers)
                {
                    await NewAppServer(appServer, appId, incomingApp.ImportSource);
                }
            }
            return appId;
        }

        private async Task UpdateApp(ModellingImportAppData incomingApp, FwoOwner existingApp, int? ownerLifeCycleStateId, List<OwnerResponsible> responsibles)
        {
            var Variables = new
            {
                id = existingApp.Id,
                name = incomingApp.Name,
                appIdExternal = string.IsNullOrEmpty(incomingApp.ExtAppId) ? null : incomingApp.ExtAppId,
                criticality = incomingApp.Criticality,
                recertInterval = incomingApp.RecertInterval ?? globalConfig.RecertificationPeriod,
                ownerLifeCycleStateId,
                commSvcPossible = existingApp.CommSvcPossible,
                recertActive = incomingApp.RecertActive || existingApp.RecertActive
            };
            await apiConnection.SendQueryAsync<ReturnIdWrapper>(OwnerQueries.updateOwner, Variables);
            await UpdateOwnerResponsibles(existingApp.Id, responsibles, existingApp.OwnerResponsibles ?? []);
            await ApplyRolesToResponsibles(responsibles, rolesToSetByType);
            await ImportAppServers(incomingApp, existingApp.Id);
        }

        private async Task<(int deleted, int failed)> DeactivateMissingApps(string importSource, IEnumerable<FwoOwner> existingApps, List<ModellingImportAppData> importedApps, OwnerChangeImportTracker ownerChangeTracker)
        {
            int deletedCounter = 0, deleteFailCounter = 0;
            foreach (var existingApp in existingApps.Where(x => x.ImportSource == importSource && x.Active))
            {
                if (importedApps.FirstOrDefault(x => x.ExtAppId == existingApp.ExtAppId) == null)
                {
                    if (await DeactivateApp(existingApp, ownerChangeTracker))
                    {
                        ++deletedCounter;
                    }
                    else
                    {
                        ++deleteFailCounter;
                    }
                }
            }
            return (deletedCounter, deleteFailCounter);
        }

        private async Task<bool> DeactivateApp(FwoOwner app, OwnerChangeImportTracker ownerChangeTracker)
        {
            try
            {
                await apiConnection.SendQueryAsync<ReturnIdWrapper>(OwnerQueries.deactivateOwner, new { id = app.Id });
                await ownerChangeTracker.AddOwnerChange(app.Id, null, ChangelogActionType.DELETE, app.ImportSource);
            }
            catch (Exception exc)
            {
                string errorText = $"Outdated App {app.Name} could not be deactivated.";
                Log.WriteError(LogMessageTitle, errorText, exc);
                await AddLogEntry(1, LevelApp, errorText);
                return false;
            }
            return true;
        }

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
                .OrderBy(type => type.SortOrder).ThenBy(type => type.Id).Select(type => type.Id)];
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
                .Select(entry => new OwnerResponsible(entry.Value)).ToList();

            List<OwnerResponsible> responsiblesToDelete = existingByKey
                .Where(entry => !incomingByKey.ContainsKey(entry.Key) && globalConfig.OwnerDataImportSyncUsers)
                .Select(entry => new OwnerResponsible(entry.Value)).ToList();

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
                await UpdateRoles(responsible.Dn, filteredRoles);
            }
        }

        private bool TryResolveOwnerLifeCycleStateId(ModellingImportAppData incomingApp, out int? ownerLifeCycleStateId)
        {
            ownerLifeCycleStateId = null;
            if (string.IsNullOrWhiteSpace(incomingApp.OwnerLifecycleState))
            {
                return true;
            }
            string stateKey = incomingApp.OwnerLifecycleState.Trim();
            if (ownerLifeCycleStateIdsByName.TryGetValue(stateKey, out int resolvedId))
            {
                ownerLifeCycleStateId = resolvedId;
                return true;
            }
            return false;
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
            foreach (Ldap ldap in connectedLdaps)
            {
                bool isUserDn = !string.IsNullOrEmpty(ldap.UserSearchPath)
                    && responsibleDn.Contains(ldap.UserSearchPath, StringComparison.OrdinalIgnoreCase);
                if (isUserDn)
                {
                    await TryUpsertUiUser(responsibleDn, handledUserDns);
                    continue;
                }

                string groupKey = $"{ldap.Id}|{responsibleDn}";
                if (!handledGroupDnsByLdap.Add(groupKey))
                {
                    continue;
                }
                foreach (string memberDn in await ldap.GetGroupMembers(responsibleDn))
                {
                    if (!string.IsNullOrWhiteSpace(memberDn))
                    {
                        await TryUpsertUiUser(memberDn, handledUserDns);
                    }
                }
            }
        }

        private async Task TryUpsertUiUser(string userDn, HashSet<string> handledUserDns)
        {
            if (!handledUserDns.Add(userDn))
            {
                return;
            }
            UiUser? uiUser = await ConvertLdapToUiUser(userDn);
            if (uiUser != null)
            {
                await UiUserHandler.UpsertUiUser(apiConnection, uiUser, false);
            }
        }

        private async Task<UiUser?> ConvertLdapToUiUser(string userDn)
        {
            // add the modelling user to local uiuser table for later ref to email address
            // find the user in all connected ldaps
            foreach (Ldap ldap in connectedLdaps)
            {
                if (!string.IsNullOrEmpty(ldap.UserSearchPath) && userDn.ToLower().Contains(ldap.UserSearchPath!.ToLower()))
                {
                    LdapEntry? ldapUser = await ldap.GetUserDetailsFromLdap(userDn);

                    if (ldapUser != null)
                    {
                        // add data from ldap entry to uiUser
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
            }
            return null;
        }

        private async Task<Tenant> DeriveTenantFromLdap(Ldap ldap, LdapEntry ldapUser)
        {
            // try to derive the the user's tenant from the ldap settings
            Tenant tenant = new()
            {
                Id = GlobalConst.kTenant0Id  // default: tenant0 (id=1)
            };

            string tenantName = "";

            // can we derive the users tenant purely from its ldap?
            if (!string.IsNullOrEmpty(ldap.GlobalTenantName) || ldap.TenantLevel > 0)
            {
                if (ldap.TenantLevel > 0)
                {
                    // getting tenant via tenant level setting from distinguished name
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
                if (!roles.Contains(role))
                {
                    await internalLdap.AddUserToEntry(dn, GetRoleDn(role));
                }
            }
        }

        private async Task ImportAppServers(ModellingImportAppData incomingApp, int applId)
        {
            int successCounter = 0;
            int failCounter = 0;
            int deleteCounter = 0;
            int deleteFailCounter = 0;

            var Variables = new
            {
                importSource = incomingApp.ImportSource,
                appId = applId
            };
            existingAppServers = await apiConnection.SendQueryAsync<List<ModellingAppServer>>(ModellingQueries.getAppServersBySource, Variables);
            foreach (var incomingAppServer in incomingApp.AppServers)
            {
                if (await SaveAppServer(incomingAppServer, applId, incomingApp.ImportSource))
                {
                    ++successCounter;
                }
                else
                {
                    ++failCounter;
                }
            }
            foreach (var existingAppServer in existingAppServers.Where(e => !e.IsDeleted).ToList())
            {
                if (incomingApp.AppServers.FirstOrDefault(x => x.Ip.IpAsCidr() == existingAppServer.Ip.IpAsCidr() && x.IpEnd.IpAsCidr() == existingAppServer.IpEnd.IpAsCidr()) == null)
                {
                    if (await MarkDeletedAppServer(existingAppServer))
                    {
                        ++deleteCounter;
                    }
                    else
                    {
                        ++deleteFailCounter;
                    }
                }
            }
            Log.WriteDebug(LogMessageTitle, $"for App {incomingApp.Name}: Imported {successCounter} app servers, {failCounter} failed. {deleteCounter} app servers marked as deleted, {deleteFailCounter} failed.");
        }

        private async Task<bool> SaveAppServer(ModellingImportAppServer incomingAppServer, int appID, string impSource)
        {
            try
            {
                if (incomingAppServer.IpEnd == "")
                {
                    incomingAppServer.IpEnd = incomingAppServer.Ip;
                }
                if (globalConfig.DnsLookup)
                {
                    incomingAppServer.Name = await BuildAppServerName(incomingAppServer);
                }
                ModellingAppServer? existingAppServer = existingAppServers.FirstOrDefault(x => x.Ip.IpAsCidr() == incomingAppServer.Ip.IpAsCidr() && x.IpEnd.IpAsCidr() == incomingAppServer.IpEnd.IpAsCidr());
                if (existingAppServer == null)
                {
                    return await NewAppServer(incomingAppServer, appID, impSource);
                }

                if (existingAppServer.IsDeleted)
                {
                    if (!await ReactivateAppServer(existingAppServer))
                    {
                        return false;
                    }
                }
                else
                {
                    // in case there are still active appservers from other sources (resulting e.g. from older revisions)
                    await AppServerHelper.DeactivateOtherSources(apiConnection, userConfig, existingAppServer);
                }
                if (!existingAppServer.Name.Equals(incomingAppServer.Name))
                {
                    if (!await UpdateAppServerName(existingAppServer, incomingAppServer.Name))
                    {
                        return false;
                    }
                }
                if (existingAppServer.CustomType == null)
                {
                    if (!await UpdateAppServerType(existingAppServer))
                    {
                        return false;
                    }
                }
                return true;
            }
            catch (Exception exc)
            {
                string errorText = $"App Server {incomingAppServer.Name} could not be processed.";
                Log.WriteError(LogMessageTitle, errorText, exc);
                await AddLogEntry(1, LevelAppServer, errorText);
                return false;
            }
        }

        private async Task<string> BuildAppServerName(ModellingImportAppServer appServer)
        {
            try
            {
                return await AppServerHelper.ConstructAppServerNameFromDns(appServer.ToModellingAppServer(), NamingConvention, globalConfig.OverwriteExistingNames, true);
            }
            catch (Exception exc)
            {
                string errorText = $"App Server name {appServer.Name} could not be set according to naming conventions.";
                Log.WriteError(LogMessageTitle, errorText, exc);
                await AddLogEntry(1, LevelAppServer, errorText);
            }
            return appServer.Name;
        }

        private async Task<bool> NewAppServer(ModellingImportAppServer incomingAppServer, int appID, string impSource)
        {
            try
            {
                var Variables = new
                {
                    name = incomingAppServer.Name,
                    appId = appID,
                    ip = incomingAppServer.Ip.IpAsCidr(),
                    ipEnd = incomingAppServer.IpEnd != "" ? incomingAppServer.IpEnd.IpAsCidr() : incomingAppServer.Ip.IpAsCidr(),
                    importSource = impSource,
                    customType = 0
                };
                ReturnId[]? returnIds = (await apiConnection.SendQueryAsync<ReturnIdWrapper>(ModellingQueries.newAppServer, Variables)).ReturnIds;
                if (returnIds != null && returnIds.Length > 0)
                {
                    ModellingAppServer newModAppServer = new(incomingAppServer.ToModellingAppServer()) { Id = returnIds[0].NewIdLong, ImportSource = impSource, AppId = appID };
                    await ModellingHandlerBase.LogChange(new LogChangeRequest
                    {
                        ChangeType = ModellingTypes.ChangeType.Insert,
                        ObjectType = ModellingTypes.ModObjectType.AppServer,
                        ObjectId = newModAppServer.Id,
                        Text = $"New App Server: {newModAppServer.Display()}",
                        ApiConnection = apiConnection,
                        UserConfig = userConfig,
                        ApplicationId = newModAppServer.AppId,
                        DisplayMessageInUi = DefaultInit.DoNothing,
                        ChangeSource = newModAppServer.ImportSource
                    });
                    await AppServerHelper.DeactivateOtherSources(apiConnection, userConfig, newModAppServer);
                }
            }
            catch (Exception exc)
            {
                string errorText = $"App Server {incomingAppServer.Name} could not be processed.";
                Log.WriteError(LogMessageTitle, errorText, exc);
                await AddLogEntry(1, LevelAppServer, errorText);
                return false;
            }
            return true;
        }

        private async Task<bool> ReactivateAppServer(ModellingAppServer appServer)
        {
            try
            {
                var Variables = new
                {
                    id = appServer.Id,
                    deleted = false
                };
                await apiConnection.SendQueryAsync<ReturnIdWrapper>(ModellingQueries.setAppServerDeletedState, Variables);
                await ModellingHandlerBase.LogChange(new LogChangeRequest
                {
                    ChangeType = ModellingTypes.ChangeType.Reactivate,
                    ObjectType = ModellingTypes.ModObjectType.AppServer,
                    ObjectId = appServer.Id,
                    Text = $"Reactivate App Server: {appServer.Display()}",
                    ApiConnection = apiConnection,
                    UserConfig = userConfig,
                    ApplicationId = appServer.AppId,
                    DisplayMessageInUi = DefaultInit.DoNothing,
                    ChangeSource = appServer.ImportSource
                });
                await AppServerHelper.DeactivateOtherSources(apiConnection, userConfig, appServer);
            }
            catch (Exception exc)
            {
                string errorText = $"App Server {appServer.Name} could not be reactivated.";
                Log.WriteError(LogMessageTitle, errorText, exc);
                await AddLogEntry(1, LevelAppServer, errorText);
                return false;
            }
            return true;
        }

        private async Task<bool> UpdateAppServerType(ModellingAppServer appServer)
        {
            try
            {
                var Variables = new
                {
                    id = appServer.Id,
                    customType = 0
                };
                await apiConnection.SendQueryAsync<ReturnIdWrapper>(ModellingQueries.setAppServerType, Variables);
                await ModellingHandlerBase.LogChange(new LogChangeRequest
                {
                    ChangeType = ModellingTypes.ChangeType.Update,
                    ObjectType = ModellingTypes.ModObjectType.AppServer,
                    ObjectId = appServer.Id,
                    Text = $"Update App Server Type: {appServer.Display()}",
                    ApiConnection = apiConnection,
                    UserConfig = userConfig,
                    ApplicationId = appServer.AppId,
                    DisplayMessageInUi = DefaultInit.DoNothing,
                    ChangeSource = appServer.ImportSource
                });
            }
            catch (Exception exc)
            {
                string errorText = $"Type of App Server {appServer.Name} could not be set.";
                Log.WriteError(LogMessageTitle, errorText, exc);
                await AddLogEntry(1, LevelAppServer, errorText);
                return false;
            }
            return true;
        }

        private async Task<bool> UpdateAppServerName(ModellingAppServer appServer, string newName)
        {
            if (appServer.Name != newName)
            {
                try
                {
                    var Variables = new
                    {
                        newName,
                        id = appServer.Id,
                    };
                    await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.setAppServerName, Variables);
                    await ModellingHandlerBase.LogChange(new LogChangeRequest
                    {
                        ChangeType = ModellingTypes.ChangeType.Update,
                        ObjectType = ModellingTypes.ModObjectType.AppServer,
                        ObjectId = appServer.Id,
                        Text = $"Update App Server Name: {appServer.Display()}",
                        ApiConnection = apiConnection,
                        UserConfig = userConfig,
                        ApplicationId = appServer.AppId,
                        DisplayMessageInUi = DefaultInit.DoNothing,
                        ChangeSource = appServer.ImportSource
                    });
                    Log.WriteWarning(LogMessageTitle, $"Name of App Server changed from {appServer.Name} changed to {newName}");
                }
                catch (Exception exc)
                {
                    string errorText = $"Name of App Server {appServer.Name} could not be set to {newName}.";
                    Log.WriteError(LogMessageTitle, errorText, exc);
                    await AddLogEntry(1, LevelAppServer, errorText);
                    return false;
                }
            }
            return true;
        }

        private async Task<bool> MarkDeletedAppServer(ModellingAppServer appServer)
        {
            try
            {
                var Variables = new
                {
                    id = appServer.Id,
                    deleted = true
                };
                await apiConnection.SendQueryAsync<ReturnIdWrapper>(ModellingQueries.setAppServerDeletedState, Variables);
                await ModellingHandlerBase.LogChange(new LogChangeRequest
                {
                    ChangeType = ModellingTypes.ChangeType.Update,
                    ObjectType = ModellingTypes.ModObjectType.AppServer,
                    ObjectId = appServer.Id,
                    Text = $"Deactivate App Server: {appServer.Display()}",
                    ApiConnection = apiConnection,
                    UserConfig = userConfig,
                    ApplicationId = appServer.AppId,
                    DisplayMessageInUi = DefaultInit.DoNothing,
                    ChangeSource = appServer.ImportSource
                });
                await AppServerHelper.ReactivateOtherSource(apiConnection, userConfig, appServer);
            }
            catch (Exception exc)
            {
                string errorText = $"Outdated AppServer {appServer.Name} could not be marked as deleted.";
                Log.WriteError(LogMessageTitle, errorText, exc);
                await AddLogEntry(1, LevelAppServer, errorText);
                return false;
            }
            return true;
        }

        private async Task InitRecert(ModellingImportAppData incomingApp, FwoOwner? existingApp, int appId)
        {
            if (userConfig.RecertificationMode == RecertificationMode.OwnersAndRules &&
                incomingApp.RecertActive && (existingApp == null || !existingApp.RecertActive))
            {
                RecertHandler recertHandler = new(apiConnection, userConfig);
                await recertHandler.InitOwnerRecert(new()
                {
                    Id = appId,
                    RecertInterval = incomingApp.RecertInterval
                });
            }
        }

        private async Task AddLogEntry(int severity, string level, string description)
        {
            await AddLogEntry(GlobalConst.kImportAppData, severity, level, description);
        }
    }
}
