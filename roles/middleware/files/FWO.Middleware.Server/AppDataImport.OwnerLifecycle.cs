using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Logging;
using FWO.Recert;
using Novell.Directory.Ldap;

namespace FWO.Middleware.Server
{
    public partial class AppDataImport
    {
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
                FwoOwner? existingApp = ExistingApps.FirstOrDefault(x => x.ExtAppId == incomingApp.ExtAppId);

                if (existingApp == null)
                {
                    appId = await NewApp(incomingApp, ownerLifeCycleStateId, responsibles);
                    await ownerChangeTracker.AddOwnerChange(null, appId, ChangelogActionType.INSERT, incomingApp.ImportSource);
                }
                else
                {
                    appId = existingApp.Id;
                    await UpdateApp(incomingApp, existingApp, ownerLifeCycleStateId, responsibles);
                    await AddOwnerChangeIfNeeded(existingApp, incomingApp, ownerChangeTracker);
                    await AddOwnerLifeCycleStateActiveChangeIfNeeded(existingApp, ownerLifeCycleStateId, incomingApp.ImportSource, ownerChangeTracker);
                }

                if (!string.IsNullOrWhiteSpace(incomingApp.MainUser) && IsResponsibleTypeActive(GlobalConst.kOwnerResponsibleTypeMain))
                {
                    await UpdateRoles(incomingApp.MainUser, GetRolesForType(GlobalConst.kOwnerResponsibleTypeMain));
                }

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

        private async Task AddOwnerLifeCycleStateActiveChangeIfNeeded(
            FwoOwner existingApp,
            int? ownerLifeCycleStateId,
            string? importSource,
            OwnerChangeImportTracker ownerChangeTracker)
        {
            if (!TryGetOwnerLifeCycleStateActive(existingApp.OwnerLifeCycleStateId, out bool oldActiveState)
                || !TryGetOwnerLifeCycleStateActive(ownerLifeCycleStateId, out bool newActiveState)
                || oldActiveState == newActiveState)
            {
                return;
            }

            await ownerChangeTracker.AddOwnerChange(
                existingApp.Id,
                existingApp.Id,
                newActiveState ? ChangelogActionType.REACTIVATE : ChangelogActionType.DEACTIVATE,
                importSource);

            if (!newActiveState && hasImmediateAppDecommNotificationForImport)
            {
                await CheckActiveRulesSync(existingApp);
            }
        }

        private async Task AddOwnerChangeIfNeeded(
            FwoOwner existingApp,
            ModellingImportAppData incomingApp,
            OwnerChangeImportTracker ownerChangeTracker)
        {
            if (!existingApp.Active || HaveAppServerChanges(incomingApp))
            {
                await ownerChangeTracker.AddOwnerChange(existingApp.Id, existingApp.Id, ChangelogActionType.CHANGE, incomingApp.ImportSource);
            }
        }

        private bool HaveAppServerChanges(ModellingImportAppData incomingApp)
        {
            HashSet<string> existing = BuildAppServerKeys(ExistingAppServers);
            HashSet<string> incoming = BuildAppServerKeys(incomingApp.AppServers.Select(appServer => appServer.ToModellingAppServer()));
            return !existing.SetEquals(incoming);
        }

        private bool TryGetOwnerLifeCycleStateActive(int? ownerLifeCycleStateId, out bool activeState)
        {
            if (ownerLifeCycleStateId.HasValue && ownerLifeCycleStateActiveById.TryGetValue(ownerLifeCycleStateId.Value, out bool resolvedActiveState))
            {
                activeState = resolvedActiveState;
                return true;
            }

            activeState = false;
            return false;
        }

        private OwnerLifeCycleState? GetOwnerLifeCycleState(int? ownerLifeCycleStateId)
        {
            if (ownerLifeCycleStateId.HasValue && ownerLifeCycleStateActiveById.TryGetValue(ownerLifeCycleStateId.Value, out bool activeState))
            {
                return new OwnerLifeCycleState
                {
                    Id = ownerLifeCycleStateId.Value,
                    ActiveState = activeState
                };
            }

            return null;
        }

