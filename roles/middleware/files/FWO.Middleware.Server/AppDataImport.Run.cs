using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Logging;
using System.Text.Json;

namespace FWO.Middleware.Server
{
    public partial class AppDataImport
    {
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
            hasImmediateAppDecommNotificationForImport = await LoadHasImmediateAppDecommNotification();
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
            ownerLifeCycleStateActiveById = [];
            foreach (OwnerLifeCycleState state in lifeCycleStates)
            {
                if (!string.IsNullOrWhiteSpace(state.Name))
                {
                    ownerLifeCycleStateIdsByName[state.Name.Trim()] = state.Id;
                    ownerLifeCycleStateActiveById[state.Id] = state.ActiveState;
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
                    ImportedApps = importedOwnerData.Owners;
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

            ExistingApps = await apiConnection.SendQueryAsync<List<FwoOwner>>(OwnerQueries.getOwners);
            foreach (var incomingApp in ImportedApps)
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

            string? importSource = ImportedApps.FirstOrDefault()?.ImportSource;
            if (importSource != null)
            {
                (deleteCounter, deleteFailCounter) = await DeactivateMissingApps(importSource, ownerChangeTracker);
            }

            string messageText = $"Imported from {importfileName}: {successCounter} apps, {failCounter} failed. Deactivated {deleteCounter} apps, {deleteFailCounter} failed.";
            Log.WriteInfo(LogMessageTitle, messageText);
            await AddLogEntry(0, LevelFile, messageText);
        }

        /// <summary>
        /// Loads whether an immediate owner decommission notification exists.
        /// This is intended to be called once per whole import run.
        /// </summary>
        protected virtual async Task<bool> LoadHasImmediateAppDecommNotification()
        {
            List<FwoNotification> notifications = await apiConnection.SendQueryAsync<List<FwoNotification>>(
                NotificationQueries.getNotifications,
                new { client = NotificationClient.AppDecomm.ToString() });
            return notifications.Any(notification => notification.Deadline == NotificationDeadline.None);
        }

        private async Task AddLogEntry(int severity, string level, string description)
        {
            await AddLogEntry(GlobalConst.kImportAppData, severity, level, description);
        }
    }
}
