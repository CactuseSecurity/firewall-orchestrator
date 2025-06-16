﻿using FWO.Data;
using FWO.Data.Modelling;
using FWO.Basics;
using System.Net;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api;
using FWO.Logging;
using System.Text.Json;

namespace FWO.Services
{
    public static class AppServerHelper
    {
        public static async Task<string> ConstructAppServerNameFromDns(ModellingAppServer appServer, ModellingNamingConvention namingConvention,
            bool overwriteExistingNames=false, bool logUnresolvable=false)
        {
            if ((string.IsNullOrEmpty(appServer.IpEnd) || appServer.IpEnd == appServer.Ip) && IPAddress.TryParse(appServer.Ip.StripOffNetmask(), out IPAddress? ip))
            {
                string dnsName = await IpOperations.DnsReverseLookUp(ip);
                if(string.IsNullOrEmpty(dnsName))
                {
                    if(logUnresolvable)
                    {
                        Log.WriteWarning("Import App Server Data", $"Found empty (unresolvable) IP {appServer.Ip}");
                    }
                }
                else
                {
                    appServer.Name = dnsName;
                    return dnsName;
                }
            }
            if (string.IsNullOrEmpty(appServer.Name) || overwriteExistingNames)
            {
                appServer.Name = ConstructAppServerName(appServer, namingConvention, overwriteExistingNames);
            }
            return appServer.Name;
        }

        public static string ConstructSanitizedAppServerName(ModellingAppServer appServer, ModellingNamingConvention namingConvention, bool overwriteExistingNames=false)
        {
            bool shortened = false;
            return Sanitizer.SanitizeJsonFieldMand(ConstructAppServerName(appServer, namingConvention, overwriteExistingNames), ref shortened);
        }

        public static string ConstructAppServerName(ModellingAppServer appServer, ModellingNamingConvention namingConvention, bool overwriteExistingNames=false)
        {
            if (string.IsNullOrEmpty(appServer.Name) || overwriteExistingNames)
            {
                return GetPrefix(appServer, namingConvention) + DisplayBase.DisplayIp(appServer.Ip, appServer.IpEnd);
            }
            return char.IsLetter(appServer.Name[0]) ? appServer.Name : GetPrefix(appServer, namingConvention) + appServer.Name;
        }

        public static async Task AdjustAppServerNames(ApiConnection apiConnection, UserConfig userConfig)
        {
            Log.WriteDebug($"Start adjusting App Server Names", "");
            ModellingNamingConvention namingConvention = JsonSerializer.Deserialize<ModellingNamingConvention>(userConfig.ModNamingConvention) ?? new();
            List<ModellingAppServer> AppServers = await apiConnection.SendQueryAsync<List<ModellingAppServer>>(ModellingQueries.getAllAppServers);
            int correctedCounter = 0;
            int failCounter = 0;
            foreach(var appServer in AppServers)
            {
                string oldName = appServer.Name;
                if((await ConstructAppServerNameFromDns(appServer, namingConvention, userConfig.OverwriteExistingNames)) != oldName)
                {
                    appServer.ImportSource = GlobalConst.kAdjustAppServerNames;
                    if (await UpdateName(apiConnection, userConfig, appServer, oldName))
                    {
                        correctedCounter++;
                    }
                    else
                    {
                        failCounter++;
                    }
                }
            }
            Log.WriteInfo($"Adjusted App Server Names", $"{correctedCounter} out of {AppServers.Count} App Servers have been corrected, {failCounter} failed");
        }

        public static async Task<bool> NoHigherPrioActive(ApiConnection apiConnection, ModellingAppServer incomingAppServer)
        {
            try
            {
                List<ModellingAppServer> ExistingAppServersSameIp = await GetExistingSameIp(apiConnection, incomingAppServer);
                return ExistingAppServersSameIp.FirstOrDefault(x => Prio(x.ImportSource) > Prio(incomingAppServer.ImportSource) && !x.IsDeleted) == null;
            }
            catch(Exception exception)
            {
                Log.WriteError("Check App Server Prio", $" Check of {incomingAppServer.Name} from {incomingAppServer.ImportSource} to exception:", exception);
            }
            return true;
        }

