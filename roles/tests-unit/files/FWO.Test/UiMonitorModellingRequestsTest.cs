using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Data.Workflow;
using FWO.Ui.Pages.Monitoring;
using NUnit.Framework;
using System.Collections;
using System.Reflection;

namespace FWO.Test
{
    [TestFixture]
    internal class UiMonitorModellingRequestsTest
    {
        private static MethodInfo GetPrivateMethod(string name)
        {
            return typeof(MonitorModellingRequests).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMethodException(typeof(MonitorModellingRequests).FullName, name);
        }

        private static MethodInfo GetPrivateStaticMethod(string name)
        {
            return typeof(MonitorModellingRequests).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new MissingMethodException(typeof(MonitorModellingRequests).FullName, name);
        }

        private static void SetPrivateField<T>(MonitorModellingRequests component, string fieldName, T value)
        {
            FieldInfo? field = typeof(MonitorModellingRequests).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
            {
                throw new MissingFieldException(typeof(MonitorModellingRequests).FullName, fieldName);
            }
            field.SetValue(component, value);
        }

        private static T GetPrivateField<T>(MonitorModellingRequests component, string fieldName)
        {
            FieldInfo? field = typeof(MonitorModellingRequests).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
            {
                throw new MissingFieldException(typeof(MonitorModellingRequests).FullName, fieldName);
            }
            return (T)field.GetValue(component)!;
        }

        private static void SetInjectedApiConnection(MonitorModellingRequests component, ApiConnection apiConnection)
        {
            PropertyInfo? prop = typeof(MonitorModellingRequests).GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .FirstOrDefault(property => property.PropertyType == typeof(ApiConnection));
            if (prop == null)
            {
                throw new MissingMemberException(typeof(MonitorModellingRequests).FullName, "apiConnection");
            }
            prop.SetValue(component, apiConnection);
        }

        private static void SetInjectedUserConfig(MonitorModellingRequests component, UserConfig userConfig)
        {
            PropertyInfo? prop = typeof(MonitorModellingRequests).GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .FirstOrDefault(property => property.PropertyType == typeof(UserConfig));
            if (prop == null)
            {
                throw new MissingMemberException(typeof(MonitorModellingRequests).FullName, "userConfig");
            }
            prop.SetValue(component, userConfig);
        }

        private static object CreateRequestStatusRow(FwoOwner owner, long ticketId, string status)
        {
            Type rowType = GetRequestStatusType();
            object row = Activator.CreateInstance(rowType) ?? throw new InvalidOperationException("Could not create owner request status row.");
            rowType.GetProperty("Owner")?.SetValue(row, owner);
            rowType.GetProperty("TicketId")?.SetValue(row, ticketId);
            rowType.GetProperty("Status")?.SetValue(row, status);
            return row;
        }

        private static IList CreateRequestStatusList(params object[] rows)
        {
            Type listType = typeof(List<>).MakeGenericType(GetRequestStatusType());
            IList list = (IList)(Activator.CreateInstance(listType) ?? throw new InvalidOperationException("Could not create row list."));
            foreach (object row in rows)
            {
                list.Add(row);
            }
            return list;
        }

        private static Type GetRequestStatusType()
        {
            return typeof(MonitorModellingRequests).GetNestedType("OwnerRequestStatus", BindingFlags.NonPublic)
                ?? throw new MissingMemberException(typeof(MonitorModellingRequests).FullName, "OwnerRequestStatus");
        }

        private static T GetRowProperty<T>(object row, string propertyName)
        {
            PropertyInfo? property = row.GetType().GetProperty(propertyName);
            if (property == null)
            {
                throw new MissingMemberException(row.GetType().FullName, propertyName);
            }
            return (T)property.GetValue(row)!;
        }

        private static T GetVariable<T>(object variables, string name)
        {
            PropertyInfo? property = variables.GetType().GetProperty(name);
            if (property == null)
            {
                throw new MissingMemberException(variables.GetType().FullName, name);
            }
            return (T)property.GetValue(variables)!;
        }

        [Test]
        public void RemoveGroupRequestState_RemovesStateAndTimestampMarkersOnly()
        {
            string marker = ModIntegrationStateConfig.DefaultMarker;
            string comment = string.Join(Environment.NewLine,
                "business comment",
                $"{marker}: Requested | 2026-05-01T10:00:00Z",
                $"{ModIntegrationStateConfig.TimestampMarker(marker)}: 2026-05-01T10:00:00Z",
                "keep this");

            string cleaned = (string)GetPrivateStaticMethod("RemoveGroupRequestState").Invoke(null, [comment, marker])!;

            Assert.Multiple(() =>
            {
                Assert.That(cleaned, Does.Contain("business comment"));
                Assert.That(cleaned, Does.Contain("keep this"));
                Assert.That(cleaned, Does.Not.Contain(marker + ":"));
                Assert.That(cleaned, Does.Not.Contain(ModIntegrationStateConfig.TimestampMarker(marker)));
            });
        }

