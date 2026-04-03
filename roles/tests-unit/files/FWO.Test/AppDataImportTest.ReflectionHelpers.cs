using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FWO.Api.Client;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Middleware.Server;
using Novell.Directory.Ldap;

namespace FWO.Test
{
    internal partial class AppDataImportTest
    {
        private static AppDataImport CreateImportWithTypeMap(Dictionary<string, int> typeMap)
        {
            AppDataImport import = new(new SimulatedApiConnection(), new GlobalConfig());
            FieldInfo field = typeof(AppDataImport).GetField("ownerResponsibleTypeIdByName", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException("ownerResponsibleTypeIdByName field not found.");
            field.SetValue(import, typeMap);
            SetResponsibleTypes(import, [.. typeMap.Select(entry => new OwnerResponsibleType
            {
                Id = entry.Value,
                Name = entry.Key,
                SortOrder = entry.Value
            })]);
            return import;
        }

        private static void SetResponsibleTypes(AppDataImport import, IEnumerable<OwnerResponsibleType> responsibleTypes)
        {
            Dictionary<int, OwnerResponsibleType> byId = responsibleTypes.ToDictionary(type => type.Id, type => type);
            FieldInfo field = typeof(AppDataImport).GetField("ownerResponsibleTypeById", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException("ownerResponsibleTypeById field not found.");
            field.SetValue(import, byId);
        }

        private static void SetOwnerLifeCycleMap(AppDataImport import, Dictionary<string, int> stateMap)
        {
            FieldInfo field = typeof(AppDataImport).GetField("ownerLifeCycleStateIdsByName", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException("ownerLifeCycleStateIdsByName field not found.");
            field.SetValue(import, stateMap);
        }

        private static void SetOwnerLifeCycleActiveMap(AppDataImport import, Dictionary<int, bool> stateMap)
        {
            FieldInfo field = typeof(AppDataImport).GetField("ownerLifeCycleStateActiveById", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException("ownerLifeCycleStateActiveById field not found.");
            field.SetValue(import, stateMap);
        }

        private static void SetOwnerDataImportSyncUsers(AppDataImport import, bool syncUsers)
        {
            FieldInfo field = typeof(DataImportBase).GetField("globalConfig", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException("globalConfig field not found.");
            GlobalConfig globalConfig = (GlobalConfig)field.GetValue(import)!;
            globalConfig.OwnerDataImportSyncUsers = syncUsers;
        }

        private static void SetRolesByType(AppDataImport import, Dictionary<int, List<string>> rolesByType)
        {
            FieldInfo field = typeof(AppDataImport).GetField("rolesToSetByType", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException("rolesToSetByType field not found.");
            field.SetValue(import, rolesByType);
        }

        private static void SetInternalLdap(AppDataImport import, Ldap? ldap)
        {
            FieldInfo field = typeof(AppDataImport).GetField("internalLdap", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException("internalLdap field not found.");
            field.SetValue(import, ldap);
        }

        private static void SetConnectedLdaps(AppDataImport import, List<Ldap> ldaps)
        {
            FieldInfo field = typeof(AppDataImport).GetField("connectedLdaps", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException("connectedLdaps field not found.");
            field.SetValue(import, ldaps);
        }

        private static void SetHasImmediateAppDecommNotificationForImport(AppDataImport import, bool value)
        {
            FieldInfo field = typeof(AppDataImport).GetField("hasImmediateAppDecommNotificationForImport", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException("hasImmediateAppDecommNotificationForImport field not found.");
            field.SetValue(import, value);
        }

        private static List<OwnerResponsible> InvokeBuildOwnerResponsibles(AppDataImport import, ModellingImportAppData incomingApp, string userGroupDn, IEnumerable<string> extraDns)
        {
            MethodInfo method = typeof(AppDataImport).GetMethod(
                "BuildOwnerResponsibles",
                BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException("BuildOwnerResponsibles helper not found.");
            return (List<OwnerResponsible>)method.Invoke(import, [incomingApp])!;
        }

        private static (bool ok, int? id) InvokeTryResolveOwnerLifeCycleStateId(AppDataImport import, ModellingImportAppData incomingApp)
        {
            MethodInfo method = typeof(AppDataImport).GetMethod(
                "TryResolveOwnerLifeCycleStateId",
                BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException("TryResolveOwnerLifeCycleStateId helper not found.");

            object?[] args = [incomingApp, null];
            bool ok = (bool)method.Invoke(import, args)!;
            return (ok, (int?)args[1]);
        }

        private static Dictionary<int, List<string>> InvokeParseRolesWithImport(string rolesJson)
        {
            MethodInfo method = typeof(AppDataImport).GetMethod(
                "ParseRolesWithImport",
                BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new InvalidOperationException("ParseRolesWithImport helper not found.");
            return (Dictionary<int, List<string>>)method.Invoke(null, [rolesJson])!;
        }

        private static async Task InvokeApplyRolesToResponsibles(
            AppDataImport import,
            List<OwnerResponsible> responsibles,
            Dictionary<int, List<string>> rolesByType)
        {
            MethodInfo method = typeof(AppDataImport).GetMethod(
                "ApplyRolesToResponsibles",
                BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException("ApplyRolesToResponsibles helper not found.");
            await (Task)method.Invoke(import, [responsibles, rolesByType])!;
        }

        private static bool InvokeIsResponsibleTypeActive(AppDataImport import, int typeId)
        {
            MethodInfo method = typeof(AppDataImport).GetMethod(
                "IsResponsibleTypeActive",
                BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException("IsResponsibleTypeActive helper not found.");
            return (bool)method.Invoke(import, [typeId])!;
        }

        private static List<string> InvokeGetRolesForType(AppDataImport import, int typeId)
        {
            MethodInfo method = typeof(AppDataImport).GetMethod(
                "GetRolesForType",
                BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException("GetRolesForType helper not found.");
            return (List<string>)method.Invoke(import, [typeId])!;
        }

        private static async Task InvokeAddAllResponsiblesToUiUser(AppDataImport import, IEnumerable<OwnerResponsible> responsibles)
        {
            MethodInfo method = typeof(AppDataImport).GetMethod(
                "AddAllResponsiblesToUiUser",
                BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException("AddAllResponsiblesToUiUser helper not found.");
            await (Task)method.Invoke(import, [responsibles])!;
        }

        private static async Task InvokeAddResponsibleDnToUiUser(
            AppDataImport import,
            string responsibleDn,
            HashSet<string> handledUserDns,
            HashSet<string> handledGroupDnsByLdap)
        {
            MethodInfo method = typeof(AppDataImport).GetMethod(
                "AddResponsibleDnToUiUser",
                BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException("AddResponsibleDnToUiUser helper not found.");
            await (Task)method.Invoke(import, [responsibleDn, handledUserDns, handledGroupDnsByLdap])!;
        }

        private static async Task<UiUser?> InvokeConvertLdapToUiUser(AppDataImport import, string userDn)
        {
            MethodInfo method = typeof(AppDataImport).GetMethod(
                "ConvertLdapToUiUser",
                BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException("ConvertLdapToUiUser helper not found.");
            return await (Task<UiUser?>)method.Invoke(import, [userDn])!;
        }

        private static async Task<bool> InvokeSaveApp(AppDataImport import, ModellingImportAppData incomingApp, OwnerChangeImportTracker tracker)
        {
            MethodInfo method = typeof(AppDataImport).GetMethod(
                "SaveApp",
                BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException("SaveApp helper not found.");
            return await (Task<bool>)method.Invoke(import, [incomingApp, tracker])!;
        }

        private static async Task InvokeAddOwnerLifeCycleStateActiveChangeIfNeeded(
            AppDataImport import,
            FwoOwner existingApp,
            int? ownerLifeCycleStateId,
            string? importSource,
            OwnerChangeImportTracker tracker)
        {
            MethodInfo method = typeof(AppDataImport).GetMethod(
                "AddOwnerLifeCycleStateActiveChangeIfNeeded",
                BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException("AddOwnerLifeCycleStateActiveChangeIfNeeded helper not found.");
            await (Task)method.Invoke(import, [existingApp, ownerLifeCycleStateId, importSource, tracker])!;
        }

        private static async Task InvokeAddOwnerChangeIfNeeded(
            AppDataImport import,
            FwoOwner existingApp,
            ModellingImportAppData incomingApp,
            OwnerChangeImportTracker tracker)
        {
            MethodInfo method = typeof(AppDataImport).GetMethod(
                "AddOwnerChangeIfNeeded",
                BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException("AddOwnerChangeIfNeeded helper not found.");
            await (Task)method.Invoke(import, [existingApp, incomingApp, tracker])!;
        }

        private static async Task<ModellingImportAppData> InvokeNormalizeImportedUserReferences(AppDataImport import, ModellingImportAppData incomingApp)
        {
            MethodInfo method = typeof(AppDataImport).GetMethod(
                "NormalizeImportedUserReferences",
                BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException("NormalizeImportedUserReferences helper not found.");
            return await (Task<ModellingImportAppData>)method.Invoke(import, [incomingApp])!;
        }

        private static async Task<(int deleted, int failed)> InvokeDeactivateMissingApps(
            AppDataImport import,
            string importSource,
            IEnumerable<FwoOwner> existingApps,
            List<ModellingImportAppData> importedApps,
            OwnerChangeImportTracker tracker)
        {
            SetExistingApps(import, existingApps.ToList());
            SetImportedApps(import, importedApps);
            MethodInfo method = typeof(AppDataImport).GetMethod(
                "DeactivateMissingApps",
                BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException("DeactivateMissingApps helper not found.");
            return ((int deleted, int failed))await (Task<(int deleted, int failed)>)method.Invoke(import, [importSource, tracker])!;
        }

        private static async Task InvokeUpdateOwnerResponsibles(
            AppDataImport import,
            int ownerId,
            List<OwnerResponsible> responsibles,
            List<OwnerResponsible> existingResponsibles)
        {
            MethodInfo method = typeof(AppDataImport).GetMethod(
                "UpdateOwnerResponsibles",
                BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException("UpdateOwnerResponsibles helper not found.");
            await (Task)method.Invoke(import, [ownerId, responsibles, existingResponsibles])!;
        }

        private static (List<OwnerResponsible> toInsert, List<OwnerResponsible> toDelete) InvokeCheckResponsibles(
            AppDataImport import,
            List<OwnerResponsible> existingResponsibles,
            List<OwnerResponsible> incomingResponsibles)
        {
            MethodInfo method = typeof(AppDataImport).GetMethod(
                "CheckResponsibles",
                BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException("CheckResponsibles helper not found.");
            object result = method.Invoke(import, [existingResponsibles, incomingResponsibles])!;

            Type resultType = result.GetType();
            List<OwnerResponsible> toInsert = (List<OwnerResponsible>)(resultType.GetField("Item1")?.GetValue(result)
                ?? throw new InvalidOperationException("CheckResponsibles result Item1 not found."));
            List<OwnerResponsible> toDelete = (List<OwnerResponsible>)(resultType.GetField("Item2")?.GetValue(result)
                ?? throw new InvalidOperationException("CheckResponsibles result Item2 not found."));

            return (toInsert, toDelete);
        }

        private static void SetImportedApps(AppDataImport import, List<ModellingImportAppData> importedApps)
        {
            FieldInfo field = typeof(AppDataImport).GetField("ImportedApps", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException("ImportedApps field not found.");
            field.SetValue(import, importedApps);
        }

        private static void SetExistingApps(AppDataImport import, List<FwoOwner> existingApps)
        {
            FieldInfo field = typeof(AppDataImport).GetField("ExistingApps", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException("ExistingApps field not found.");
            field.SetValue(import, existingApps);
        }

        private static void SetExistingAppServers(AppDataImport import, List<ModellingAppServer> existingAppServers)
        {
            FieldInfo field = typeof(AppDataImport).GetField("ExistingAppServers", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException("ExistingAppServers field not found.");
            field.SetValue(import, existingAppServers);
        }
    }
}