        public static async Task ReactivateOtherSource(ApiConnection apiConnection, UserConfig userConfig, ModellingAppServer deletedAppServer)
        {
            try
            {
                List<ModellingAppServer> ExistingOtherAppServersSameIp = [.. (await GetExistingSameIp(apiConnection, deletedAppServer)).Where(x => x.Id != deletedAppServer.Id)];
                if(ExistingOtherAppServersSameIp != null && ExistingOtherAppServersSameIp.Count > 0)
                {
                    int maxPrio = ExistingOtherAppServersSameIp.Max(x => Prio(x.ImportSource));
                    List<ModellingAppServer> ExistingOtherAppServersMaxPrio = [.. ExistingOtherAppServersSameIp.Where(x => Prio(x.ImportSource) == maxPrio)];
                    long reactivatedId = ExistingOtherAppServersMaxPrio.Max(x => x.Id);
                    ModellingAppServer reactivatedAppServer = ExistingOtherAppServersMaxPrio.First(x => x.Id == reactivatedId);
                    var Variables = new
                    {
                        id = reactivatedId,
                        deleted = false
                    };
                    await apiConnection.SendQueryAsync<ReturnIdWrapper>(ModellingQueries.setAppServerDeletedState, Variables);
                    await ModellingHandlerBase.LogChange(ModellingTypes.ChangeType.Reactivate, ModellingTypes.ModObjectType.AppServer, reactivatedAppServer.Id,
                        $"Reactivated App Server: {reactivatedAppServer.Display()}", apiConnection, userConfig, reactivatedAppServer.AppId, DefaultInit.DoNothing, null, reactivatedAppServer.ImportSource);
                    if(userConfig.AutoReplaceAppServer)
                    {
                        await ReplaceAppServer(apiConnection, deletedAppServer.Id, reactivatedId);
                    }
                }
            }
            catch(Exception exception)
            {
                Log.WriteError("Reactivate App Server", $"Reactivation of other than {deletedAppServer.Name} from {deletedAppServer.ImportSource} leads to exception:", exception);
            }
        }

        public static async Task DeactivateOtherSources(ApiConnection apiConnection, UserConfig userConfig, ModellingAppServer incomingAppServer)
        {
            try
            {
                List<ModellingAppServer> ExistingActiveAppServersSameIp = [.. (await GetExistingSameIp(apiConnection, incomingAppServer)).Where(x => x.Id != incomingAppServer.Id && !x.IsDeleted)];
                if(ExistingActiveAppServersSameIp != null && ExistingActiveAppServersSameIp.Count > 0)
                {
                    foreach(var activeAppServer in ExistingActiveAppServersSameIp)
                    {
                        await DeactivateAppServer(apiConnection, userConfig, activeAppServer, incomingAppServer);
                    }
                }
            }
            catch(Exception exception)
            {
                Log.WriteError("Deactivate App Servers", $"Deactivation of {incomingAppServer.Name} from {incomingAppServer.ImportSource} leads to exception:", exception);
            }
        }

        public static async Task<(long?, string?)> UpsertAppServer(ApiConnection apiConnection, UserConfig userConfig, ModellingAppServer incomingAppServer, bool nameCheck, bool manual=false, bool addMode=false)
        {
            try
            {
                if(nameCheck && await CheckNameExisting(apiConnection, incomingAppServer))
                {
                    return (null, incomingAppServer.Name);
                }

                List<ModellingAppServer> ExistingAppServersSameIp = await GetExistingSameIp(apiConnection, incomingAppServer);

                long? AppServerId = null;
                if(ExistingAppServersSameIp == null || ExistingAppServersSameIp.Count == 0)
                {
                    AppServerId = manual && !addMode ? await UpdateAppServerInDb(apiConnection, userConfig, incomingAppServer) :
                        await AddAppServerToDb(apiConnection, userConfig, incomingAppServer);
                    return (AppServerId, null);
                }

                ModellingAppServer? higherPrioAppServer = ExistingAppServersSameIp.FirstOrDefault(x => Prio(x.ImportSource) > Prio(incomingAppServer.ImportSource) && !x.IsDeleted);
                if (higherPrioAppServer != null)
                {
                    return (null, higherPrioAppServer.Name);
                }

                if (manual)
                {
                    ModellingAppServer? otherAppServerSameIp = ExistingAppServersSameIp.FirstOrDefault(x => x.Id != incomingAppServer.Id && !x.IsDeleted);
                    if(otherAppServerSameIp != null)
                    {
                        return (null, otherAppServerSameIp.Name);
                    }
                }

                return await OverwriteAppServer(apiConnection, userConfig, incomingAppServer, ExistingAppServersSameIp);
            }
            catch(Exception exception)
            {
                Log.WriteError("Upsert App Server", $"Upsert of {incomingAppServer.Name} leads to exception:", exception);
                return (null, null);
            }
        }

