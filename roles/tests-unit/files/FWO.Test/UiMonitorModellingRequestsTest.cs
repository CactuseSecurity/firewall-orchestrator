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

        private static T GetPrivateProperty<T>(MonitorModellingRequests component, string propertyName)
        {
            PropertyInfo? property = typeof(MonitorModellingRequests).GetProperty(propertyName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (property == null)
            {
                throw new MissingMemberException(typeof(MonitorModellingRequests).FullName, propertyName);
            }
            return (T)property.GetValue(component)!;
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

        private static object CreateRequestStatusRow(FwoOwner owner, long ticketId, string status, int stateId = 0)
        {
            Type rowType = GetRequestStatusType();
            object row = Activator.CreateInstance(rowType) ?? throw new InvalidOperationException("Could not create owner request status row.");
            rowType.GetProperty("Owner")?.SetValue(row, owner);
            rowType.GetProperty("TicketId")?.SetValue(row, ticketId);
            rowType.GetProperty("Status")?.SetValue(row, status);
            rowType.GetProperty("StateId")?.SetValue(row, stateId);
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
                Assert.That(GetPrivateProperty<bool>(component, "ResetMode"), Is.True);
                Assert.That(GetPrivateField<object>(component, "ResetRow"), Is.SameAs(row));
            });
        }

        [Test]
        public void RequestImplementationStatusChange_OffersConfiguredStates()
        {
            MonitorModellingRequests component = new();
            SetInjectedUserConfig(component, new MonitorModellingRequestsUserConfig
            {
                ModIntegrationStates = ModIntegrationStateConfig.ToConfigValue(
                [
                    new() { Name = " Done " },
                    new() { Name = "Retry" },
                    new() { Name = "Done" }
                ])
            });
            object row = CreateRequestStatusRow(new FwoOwner { Id = 7, Name = "App" }, 77, "Requested");

            GetPrivateMethod("RequestImplementationStatusChange").Invoke(component, [row]);

            Assert.Multiple(() =>
            {
                Assert.That(GetPrivateProperty<bool>(component, "ChangeImplementationStatusMode"), Is.True);
                Assert.That(GetPrivateField<object>(component, "ChangeStatusRow"), Is.SameAs(row));
                Assert.That(GetPrivateField<List<string>>(component, "ConfiguredImplementationStatuses"), Is.EqualTo(new List<string> { "Done", "Retry" }));
                Assert.That(GetPrivateField<string>(component, "SelectedImplementationStatus"), Is.EqualTo("Done"));
            });
        }

        [Test]
        public void RequestTicketStateChange_StoresSelectedRowAndCurrentState()
        {
            MonitorModellingRequests component = new();
            object row = CreateRequestStatusRow(new FwoOwner { Id = 7, Name = "App" }, 77, "Requested", 5);

            GetPrivateMethod("RequestTicketStateChange").Invoke(component, [row]);

            Assert.Multiple(() =>
            {
                Assert.That(GetPrivateProperty<bool>(component, "ChangeTicketStateMode"), Is.True);
                Assert.That(GetPrivateField<object>(component, "ChangeTicketStateRow"), Is.SameAs(row));
                Assert.That(GetPrivateField<int>(component, "SelectedTicketStateId"), Is.EqualTo(5));
            });
        }

        [Test]
        public void TicketStateName_ResolvesConfiguredStateName()
        {
            MonitorModellingRequests component = new();
            SetPrivateField(component, "States", new FWO.Services.Workflow.WfStateDict { Name = { [5] = "Approved" } });

            string stateName = (string)GetPrivateMethod("TicketStateName").Invoke(component, [5])!;

            Assert.That(stateName, Is.EqualTo("Approved"));
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
                Assert.That(GetRowProperty<string>(rows[1]!, "Status"), Is.EqualTo("U9026"));
                Assert.That(GetRowProperty<string>(rows[1]!, "StateName"), Is.EqualTo("Open"));
            });
        }

        [Test]
        public async Task LoadRows_ShowsNeverRequested_WhenOwnerHasUnmarkedConnectionsAndNoTicket()
        {
            MonitorModellingRequests component = new();
            MonitorModellingRequestsApiConn apiConn = new()
            {
                Owners =
                [
                    new FwoOwner { Id = 1, Name = "Alpha", ConnectionCount = new() { Aggregate = new() { Count = 1 } } }
                ]
            };
            apiConn.Connections.Add(new ModellingConnection { Id = 10 });
            SetInjectedApiConnection(component, apiConn);
            SetInjectedUserConfig(component, new MonitorModellingRequestsUserConfig());
            SetPrivateField(component, "States", new FWO.Services.Workflow.WfStateDict());

            Task task = (Task)GetPrivateMethod("LoadRows").Invoke(component, null)!;
            await task;

            IList rows = GetPrivateField<IList>(component, "Rows");
            Assert.That(GetRowProperty<string>(rows[0]!, "Status"), Is.EqualTo("never requested"));
        }

        [Test]
        public async Task LoadRows_ShowsChangesNotRequested_WhenUnmarkedConnectionsExistAfterTicket()
        {
            MonitorModellingRequests component = new();
            MonitorModellingRequestsApiConn apiConn = new()
            {
                Owners =
                [
                    new FwoOwner { Id = 1, Name = "Alpha", ConnectionCount = new() { Aggregate = new() { Count = 1 } } }
                ],
                LatestTickets =
                [
                    new OwnerTicket
                    {
                        Owner = new FwoOwner { Id = 1, Name = "Alpha" },
                        Ticket = new WfTicket { Id = 200, StateId = 5 }
                    }
                ]
            };
            apiConn.Connections.Add(new ModellingConnection { Id = 10 });
            SetInjectedApiConnection(component, apiConn);
            SetInjectedUserConfig(component, new MonitorModellingRequestsUserConfig());
            SetPrivateField(component, "States", new FWO.Services.Workflow.WfStateDict { Name = { [5] = "Open" } });

            Task task = (Task)GetPrivateMethod("LoadRows").Invoke(component, null)!;
            await task;

            IList rows = GetPrivateField<IList>(component, "Rows");
            Assert.That(GetRowProperty<string>(rows[0]!, "Status"), Is.EqualTo("Changes not requested"));
        }

        [Test]
        public async Task LoadRows_ShowsAllImplemented_WhenAllObjectsAreImplemented()
        {
            MonitorModellingRequests component = new();
            MonitorModellingRequestsApiConn apiConn = new()
            {
                Owners =
                [
                    new FwoOwner { Id = 1, Name = "Alpha", ConnectionCount = new() { Aggregate = new() { Count = 1 } } }
                ],
                LatestTickets =
                [
                    new OwnerTicket
                    {
                        Owner = new FwoOwner { Id = 1, Name = "Alpha" },
                        Ticket = new WfTicket { Id = 200, StateId = 5 }
                    }
                ]
            };
            ModellingConnection connection = new() { Id = 10 };
            connection.AddProperty(ModIntegrationStateConfig.DefaultMarker, "Implemented | 2026-05-18T10:00:00Z");
            apiConn.Connections.Add(connection);
            SetInjectedApiConnection(component, apiConn);
            SetInjectedUserConfig(component, new MonitorModellingRequestsUserConfig
            {
                ModIntegrationStates = ModIntegrationStateConfig.ToConfigValue(
                [
                    new() { Name = "Implemented", IncludeIntoRequest = true, MonitorStatus = ModIntegrationStateStatus.Implemented }
                ])
            });
            SetPrivateField(component, "States", new FWO.Services.Workflow.WfStateDict { Name = { [5] = "Open" } });

            Task task = (Task)GetPrivateMethod("LoadRows").Invoke(component, null)!;
            await task;

            IList rows = GetPrivateField<IList>(component, "Rows");
            Assert.That(GetRowProperty<string>(rows[0]!, "Status"), Is.EqualTo("All implemented"));
        }

        [Test]
        public async Task LoadRows_ShowsAllImplemented_BeforeRequestableChanges()
        {
            MonitorModellingRequests component = new();
            MonitorModellingRequestsApiConn apiConn = new()
            {
                Owners =
                [
                    new FwoOwner { Id = 1, Name = "Alpha", ConnectionCount = new() { Aggregate = new() { Count = 1 } } }
                ],
                LatestTickets =
                [
                    new OwnerTicket
                    {
                        Owner = new FwoOwner { Id = 1, Name = "Alpha" },
                        Ticket = new WfTicket { Id = 200, StateId = 5, CreationDate = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc) }
                    }
                ]
            };
            ModellingConnection connection = new() { Id = 10 };
            connection.AddProperty(ModIntegrationStateConfig.DefaultMarker, "Implemented | 2026-05-02T10:00:00Z");
            apiConn.Connections.Add(connection);
            apiConn.History.Add(new ModellingHistoryEntry
            {
                ObjectType = (int)ModellingTypes.ModObjectType.Connection,
                ObjectId = 10,
                ChangeTime = new DateTime(2026, 5, 3, 0, 0, 0, DateTimeKind.Utc)
            });
            SetInjectedApiConnection(component, apiConn);
            SetInjectedUserConfig(component, new MonitorModellingRequestsUserConfig
            {
                ModIntegrationStates = ModIntegrationStateConfig.ToConfigValue(
                [
                    new() { Name = "Implemented", IncludeIntoRequest = true, MonitorStatus = ModIntegrationStateStatus.Implemented }
                ])
            });
            SetPrivateField(component, "States", new FWO.Services.Workflow.WfStateDict { Name = { [5] = "Open" } });

            Task task = (Task)GetPrivateMethod("LoadRows").Invoke(component, null)!;
            await task;

            IList rows = GetPrivateField<IList>(component, "Rows");
            Assert.That(GetRowProperty<string>(rows[0]!, "Status"), Is.EqualTo("All implemented"));
        }

        [Test]
        public async Task LoadRows_DoesNotTreatIncludedNamedStateAsUnrequestedChange()
        {
            MonitorModellingRequests component = new();
            MonitorModellingRequestsApiConn apiConn = new()
            {
                Owners =
                [
                    new FwoOwner { Id = 1, Name = "Alpha", ConnectionCount = new() { Aggregate = new() { Count = 1 } } }
                ],
                LatestTickets =
                [
                    new OwnerTicket
                    {
                        Owner = new FwoOwner { Id = 1, Name = "Alpha" },
                        Ticket = new WfTicket { Id = 200, StateId = 5 }
                    }
                ]
            };
            ModellingConnection connection = new() { Id = 10 };
            connection.AddProperty(ModIntegrationStateConfig.DefaultMarker, "Retry | 2026-05-18T10:00:00Z");
            apiConn.Connections.Add(connection);
            SetInjectedApiConnection(component, apiConn);
            SetInjectedUserConfig(component, new MonitorModellingRequestsUserConfig
            {
                ModIntegrationStates = ModIntegrationStateConfig.ToConfigValue([new() { Name = "Retry", IncludeIntoRequest = true }])
            });
            SetPrivateField(component, "States", new FWO.Services.Workflow.WfStateDict { Name = { [5] = "Open" } });

            Task task = (Task)GetPrivateMethod("LoadRows").Invoke(component, null)!;
            await task;

            IList rows = GetPrivateField<IList>(component, "Rows");
            Assert.That(GetRowProperty<string>(rows[0]!, "Status"), Is.EqualTo("U9026"));
        }

        [Test]
        public async Task LoadRows_IgnoresUnmarkedGroups()
        {
            MonitorModellingRequests component = new();
            MonitorModellingRequestsApiConn apiConn = new()
            {
                Owners =
                [
                    new FwoOwner { Id = 1, Name = "Alpha", ConnectionCount = new() { Aggregate = new() { Count = 1 } } }
                ],
                LatestTickets =
                [
                    new OwnerTicket
                    {
                        Owner = new FwoOwner { Id = 1, Name = "Alpha" },
                        Ticket = new WfTicket { Id = 200, StateId = 5 }
                    }
                ]
            };
            ModellingConnection connection = new() { Id = 10 };
            connection.AddProperty(ModIntegrationStateConfig.DefaultMarker, "Implemented | 2026-05-18T10:00:00Z");
            apiConn.Connections.Add(connection);
            apiConn.NwGroups.Add(new ModellingAppRole { Id = 20, Comment = "manual note" });
            apiConn.ServiceGroups.Add(new ModellingServiceGroup { Id = 30, Comment = "manual note" });
            SetInjectedApiConnection(component, apiConn);
            SetInjectedUserConfig(component, new MonitorModellingRequestsUserConfig
            {
                ModIntegrationStates = ModIntegrationStateConfig.ToConfigValue(
                [
                    new() { Name = "Implemented", MonitorStatus = ModIntegrationStateStatus.Implemented }
                ])
            });
            SetPrivateField(component, "States", new FWO.Services.Workflow.WfStateDict { Name = { [5] = "Open" } });

            Task task = (Task)GetPrivateMethod("LoadRows").Invoke(component, null)!;
            await task;

            IList rows = GetPrivateField<IList>(component, "Rows");
            Assert.That(GetRowProperty<string>(rows[0]!, "Status"), Is.EqualTo("All implemented"));
        }

        [Test]
        public async Task LoadRows_DoesNotShowAllImplemented_WhenConnectionsAreUnmarked()
        {
            MonitorModellingRequests component = new();
            MonitorModellingRequestsApiConn apiConn = new()
            {
                Owners =
                [
                    new FwoOwner { Id = 1, Name = "Alpha", ConnectionCount = new() { Aggregate = new() { Count = 1 } } }
                ],
                LatestTickets =
                [
                    new OwnerTicket
                    {
                        Owner = new FwoOwner { Id = 1, Name = "Alpha" },
                        Ticket = new WfTicket { Id = 200, StateId = 5 }
                    }
                ]
            };
            apiConn.Connections.Add(new ModellingConnection
            {
                Id = 10,
                RequestedOnFw = true,
                SourceAppRoles =
                [
                    new() { Content = new() { Id = 20, IdString = "AR", Comment = "ImplementationState: Implemented | 2026-05-18T10:00:00Z" } }
                ]
            });
            SetInjectedApiConnection(component, apiConn);
            SetInjectedUserConfig(component, new MonitorModellingRequestsUserConfig
            {
                ModIntegrationStates = ModIntegrationStateConfig.ToConfigValue(
                [
                    new() { Name = "Implemented", MonitorStatus = ModIntegrationStateStatus.Implemented }
                ])
            });
            SetPrivateField(component, "States", new FWO.Services.Workflow.WfStateDict { Name = { [5] = "Open" } });

            Task task = (Task)GetPrivateMethod("LoadRows").Invoke(component, null)!;
            await task;

            IList rows = GetPrivateField<IList>(component, "Rows");
            Assert.That(GetRowProperty<string>(rows[0]!, "Status"), Is.EqualTo("U9026"));
        }

        [Test]
        public async Task LoadRows_UsesRequestBuilderForUnmarkedConnectionsWhenMarkersExist()
        {
            MonitorModellingRequests component = new();
            MonitorModellingRequestsApiConn apiConn = new()
            {
                Owners =
                [
                    new FwoOwner { Id = 1, Name = "Alpha", ConnectionCount = new() { Aggregate = new() { Count = 2 } } }
                ],
                LatestTickets =
                [
                    new OwnerTicket
                    {
                        Owner = new FwoOwner { Id = 1, Name = "Alpha" },
                        Ticket = new WfTicket { Id = 200, StateId = 5 }
                    }
                ]
            };
            ModellingConnection implementedConnection = new() { Id = 10 };
            implementedConnection.AddProperty(ModIntegrationStateConfig.DefaultMarker, "Implemented | 2026-05-18T10:00:00Z");
            apiConn.Connections.Add(implementedConnection);
            apiConn.Connections.Add(new ModellingConnection { Id = 11 });
            SetInjectedApiConnection(component, apiConn);
            SetInjectedUserConfig(component, new MonitorModellingRequestsUserConfig
            {
                ModIntegrationStates = ModIntegrationStateConfig.ToConfigValue(
                [
                    new() { Name = "Implemented", MonitorStatus = ModIntegrationStateStatus.Implemented }
                ])
            });
            SetPrivateField(component, "States", new FWO.Services.Workflow.WfStateDict { Name = { [5] = "Open" } });

            Task task = (Task)GetPrivateMethod("LoadRows").Invoke(component, null)!;
            await task;

            IList rows = GetPrivateField<IList>(component, "Rows");
            Assert.That(GetRowProperty<string>(rows[0]!, "Status"), Is.EqualTo("Changes not requested"));
        }

        [Test]
        public async Task LoadRows_ShowsRequestRunning_WhenConnectionImplementationStateIsIncluded()
        {
            MonitorModellingRequests component = new();
            MonitorModellingRequestsApiConn apiConn = new()
            {
                Owners =
                [
                    new FwoOwner { Id = 1, Name = "Alpha", ConnectionCount = new() { Aggregate = new() { Count = 1 } } }
                ],
                LatestTickets =
                [
                    new OwnerTicket
                    {
                        Owner = new FwoOwner { Id = 1, Name = "Alpha" },
                        Ticket = new WfTicket { Id = 200, StateId = 5 }
                    }
                ]
            };
            ModellingConnection connection = new() { Id = 10 };
            connection.AddProperty(ModIntegrationStateConfig.DefaultMarker, "Requested | 2026-05-18T10:00:00Z");
            apiConn.Connections.Add(connection);
            SetInjectedApiConnection(component, apiConn);
            SetInjectedUserConfig(component, new MonitorModellingRequestsUserConfig());
            SetPrivateField(component, "States", new FWO.Services.Workflow.WfStateDict { Name = { [5] = "Open" } });

            Task task = (Task)GetPrivateMethod("LoadRows").Invoke(component, null)!;
            await task;

            IList rows = GetPrivateField<IList>(component, "Rows");
            Assert.That(GetRowProperty<string>(rows[0]!, "Status"), Is.EqualTo("Request running"));
        }

        [Test]
        public async Task LoadRows_ShowsRejections_WhenAnyObjectIsRejected()
        {
            MonitorModellingRequests component = new();
            MonitorModellingRequestsApiConn apiConn = new()
            {
                Owners =
                [
                    new FwoOwner { Id = 1, Name = "Alpha", ConnectionCount = new() { Aggregate = new() { Count = 1 } } }
                ],
                LatestTickets =
                [
                    new OwnerTicket
                    {
                        Owner = new FwoOwner { Id = 1, Name = "Alpha" },
                        Ticket = new WfTicket { Id = 200, StateId = 5 }
                    }
                ]
            };
            apiConn.ServiceGroups.Add(new ModellingServiceGroup
            {
                Id = 20,
                Comment = "ImplementationState: Rejected | 2026-05-18T10:00:00Z"
            });
            SetInjectedApiConnection(component, apiConn);
            SetInjectedUserConfig(component, new MonitorModellingRequestsUserConfig());
            SetPrivateField(component, "States", new FWO.Services.Workflow.WfStateDict { Name = { [5] = "Open" } });

            Task task = (Task)GetPrivateMethod("LoadRows").Invoke(component, null)!;
            await task;

            IList rows = GetPrivateField<IList>(component, "Rows");
            Assert.That(GetRowProperty<string>(rows[0]!, "Status"), Is.EqualTo("Rejections"));
        }

        [Test]
        public async Task LoadRows_ShowsChangesNotRequested_WhenUnmarkedObjectsExistAfterTicket()
        {
            MonitorModellingRequests component = new();
            MonitorModellingRequestsApiConn apiConn = new()
            {
                Owners =
                [
                    new FwoOwner { Id = 1, Name = "Alpha", ConnectionCount = new() { Aggregate = new() { Count = 1 } } }
                ],
                LatestTickets =
                [
                    new OwnerTicket
                    {
                        Owner = new FwoOwner { Id = 1, Name = "Alpha" },
                        Ticket = new WfTicket { Id = 200, StateId = 5 }
                    }
                ]
            };
            apiConn.Connections.Add(new ModellingConnection { Id = 10 });
            SetInjectedApiConnection(component, apiConn);
            SetInjectedUserConfig(component, new MonitorModellingRequestsUserConfig());
            SetPrivateField(component, "States", new FWO.Services.Workflow.WfStateDict { Name = { [5] = "Open" } });

            Task task = (Task)GetPrivateMethod("LoadRows").Invoke(component, null)!;
            await task;

            IList rows = GetPrivateField<IList>(component, "Rows");
            Assert.That(GetRowProperty<string>(rows[0]!, "Status"), Is.EqualTo("Changes not requested"));
        }

        [Test]
        public async Task LoadRows_ShowsNothingToRequest_WhenUnmarkedConnectionsAreAlreadyRequested()
        {
            MonitorModellingRequests component = new();
            MonitorModellingRequestsApiConn apiConn = new()
            {
                Owners =
                [
                    new FwoOwner { Id = 1, Name = "Alpha", ConnectionCount = new() { Aggregate = new() { Count = 1 } } }
                ],
                LatestTickets =
                [
                    new OwnerTicket
                    {
                        Owner = new FwoOwner { Id = 1, Name = "Alpha" },
                        Ticket = new WfTicket { Id = 200, StateId = 5 }
                    }
                ]
            };
            apiConn.Connections.Add(new ModellingConnection { Id = 10, RequestedOnFw = true });
            SetInjectedApiConnection(component, apiConn);
            SetInjectedUserConfig(component, new MonitorModellingRequestsUserConfig());
            SetPrivateField(component, "States", new FWO.Services.Workflow.WfStateDict { Name = { [5] = "Open" } });

            Task task = (Task)GetPrivateMethod("LoadRows").Invoke(component, null)!;
            await task;

            IList rows = GetPrivateField<IList>(component, "Rows");
            Assert.That(GetRowProperty<string>(rows[0]!, "Status"), Is.EqualTo("U9026"));
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

        [Test]
        public async Task SetTicketImplementationStatus_UpdatesRequestedConnectionsAndGroups()
        {
            MonitorModellingRequests component = new();
            MonitorModellingRequestsApiConn apiConn = new();
            string marker = ModIntegrationStateConfig.DefaultMarker;
            WfReqTask accessTask = new() { Id = 1, TicketId = 77, TaskType = WfTaskType.access.ToString() };
            accessTask.SetAddInfo(AdditionalInfoKeys.ConnId, "100");
            WfReqTask appRoleTask = new() { Id = 2, TicketId = 77, TaskType = WfTaskType.group_modify.ToString() };
            appRoleTask.SetAddInfo(AdditionalInfoKeys.AppRoleId, "200");
            WfReqTask serviceGroupTask = new() { Id = 3, TicketId = 77, TaskType = WfTaskType.group_modify.ToString() };
            serviceGroupTask.SetAddInfo(AdditionalInfoKeys.SvcGrpId, "300");
            WfTicket ticket = new() { Id = 77, Tasks = [accessTask, appRoleTask, serviceGroupTask] };
            ModellingConnection connection = new() { Id = 100 };
            connection.AddProperty("keep", "value");
            apiConn.WorkflowConnectionsById[100] = connection;
            apiConn.AppRoles[200] = new ModellingAppRole
            {
                Id = 200,
                Comment = $"{marker}: Old | 2026-05-01T10:00:00Z{Environment.NewLine}keep"
            };
            apiConn.ServiceGroupsById[300] = new ModellingServiceGroup { Id = 300, Comment = "before" };
            SetInjectedApiConnection(component, apiConn);
            SetInjectedUserConfig(component, new MonitorModellingRequestsUserConfig());

            Task task = (Task)GetPrivateMethod("SetTicketImplementationStatus").Invoke(component, [ticket, "Done"])!;
            await task;

            object connectionUpdate = apiConn.Variables[1];
            object appRoleUpdate = apiConn.Variables[3];
            object serviceGroupUpdate = apiConn.Variables[5];
            Assert.Multiple(() =>
            {
                Assert.That(apiConn.Queries, Is.EqualTo(new List<string>
                {
                    ModellingQueries.getWorkflowConnectionById,
                    ModellingQueries.updateConnectionProperties,
                    ModellingQueries.getAppRoleById,
                    ModellingQueries.updateNwGroupComment,
                    ModellingQueries.getServiceGroupById,
                    ModellingQueries.updateServiceGroupComment
                }));
                Assert.That(GetVariable<int>(connectionUpdate, "id"), Is.EqualTo(100));
                Assert.That(GetVariable<string>(connectionUpdate, "connProp"), Does.Contain("keep"));
                Assert.That(GetVariable<string>(connectionUpdate, "connProp"), Does.Contain($"\"{marker}\":\"Done | "));
                Assert.That(GetVariable<long>(appRoleUpdate, "id"), Is.EqualTo(200));
                Assert.That(GetVariable<string>(appRoleUpdate, "comment"), Does.Contain($"{marker}: Done"));
                Assert.That(GetVariable<string>(appRoleUpdate, "comment"), Does.Contain("keep"));
                Assert.That(GetVariable<int>(serviceGroupUpdate, "id"), Is.EqualTo(300));
                Assert.That(GetVariable<string>(serviceGroupUpdate, "comment"), Does.Contain("before"));
                Assert.That(GetVariable<string>(serviceGroupUpdate, "comment"), Does.Contain($"{marker}: Done"));
            });
        }

        [Test]
        public async Task SetTicketState_UpdatesTicketStateAndReloadsRows()
        {
            MonitorModellingRequests component = new();
            FwoOwner owner = new() { Id = 7, Name = "App" };
            object row = CreateRequestStatusRow(owner, 77, "Requested", 5);
            MonitorModellingRequestsApiConn apiConn = new()
            {
                Ticket = new WfTicket
                {
                    Id = 77,
                    StateId = 5,
                    Deadline = new DateTime(2026, 6, 1),
                    Priority = 3,
                    CompletionDate = new DateTime(2026, 5, 17)
                },
                Owners = [owner],
                LatestTickets =
                [
                    new OwnerTicket
                    {
                        Owner = owner,
                        Ticket = new WfTicket { Id = 77, StateId = 8 }
                    }
                ]
            };
            SetInjectedApiConnection(component, apiConn);
            SetInjectedUserConfig(component, new MonitorModellingRequestsUserConfig());
            SetPrivateField(component, "States", new FWO.Services.Workflow.WfStateDict { Name = { [8] = "Closed" } });
            SetPrivateField(component, "ChangeTicketStateRow", row);
            SetPrivateField(component, "SelectedTicketStateId", 8);

            Task task = (Task)GetPrivateMethod("SetTicketState").Invoke(component, null)!;
            await task;

            object updateVars = apiConn.Variables[1];
            Assert.Multiple(() =>
            {
                Assert.That(apiConn.Queries, Does.Contain(RequestQueries.getTicketById));
                Assert.That(apiConn.Queries, Does.Contain(RequestQueries.updateTicketState));
                Assert.That(GetVariable<long>(updateVars, "id"), Is.EqualTo(77));
                Assert.That(GetVariable<int>(updateVars, "state"), Is.EqualTo(8));
                Assert.That(GetVariable<DateTime?>(updateVars, "closed"), Is.EqualTo(new DateTime(2026, 5, 17)));
                Assert.That(GetVariable<DateTime?>(updateVars, "deadline"), Is.EqualTo(new DateTime(2026, 6, 1)));
                Assert.That(GetVariable<int?>(updateVars, "priority"), Is.EqualTo(3));
                Assert.That(GetPrivateProperty<bool>(component, "ChangeTicketStateMode"), Is.False);
            });
        }
    }

    internal sealed class MonitorModellingRequestsUserConfig : SimulatedUserConfig
    {
        public MonitorModellingRequestsUserConfig()
        {
            ModIntegrationStateMarker = ModIntegrationStateConfig.DefaultMarker;
            ModIntegrationStates = ModIntegrationStateConfig.ToConfigValue(
            [
                new() { Name = "Requested", IncludeIntoRequest = true, MonitorStatus = ModIntegrationStateStatus.RequestRunning },
                new() { Name = "Implemented", MonitorStatus = ModIntegrationStateStatus.Implemented },
                new() { Name = "Rejected", MonitorStatus = ModIntegrationStateStatus.Rejected }
            ]);
        }

        public override string GetText(string key)
        {
            return key switch
            {
                "all_implemented" => "All implemented",
                "changes_not_requested" => "Changes not requested",
                "never_requested" => "never requested",
                "request_running" => "Request running",
                "rejections" => "Rejections",
                _ => key
            };
        }
    }

    internal sealed class MonitorModellingRequestsApiConn : SimulatedApiConnection
    {
        public List<string> Queries { get; } = [];
        public List<object> Variables { get; } = [];
        public List<FwoOwner> Owners { get; set; } = [];
        public List<OwnerTicket> LatestTickets { get; set; } = [];
        public List<ModellingConnection> Connections { get; } = [];
        public List<ModellingConnection> WorkflowConnections { get; } = [];
        public Dictionary<int, ModellingConnection> WorkflowConnectionsById { get; } = [];
        public List<ModellingHistoryEntry> History { get; } = [];
        public List<ModellingAppRole> NwGroups { get; } = [];
        public Dictionary<long, ModellingAppRole> AppRoles { get; } = [];
        public List<ModellingServiceGroup> ServiceGroups { get; } = [];
        public Dictionary<int, ModellingServiceGroup> ServiceGroupsById { get; } = [];
        public WfTicket Ticket { get; set; } = new();

        private static T GetVariable<T>(object variables, string name)
        {
            PropertyInfo? property = variables.GetType().GetProperty(name);
            if (property == null)
            {
                throw new MissingMemberException(variables.GetType().FullName, name);
            }
            return (T)property.GetValue(variables)!;
        }

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
            if (typeof(QueryResponseType) == typeof(List<ModellingConnection>) && query == ModellingQueries.getConnectionsResolved)
            {
                return Task.FromResult((QueryResponseType)(object)Connections);
            }
            if (typeof(QueryResponseType) == typeof(List<ModellingAppRole>) && query == ModellingQueries.getDummyAppRole)
            {
                return Task.FromResult((QueryResponseType)(object)new List<ModellingAppRole>());
            }
            if (typeof(QueryResponseType) == typeof(List<ModellingConnection>) && query == ModellingQueries.getWorkflowConnectionsByTicketId)
            {
                return Task.FromResult((QueryResponseType)(object)WorkflowConnections);
            }
            if (typeof(QueryResponseType) == typeof(List<ModellingConnection>) && query == ModellingQueries.getWorkflowConnectionById)
            {
                int id = GetVariable<int>(variables!, "id");
                List<ModellingConnection> result = WorkflowConnectionsById.TryGetValue(id, out ModellingConnection? connection) ? [connection] : [];
                return Task.FromResult((QueryResponseType)(object)result);
            }
            if (typeof(QueryResponseType) == typeof(List<ModellingHistoryEntry>) && query == ModellingQueries.getHistoryForApp)
            {
                return Task.FromResult((QueryResponseType)(object)History);
            }
            if (typeof(QueryResponseType) == typeof(List<ModellingAppRole>) && query == ModellingQueries.getNwGroupsForApp)
            {
                return Task.FromResult((QueryResponseType)(object)NwGroups);
            }
            if (typeof(QueryResponseType) == typeof(ModellingAppRole) && query == ModellingQueries.getAppRoleById)
            {
                long id = GetVariable<long>(variables!, "id");
                return Task.FromResult((QueryResponseType)(object)AppRoles[id]);
            }
            if (typeof(QueryResponseType) == typeof(List<ModellingServiceGroup>) && query == ModellingQueries.getServiceGroupsForApp)
            {
                return Task.FromResult((QueryResponseType)(object)ServiceGroups);
            }
            if (typeof(QueryResponseType) == typeof(ModellingServiceGroup) && query == ModellingQueries.getServiceGroupById)
            {
                int id = GetVariable<int>(variables!, "id");
                return Task.FromResult((QueryResponseType)(object)ServiceGroupsById[id]);
            }
            if (typeof(QueryResponseType) == typeof(WfTicket) && query == RequestQueries.getTicketById)
            {
                return Task.FromResult((QueryResponseType)(object)Ticket);
            }
            if (typeof(QueryResponseType) == typeof(ReturnId))
            {
                return Task.FromResult((QueryResponseType)(object)new ReturnId());
            }

            throw new NotImplementedException();
        }
    }
}
