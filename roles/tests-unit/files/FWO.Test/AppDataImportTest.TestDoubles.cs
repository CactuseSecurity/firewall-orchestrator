using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Middleware.Server;
using Novell.Directory.Ldap;

namespace FWO.Test
{
    internal partial class AppDataImportTest
    {
        private sealed class AppDataImportFlowTestApiConn : SimulatedApiConnection
        {
            public int GetOwnersCalls { get; private set; }
            public int DeactivateOwnerCalls { get; private set; }
            public int UpdateChangelogOwnerCalls { get; private set; }
            public HashSet<int> FailDeactivateOwnerIds { get; } = [];

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null)
            {
                if (query == OwnerQueries.getOwners)
                {
                    ++GetOwnersCalls;
                    return Task.FromResult((QueryResponseType)(object)new List<FwoOwner>());
                }

                if (query == OwnerQueries.deactivateOwner)
                {
                    ++DeactivateOwnerCalls;
                    int ownerId = GetAnonymousInt(variables, "id");
                    if (FailDeactivateOwnerIds.Contains(ownerId))
                    {
                        throw new InvalidOperationException($"Deactivate failed for owner {ownerId}.");
                    }

                    return Task.FromResult((QueryResponseType)(object)new ReturnIdWrapper
                    {
                        ReturnIds = [new ReturnId { NewId = ownerId }]
                    });
                }

                if (query == ImportQueries.addImportForOwner)
                {
                    return Task.FromResult((QueryResponseType)(object)new InsertImportControl
                    {
                        Returning = new List<ImportControl>
                        {
                            new ImportControl { ControlId = 123 }
                        }
                    });
                }

                if (query == OwnerQueries.updateChangelogOwner)
                {
                    ++UpdateChangelogOwnerCalls;
                    return Task.FromResult((QueryResponseType)(object)new object());
                }

                if (query == MonitorQueries.addDataImportLogEntry)
                {
                    return Task.FromResult((QueryResponseType)(object)new ReturnIdWrapper
                    {
                        ReturnIds = [new ReturnId { NewId = 1 }]
                    });
                }

                throw new NotImplementedException($"Query not implemented in test api: {query}");
            }

            private static int GetAnonymousInt(object? variables, string propertyName)
            {
                if (variables == null)
                {
                    return 0;
                }

                PropertyInfo? property = variables.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
                if (property == null)
                {
                    return 0;
                }

                object? value = property.GetValue(variables);
                return value is int intValue ? intValue : 0;
            }
        }

        private sealed class AppDataImportResponsiblesApiConn : SimulatedApiConnection
        {
            public int NewOwnerResponsiblesCalls { get; private set; }
            public int DeleteSpecificOwnerResponsiblesCalls { get; private set; }
            public List<(int ownerId, string dn, int responsibleType)> Inserted { get; } = [];
            public List<(int ownerId, string dn, int responsibleType)> Deleted { get; } = [];

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null)
            {
                if (query == OwnerQueries.newOwnerResponsibles)
                {
                    ++NewOwnerResponsiblesCalls;
                    if (variables != null)
                    {
                        object? objects = GetAnonymousValue(variables, "responsibles");
                        if (objects is IEnumerable enumerable)
                        {
                            foreach (object entry in enumerable)
                            {
                                Inserted.Add((
                                    GetAnonymousInt(entry, "owner_id"),
                                    GetAnonymousString(entry, "dn"),
                                    GetAnonymousInt(entry, "responsible_type")));
                            }
                        }
                    }

                    return Task.FromResult((QueryResponseType)(object)new object());
                }

                if (query == OwnerQueries.deleteSpecificOwnerResponsibles)
                {
                    ++DeleteSpecificOwnerResponsiblesCalls;
                    int ownerId = GetAnonymousInt(variables, "ownerId");
                    object? objects = GetAnonymousValue(variables, "objects");
                    if (objects is IEnumerable enumerable)
                    {
                        foreach (object entry in enumerable)
                        {
                            object? dnObject = GetAnonymousValue(entry, "dn");
                            object? typeObject = GetAnonymousValue(entry, "responsible_type");
                            Deleted.Add((
                                ownerId,
                                GetAnonymousString(dnObject, "_eq"),
                                GetAnonymousInt(typeObject, "_eq")));
                        }
                    }

                    return Task.FromResult((QueryResponseType)(object)new object());
                }

                throw new NotImplementedException($"Query not implemented in responsibles test api: {query}");
            }