        [Test]
        public void RemoveConnectionRequestState_RemovesStateAndTimestampProperties()
        {
            string marker = ModIntegrationStateConfig.DefaultMarker;
            ModellingConnection connection = new();
            connection.AddProperty(marker, "Requested | 2026-05-01T10:00:00Z");
            connection.AddProperty(ModIntegrationStateConfig.TimestampMarker(marker), "2026-05-01T10:00:00Z");
            connection.AddProperty("keep", "value");

            GetPrivateStaticMethod("RemoveConnectionRequestState").Invoke(null, [connection, marker]);

            Assert.Multiple(() =>
            {
                Assert.That(connection.GetStringProperty(marker), Is.Empty);
                Assert.That(connection.GetStringProperty(ModIntegrationStateConfig.TimestampMarker(marker)), Is.Empty);
                Assert.That(connection.GetStringProperty("keep"), Is.EqualTo("value"));
            });
        }

        [Test]
        public void RequestReset_StoresSelectedRowAndEnablesResetMode()
        {
            MonitorModellingRequests component = new();
            object row = CreateRequestStatusRow(new FwoOwner { Id = 7, Name = "App" }, 77, "Requested");

            GetPrivateMethod("RequestReset").Invoke(component, [row]);

            Assert.Multiple(() =>
            {
                Assert.That(GetPrivateField<bool>(component, "ResetMode"), Is.True);
                Assert.That(GetPrivateField<object>(component, "ResetRow"), Is.SameAs(row));
            });
        }

        [Test]
        public void ReplaceResetRowDisplay_ReplacesMatchingRowWithNoRequestDisplay()
        {
            MonitorModellingRequests component = new();
            SetInjectedUserConfig(component, new MonitorModellingRequestsUserConfig());
            FwoOwner owner = new() { Id = 8, Name = "Owner A" };
            object row = CreateRequestStatusRow(owner, 88, "Requested");
            IList rows = CreateRequestStatusList(row, CreateRequestStatusRow(new FwoOwner { Id = 9, Name = "Owner B" }, 99, "Requested"));
            SetPrivateField(component, "Rows", rows);
            SetPrivateField(component, "ResetRow", row);

            GetPrivateMethod("ReplaceResetRowDisplay").Invoke(component, null);

            object updatedRow = rows[0]!;
            object untouchedRow = rows[1]!;
            Assert.Multiple(() =>
            {
                Assert.That(GetRowProperty<FwoOwner>(updatedRow, "Owner"), Is.SameAs(owner));
                Assert.That(GetRowProperty<long>(updatedRow, "TicketId"), Is.EqualTo(0));
                Assert.That(GetRowProperty<string>(updatedRow, "Status"), Is.EqualTo("never requested"));
                Assert.That(GetRowProperty<long>(untouchedRow, "TicketId"), Is.EqualTo(99));
            });
        }

        [Test]
        public async Task LoadRows_CombinesOwnersWithLatestTicketsAndSortsByOwnerName()
        {
            MonitorModellingRequests component = new();
            MonitorModellingRequestsApiConn apiConn = new()
            {
                Owners =
                [
                    new FwoOwner { Id = 2, Name = "Zulu" },
                    new FwoOwner { Id = 1, Name = "Alpha" }
                ],
                LatestTickets =
                [
                    new OwnerTicket
                    {
                        Owner = new FwoOwner { Id = 2, Name = "Zulu" },
                        Ticket = new WfTicket { Id = 200, StateId = 5, CreationDate = new DateTime(2026, 5, 1) }
                    }
                ]
            };
            SetInjectedApiConnection(component, apiConn);
            SetInjectedUserConfig(component, new MonitorModellingRequestsUserConfig());
            SetPrivateField(component, "States", new FWO.Services.Workflow.WfStateDict { Name = { [5] = "Open" } });

            Task task = (Task)GetPrivateMethod("LoadRows").Invoke(component, null)!;
            await task;

            IList rows = GetPrivateField<IList>(component, "Rows");
            Assert.Multiple(() =>
            {
                Assert.That(rows, Has.Count.EqualTo(2));
                Assert.That(GetRowProperty<FwoOwner>(rows[0]!, "Owner").Name, Is.EqualTo("Alpha"));
                Assert.That(GetRowProperty<long>(rows[0]!, "TicketId"), Is.EqualTo(0));
                Assert.That(GetRowProperty<string>(rows[0]!, "Status"), Is.EqualTo("never requested"));
                Assert.That(GetRowProperty<FwoOwner>(rows[1]!, "Owner").Name, Is.EqualTo("Zulu"));
                Assert.That(GetRowProperty<long>(rows[1]!, "TicketId"), Is.EqualTo(200));
                Assert.That(GetRowProperty<string>(rows[1]!, "StateName"), Is.EqualTo("Open"));
            });
        }