        private DateTime? GetDecommDateAfterLifecycleChange(FwoOwner existingApp, int? ownerLifeCycleStateId)
        {
            return OwnerLifeCycleState.GetDecommDate(
                existingApp.DecommDate,
                GetOwnerLifeCycleState(existingApp.OwnerLifeCycleStateId),
                GetOwnerLifeCycleState(ownerLifeCycleStateId),
                DateTime.UtcNow);
        }

        private DateTime? GetDecommDateForNewOwner(int? ownerLifeCycleStateId)
        {
            return OwnerLifeCycleState.GetDecommDate(
                null,
                null,
                GetOwnerLifeCycleState(ownerLifeCycleStateId),
                DateTime.UtcNow);
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

            string? resolvedDn = await ResolveImportedResponsibleIdentifierToDn(trimmedIdentifier);
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
        /// Resolves a plain imported responsible identifier to a distinguished name.
        /// User identifiers are tried first, then group identifiers.
        /// </summary>
        /// <param name="identifier">Imported user or group identifier from the source system.</param>
        /// <returns>Resolved distinguished name if found; otherwise null.</returns>
        protected virtual async Task<string?> ResolveImportedResponsibleIdentifierToDn(string identifier)
        {
            string? userDn = await ResolveImportedUserIdentifierToDn(identifier);
            if (!string.IsNullOrWhiteSpace(userDn))
            {
                return userDn;
            }

            return await ResolveImportedGroupIdentifierToDn(identifier);
        }

        /// <summary>
        /// Resolves a plain imported user identifier to a distinguished name.
        /// </summary>
        /// <param name="userIdentifier">Imported user identifier such as uid, cn, or login name.</param>
        /// <returns>Resolved user distinguished name if found in any connected LDAP; otherwise null.</returns>
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

        /// <summary>
        /// Resolves a plain imported group identifier to a distinguished name.
        /// </summary>
        /// <param name="groupIdentifier">Imported group identifier from the source system.</param>
        /// <returns>Resolved group distinguished name if found; otherwise null.</returns>
        protected virtual async Task<string?> ResolveImportedGroupIdentifierToDn(string groupIdentifier)
        {
            foreach (Ldap ldap in connectedLdaps)
            {
                List<string> matches = await ldap.GetAllGroups(groupIdentifier);
                if (matches.Count > 0)
                {
                    return matches[0];
                }
            }

            return null;
        }

        /// <summary>
        /// Checks whether the owner still has active rules after a lifecycle transition to an inactive state.
        /// </summary>
        /// <param name="owner">Owner to check.</param>
        protected virtual async Task CheckActiveRulesSync(FwoOwner owner)
        {
            await new OwnerActiveRuleCheck(apiConnection, globalConfig).CheckActiveRulesSync(owner);
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
                recertActive = false,
                decommDate = GetDecommDateForNewOwner(ownerLifeCycleStateId)
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
                decommDate = GetDecommDateAfterLifecycleChange(existingApp, ownerLifeCycleStateId),
                commSvcPossible = existingApp.CommSvcPossible,
                recertActive = incomingApp.RecertActive || existingApp.RecertActive
            };
            await apiConnection.SendQueryAsync<ReturnIdWrapper>(OwnerQueries.updateOwner, Variables);
            await UpdateOwnerResponsibles(existingApp.Id, responsibles, existingApp.OwnerResponsibles ?? []);
            await ApplyRolesToResponsibles(responsibles, rolesToSetByType);
            await ImportAppServers(incomingApp, existingApp.Id);
        }

        private async Task<(int deleted, int failed)> DeactivateMissingApps(string importSource, OwnerChangeImportTracker ownerChangeTracker)
        {
            int deletedCounter = 0, deleteFailCounter = 0;
            foreach (var existingApp in ExistingApps.Where(x => x.ImportSource == importSource && x.Active))
            {
                if (ImportedApps.FirstOrDefault(x => x.ExtAppId == existingApp.ExtAppId) == null)
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
    }
}