            private static int GetAnonymousInt(object? variables, string propertyName)
            {
                object? value = GetAnonymousValue(variables, propertyName);
                return value is int intValue ? intValue : 0;
            }

            private static string GetAnonymousString(object? variables, string propertyName)
            {
                object? value = GetAnonymousValue(variables, propertyName);
                return value as string ?? "";
            }

            private static object? GetAnonymousValue(object? variables, string propertyName)
            {
                if (variables == null)
                {
                    return null;
                }

                PropertyInfo? property = variables.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
                return property?.GetValue(variables);
            }
        }

        private sealed class AppDataImportSaveAppApiConn : SimulatedApiConnection
        {
            public int NewOwnerCalls { get; private set; }
            public int UpdateOwnerCalls { get; private set; }
            public int UpdateChangelogOwnerCalls { get; private set; }
            public List<char> ChangelogActions { get; } = [];
            public Dictionary<(int ownerId, string importSource), List<ModellingAppServer>> AppServersByOwner { get; } = [];
            public DateTime? LastNewOwnerDecommDate { get; private set; }
            public DateTime? LastUpdateOwnerDecommDate { get; private set; }

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null)
            {
                if (query == OwnerQueries.newOwner)
                {
                    ++NewOwnerCalls;
                    LastNewOwnerDecommDate = GetAnonymousDateTime(variables, "decommDate");
                    return Task.FromResult((QueryResponseType)(object)new ReturnIdWrapper
                    {
                        ReturnIds = [new ReturnId { NewId = 1 }]
                    });
                }

                if (query == OwnerQueries.updateOwner)
                {
                    ++UpdateOwnerCalls;
                    LastUpdateOwnerDecommDate = GetAnonymousDateTime(variables, "decommDate");
                    return Task.FromResult((QueryResponseType)(object)new ReturnIdWrapper
                    {
                        ReturnIds = [new ReturnId { NewId = 1 }]
                    });
                }

                if (query == OwnerQueries.updateChangelogOwner)
                {
                    ++UpdateChangelogOwnerCalls;
                    char? action = GetAnonymousChar(variables, "change_action");
                    if (action.HasValue)
                    {
                        ChangelogActions.Add(action.Value);
                    }

                    return Task.FromResult((QueryResponseType)(object)new object());
                }

                if (query == ModellingQueries.getAppServersBySource)
                {
                    int ownerId = GetAnonymousInt(variables, "appId");
                    string importSource = GetAnonymousString(variables, "importSource");
                    List<ModellingAppServer> appServers = AppServersByOwner.TryGetValue((ownerId, importSource), out List<ModellingAppServer>? value)
                        ? value
                        : [];
                    return Task.FromResult((QueryResponseType)(object)appServers);
                }

                if (query == ImportQueries.addImportForOwner)
                {
                    return Task.FromResult((QueryResponseType)(object)new InsertImportControl
                    {
                        Returning = new List<ImportControl>
                        {
                            new ImportControl { ControlId = 123 }
                        }
                    });
                }

                if (query == MonitorQueries.addDataImportLogEntry)
                {
                    return Task.FromResult((QueryResponseType)(object)new ReturnIdWrapper
                    {
                        ReturnIds = [new ReturnId { NewId = 1 }]
                    });
                }

                throw new NotImplementedException($"Query not implemented in save-app test api: {query}");
            }

            private static char? GetAnonymousChar(object? variables, string propertyName)
            {
                if (variables == null)
                {
                    return null;
                }

                PropertyInfo? property = variables.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
                if (property == null)
                {
                    return null;
                }

                object? value = property.GetValue(variables);
                return value is char charValue ? charValue : null;
            }

            private static int GetAnonymousInt(object? variables, string propertyName)
            {
                if (variables == null)
                {
                    return 0;
                }

                PropertyInfo? property = variables.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
                if (property == null)
                {
                    return 0;
                }

                object? value = property.GetValue(variables);
                return value is int intValue ? intValue : 0;
            }

            private static DateTime? GetAnonymousDateTime(object? variables, string propertyName)
            {
                if (variables == null)
                {
                    return null;
                }

                PropertyInfo? property = variables.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
                if (property == null)
                {
                    return null;
                }

                object? value = property.GetValue(variables);
                return value as DateTime? ?? (value is DateTime dateTime ? dateTime : null);
            }

            private static string GetAnonymousString(object? variables, string propertyName)
            {
                if (variables == null)
                {
                    return "";
                }

                PropertyInfo? property = variables.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
                if (property == null)
                {
                    return "";
                }

                object? value = property.GetValue(variables);
                return value as string ?? "";
            }
        }

        private sealed class TestAppDataImport(ApiConnection apiConnection, GlobalConfig globalConfig) : AppDataImport(apiConnection, globalConfig)
        {
            public List<FwoOwner> CheckedOwners { get; } = [];

            protected override Task CheckActiveRulesSync(FwoOwner owner)
            {
                CheckedOwners.Add(new FwoOwner(owner) { OwnerLifeCycleStateId = owner.OwnerLifeCycleStateId });
                return Task.CompletedTask;
            }
        }

        private sealed class ResolverTestAppDataImport : AppDataImport
        {
            private readonly Dictionary<string, string?> resolutions;
            private readonly Dictionary<string, UiUser> uiUsersByDn;
            private readonly Dictionary<string, List<string>> groupMembersByDn;
            private readonly Dictionary<string, string?> groupResolutions;
            public List<string> ResolvedIdentifiers { get; } = [];
            public List<string> ResolvedGroupIdentifiers { get; } = [];

            public ResolverTestAppDataImport(
                Dictionary<string, string?> resolutions,
                Dictionary<string, UiUser>? uiUsersByDn = null,
                Dictionary<string, List<string>>? groupMembersByDn = null,
                Dictionary<string, string?>? groupResolutions = null)
                : base(new SimulatedApiConnection(), new GlobalConfig())
            {
                this.resolutions = resolutions;
                this.uiUsersByDn = uiUsersByDn ?? new(StringComparer.OrdinalIgnoreCase);
                this.groupMembersByDn = groupMembersByDn ?? new(StringComparer.OrdinalIgnoreCase);
                this.groupResolutions = groupResolutions ?? new(StringComparer.OrdinalIgnoreCase);
            }

            protected override Task<string?> ResolveImportedUserIdentifierToDn(string userIdentifier)
            {
                ResolvedIdentifiers.Add(userIdentifier);
                return Task.FromResult(resolutions.TryGetValue(userIdentifier, out string? resolvedDn) ? resolvedDn : null);
            }

            protected override Task<UiUser?> ResolveImportedUiUser(string responsibleDn)
            {
                return Task.FromResult(uiUsersByDn.TryGetValue(responsibleDn, out UiUser? uiUser) ? uiUser : null);
            }

            protected override Task<List<string>> ResolveImportedGroupMembers(Ldap ldap, string groupDn)
            {
                return Task.FromResult(groupMembersByDn.TryGetValue(groupDn, out List<string>? members) ? members : []);
            }

            protected override Task<string?> ResolveImportedGroupIdentifierToDn(string groupIdentifier)
            {
                ResolvedGroupIdentifiers.Add(groupIdentifier);
                return Task.FromResult(groupResolutions.TryGetValue(groupIdentifier, out string? resolvedDn) ? resolvedDn : null);
            }
        }

        private sealed class RoleRemovalTrackingImport : AppDataImport
        {
            public List<(string dn, string role)> RemovedRoleAssignments { get; } = [];

            public RoleRemovalTrackingImport(ApiConnection apiConnection)
                : base(apiConnection, new GlobalConfig())
            {
            }

            protected override Task RemoveRoleFromDn(string dn, string role)
            {
                RemovedRoleAssignments.Add((dn, role));
                return Task.CompletedTask;
            }
        }
    }
}