        private static async Task<List<ModellingAppServer>> GetExistingSameIp(ApiConnection apiConnection, ModellingAppServer appServer)
        {
            try
            {
                var Variables = new
                {
                    appId = appServer.AppId,
                    ip = appServer.Ip.IpAsCidr(),
                    ipEnd = appServer.IpEnd.IpAsCidr()
                };
                return await apiConnection.SendQueryAsync<List<ModellingAppServer>>(ModellingQueries.getAppServersByIp, Variables);
            }
            catch(Exception exception)
            {
                Log.WriteError("Get Existing App Server", $"leads to exception:", exception);
                return [];
            }
        }

        private static async Task<(long?, string?)> OverwriteAppServer(ApiConnection apiConnection, UserConfig userConfig, ModellingAppServer incomingAppServer, List<ModellingAppServer> existingAppServersSameIp)
        {
            long? AppServerId = null;
            string? exAppServerName = null;
            ModellingAppServer? existAppServerSameSource = existingAppServersSameIp.FirstOrDefault(x => x.ImportSource == incomingAppServer.ImportSource);
            if (existAppServerSameSource != null)
            {
                incomingAppServer.Id = existAppServerSameSource.Id;
                await UpdateAppServerInDb(apiConnection, userConfig, incomingAppServer);
                AppServerId = incomingAppServer.Id;
                exAppServerName = existAppServerSameSource.Name;
            }
            else
            {
                AppServerId = await AddAppServerToDb(apiConnection, userConfig, incomingAppServer);
            }

            foreach(var existAppServerOtherSource in existingAppServersSameIp.Where(x => x.ImportSource != incomingAppServer.ImportSource))
            {
                if(!existAppServerOtherSource.IsDeleted)
                {
                    await DeactivateAppServer(apiConnection, userConfig, existAppServerOtherSource, incomingAppServer);
                }
            }
            return (AppServerId, exAppServerName);
        }

        private static async Task DeactivateAppServer(ApiConnection apiConnection, UserConfig userConfig, ModellingAppServer appServerToDeactivate, ModellingAppServer newAppServer)
        {
            try
            {
                await apiConnection.SendQueryAsync<ReturnIdWrapper>(ModellingQueries.setAppServerDeletedState, new { id = appServerToDeactivate.Id, deleted = true });
                await ModellingHandlerBase.LogChange(ModellingTypes.ChangeType.MarkDeleted, ModellingTypes.ModObjectType.AppServer, appServerToDeactivate.Id,
                    $"Deactivated App Server: {appServerToDeactivate.Display()}", apiConnection, userConfig, appServerToDeactivate.AppId, DefaultInit.DoNothing, null, appServerToDeactivate.ImportSource);
                if(userConfig.AutoReplaceAppServer)
                {
                    await ReplaceAppServer(apiConnection, appServerToDeactivate.Id, newAppServer.Id);
                }
            }
            catch(Exception exception)
            {
                Log.WriteError("Deactivate App Server", $"leads to exception:", exception);
            }
        }

        private static async Task ReplaceAppServer(ApiConnection apiConnection, long oldAppServerId, long newAppServerId)
        {
            try
            {
                await apiConnection.SendQueryAsync<ReturnIdWrapper>(ModellingQueries.updateNwObjectInNwGroup, new { oldObjectId = oldAppServerId, newObjectId = newAppServerId });
                await apiConnection.SendQueryAsync<ReturnIdWrapper>(ModellingQueries.updateNwObjectInConnection, new { oldObjectId = oldAppServerId, newObjectId = newAppServerId });
            }
            catch(Exception exception)
            {
                Log.WriteError("Replace App Server", $"Replacing {oldAppServerId} by {newAppServerId} leads to exception:", exception);
            }
        }