        [Test]
        public async Task CleanOwnerModellingRequestState_RemovesMarkersFromConnectionsAndGroups()
        {
            MonitorModellingRequests component = new();
            MonitorModellingRequestsApiConn apiConn = new();
            string marker = ModIntegrationStateConfig.DefaultMarker;
            ModellingConnection connection = new() { Id = 100 };
            connection.AddProperty(marker, "Requested | 2026-05-01T10:00:00Z");
            connection.AddProperty(ModIntegrationStateConfig.TimestampMarker(marker), "2026-05-01T10:00:00Z");
            connection.AddProperty("keep", "value");
            apiConn.Connections.Add(connection);
            apiConn.NwGroups.Add(new ModellingAppRole
            {
                Id = 200,
                Comment = $"before{Environment.NewLine}{marker}: Requested | 2026-05-01T10:00:00Z"
            });
            apiConn.NwGroups.Add(new ModellingAppRole { Id = 201, Comment = "unchanged" });
            apiConn.ServiceGroups.Add(new ModellingServiceGroup
            {
                Id = 300,
                Comment = $"{ModIntegrationStateConfig.TimestampMarker(marker)}: 2026-05-01T10:00:00Z{Environment.NewLine}after"
            });
            SetInjectedApiConnection(component, apiConn);
            SetInjectedUserConfig(component, new MonitorModellingRequestsUserConfig());

            Task task = (Task)GetPrivateMethod("CleanOwnerModellingRequestState").Invoke(component, [42])!;
            await task;

            Assert.Multiple(() =>
            {
                Assert.That(apiConn.Queries, Is.EqualTo(new List<string>
                {
                    ModellingQueries.getConnections,
                    ModellingQueries.resetConnectionRequestState,
                    ModellingQueries.getNwGroupsForApp,
                    ModellingQueries.updateNwGroupComment,
                    ModellingQueries.getServiceGroupsForApp,
                    ModellingQueries.updateServiceGroupComment
                }));
                Assert.That(GetVariable<int>(apiConn.Variables[0], "appId"), Is.EqualTo(42));
                Assert.That(GetVariable<int>(apiConn.Variables[1], "id"), Is.EqualTo(100));
                string connProp = GetVariable<string>(apiConn.Variables[1], "connProp");
                Assert.That(connProp, Does.Contain("keep"));
                Assert.That(connProp, Does.Not.Contain(marker));
                Assert.That(GetVariable<long>(apiConn.Variables[3], "id"), Is.EqualTo(200));
                Assert.That(GetVariable<string>(apiConn.Variables[3], "comment"), Is.EqualTo("before"));
                Assert.That(GetVariable<int>(apiConn.Variables[5], "id"), Is.EqualTo(300));
                Assert.That(GetVariable<string>(apiConn.Variables[5], "comment"), Is.EqualTo("after"));
            });
        }
    }

    internal sealed class MonitorModellingRequestsUserConfig : SimulatedUserConfig
    {
        public MonitorModellingRequestsUserConfig()
        {
            ModIntegrationStateMarker = ModIntegrationStateConfig.DefaultMarker;
        }

        public override string GetText(string key)
        {
            return key == "never_requested" ? "never requested" : key;
        }
    }

    internal sealed class MonitorModellingRequestsApiConn : SimulatedApiConnection
    {
        public List<string> Queries { get; } = [];
        public List<object> Variables { get; } = [];
        public List<FwoOwner> Owners { get; set; } = [];
        public List<OwnerTicket> LatestTickets { get; set; } = [];
        public List<ModellingConnection> Connections { get; } = [];
        public List<ModellingAppRole> NwGroups { get; } = [];
        public List<ModellingServiceGroup> ServiceGroups { get; } = [];

        public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null)
        {
            Queries.Add(query);
            if (variables != null)
            {
                Variables.Add(variables);
            }

            if (typeof(QueryResponseType) == typeof(List<FwoOwner>) && query == OwnerQueries.getOwnersWithConn)
            {
                return Task.FromResult((QueryResponseType)(object)Owners);
            }
            if (typeof(QueryResponseType) == typeof(List<OwnerTicket>) && query == MonitorQueries.getLatestOwnerTickets)
            {
                return Task.FromResult((QueryResponseType)(object)LatestTickets);
            }
            if (typeof(QueryResponseType) == typeof(List<ModellingConnection>) && query == ModellingQueries.getConnections)
            {
                return Task.FromResult((QueryResponseType)(object)Connections);
            }
            if (typeof(QueryResponseType) == typeof(List<ModellingAppRole>) && query == ModellingQueries.getNwGroupsForApp)
            {
                return Task.FromResult((QueryResponseType)(object)NwGroups);
            }
            if (typeof(QueryResponseType) == typeof(List<ModellingServiceGroup>) && query == ModellingQueries.getServiceGroupsForApp)
            {
                return Task.FromResult((QueryResponseType)(object)ServiceGroups);
            }
            if (typeof(QueryResponseType) == typeof(ReturnId))
            {
                return Task.FromResult((QueryResponseType)(object)new ReturnId());
            }

            throw new NotImplementedException();
        }
    }
}