        private static async Task<bool> CheckNameExisting(ApiConnection apiConnection, ModellingAppServer incomingAppServer)
        {
            try
            {
                var Variables = new
                {
                    appId = incomingAppServer.AppId,
                    name = incomingAppServer.Name
                };
                List<ModellingAppServer> ExistingAppServersSameIp = await apiConnection.SendQueryAsync<List<ModellingAppServer>>(ModellingQueries.getAppServersByName, Variables);
                return ExistingAppServersSameIp != null && ExistingAppServersSameIp.Count > 0 && ExistingAppServersSameIp.FirstOrDefault(x => x.Id == incomingAppServer.Id && !x.IsDeleted) == null;
            }
            catch(Exception exception)
            {
                Log.WriteError("Upsert App Server", $"leads to exception:", exception);
                return false;
            }
        } 

        private static string GetPrefix(ModellingAppServer appServer, ModellingNamingConvention namingConvention)
        {
            return IpOperations.GetObjectType(appServer.Ip, appServer.IpEnd) switch
            {
                ObjectType.Host => namingConvention.AppServerPrefix ?? "",
                ObjectType.Network => namingConvention.NetworkPrefix ?? "",
                ObjectType.IPRange => namingConvention.IpRangePrefix ?? "",
                _ => ""
            };
        }
        
        private static async Task<bool> UpdateName(ApiConnection apiConnection, UserConfig userConfig, ModellingAppServer appServer, string oldName)
        {
            try
            {
                var Variables = new
                {
                    id = appServer.Id,
                    newName = appServer.Name
                };
                await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.setAppServerName, Variables);
                await ModellingHandlerBase.LogChange(ModellingTypes.ChangeType.Update, ModellingTypes.ModObjectType.AppServer, appServer.Id,
                    $"Updated App Server Name: {appServer.Display()}", apiConnection, userConfig, appServer.AppId, DefaultInit.DoNothing, null, appServer.ImportSource);
                Log.WriteDebug($"Correct App Server Name", $"Changed {oldName} to {appServer.Name}.");
                return true;
            }
            catch(Exception exception)
            {
                Log.WriteError("Correct AppServer Name", $"Leads to exception:", exception);
                return false;
            }
        }

        private static int Prio(string importSource)
        {
            return (importSource == GlobalConst.kManual || importSource.StartsWith(GlobalConst.kCSV_)) ? 0 : 1;
        }

        private static async Task<long?> AddAppServerToDb(ApiConnection apiConnection, UserConfig userConfig, ModellingAppServer appServer)
        {
            try
            {
                var Variables = new
                {
                    name = appServer.Name,
                    appId = appServer.AppId,
                    ip = appServer.Ip.IpAsCidr(),
                    ipEnd = appServer.IpEnd.IpAsCidr(),
                    importSource = appServer.ImportSource,
                    customType = appServer.CustomType
                };
                ReturnId[]? returnIds = (await apiConnection.SendQueryAsync<ReturnIdWrapper>(ModellingQueries.newAppServer, Variables)).ReturnIds;
                if(returnIds != null && returnIds.Length > 0)
                {
                    appServer.Id = returnIds[0].NewIdLong;
                    await ModellingHandlerBase.LogChange(ModellingTypes.ChangeType.Insert, ModellingTypes.ModObjectType.AppServer, appServer.Id,
                        $"New App Server: {appServer.Display()}", apiConnection, userConfig, appServer.AppId, DefaultInit.DoNothing, null, appServer.ImportSource);
                    return appServer.Id;
                }
                return null;
            }
            catch (Exception exception)
            {
                Log.WriteError("Add App Server", $"Leads to exception:", exception);
                return null;
            }
        }

        private static async Task<long?> UpdateAppServerInDb(ApiConnection apiConnection, UserConfig userConfig, ModellingAppServer appServer)
        {
            try
            {
                var Variables = new
                {
                    id = appServer.Id,
                    name = appServer.Name,
                    appId = appServer.AppId,
                    ip = appServer.Ip.IpAsCidr(),
                    ipEnd = appServer.IpEnd.IpAsCidr(),
                    importSource = appServer.ImportSource,
                    customType = appServer.CustomType
                };
                await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.updateAppServer, Variables);
                await ModellingHandlerBase.LogChange(ModellingTypes.ChangeType.Update, ModellingTypes.ModObjectType.AppServer, appServer.Id,
                    $"Updated App Server: {appServer.Display()}", apiConnection, userConfig, appServer.AppId, DefaultInit.DoNothing, null, appServer.ImportSource);
                return appServer.Id;
            }
            catch (Exception exception)
            {
                Log.WriteError("Add App Server", $"Leads to exception:", exception);
                return null;
            }
        }
    }
}
