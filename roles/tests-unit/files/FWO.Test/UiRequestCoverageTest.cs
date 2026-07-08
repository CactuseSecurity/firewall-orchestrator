using Bunit;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Workflow;
using FWO.Middleware.Client;
using FWO.Services;
using FWO.Services.Workflow;
using FWO.Ui.Data;
using FWO.Ui.Pages.Request;
using FWO.Ui.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Threading.Tasks;

namespace FWO.Test
{
    [TestFixture]
    internal class UiRequestCoverageTest
    {
        private static readonly int[] kOwnerOptionIds = [-2, -1, 1, 2];
        private static readonly int[] kDeviceOptionIds = [11, 12, 0, -1];
        private static readonly int[] kAllDeviceSelectionIds = [WfReqTaskBase.kAllDevicesId];
        private static readonly int[] kSingleDeviceSelectionIds = [1];
        private static readonly int[] kTwoDeviceSelectionIds = [1, 2];
        private static readonly long[] kRemovedElementIds = [1L, 2L, 3L];
        private static readonly int[] kDeviceListIds = [101, 102];

        private static void SetMember(object instance, string memberName, object? value)
        {
            Type type = instance.GetType();
            PropertyInfo? property = type.GetProperty(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (property != null)
            {
                property.SetValue(instance, value);
                return;
            }

            FieldInfo? field = type.GetField(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(instance, value);
                return;
            }

            throw new MissingFieldException(type.FullName, memberName);
        }

        private static T GetMember<T>(object instance, string memberName)
        {
            Type type = instance.GetType();
            PropertyInfo? property = type.GetProperty(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (property != null)
            {
                return (T)property.GetValue(instance)!;
            }

            FieldInfo? field = type.GetField(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                return (T)field.GetValue(instance)!;
            }

            throw new MissingFieldException(type.FullName, memberName);
        }

        private static MethodInfo GetPrivateMethod(Type type, string methodName)
        {
            return type.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
                ?? throw new MissingMethodException(type.FullName, methodName);
        }

        private static async Task InvokePrivateTask(object instance, string methodName, params object[] args)
        {
            Task task = (Task)GetPrivateMethod(instance.GetType(), methodName).Invoke(instance, args)!;
            await task;
        }

        private static async Task InvokePrivateTask(Type type, object instance, string methodName, params object[] args)
        {
            Task task = (Task)GetPrivateMethod(type, methodName).Invoke(instance, args)!;
            await task;
        }

        private static async Task<T> InvokePrivateTaskResult<T>(object instance, string methodName, params object[] args)
        {
            Task<T> task = (Task<T>)GetPrivateMethod(instance.GetType(), methodName).Invoke(instance, args)!;
            return await task;
        }

        private static bool InvokePrivateBool(object instance, string methodName, params object[] args)
        {
            return (bool)GetPrivateMethod(instance.GetType(), methodName).Invoke(instance, args)!;
        }

        private static ClaimsPrincipal CreatePrincipal(params string[] roles)
        {
            return new ClaimsPrincipal(new ClaimsIdentity(
                roles.Select(role => new Claim(ClaimTypes.Role, role)),
                "Test",
                ClaimTypes.Name,
                ClaimTypes.Role));
        }

        private static RequestCoverageUserConfig CreateUserConfig(params string[] roles)
        {
            RequestCoverageUserConfig userConfig = new();
            userConfig.User.Roles = [.. roles];
            userConfig.User.DbId = 77;
            userConfig.User.Dn = "cn=current";
            return userConfig;
        }

        private static WfHandler CreateHandler(ApiConnection apiConnection, RequestCoverageUserConfig userConfig, WorkflowPhases phase = WorkflowPhases.request)
        {
            return new WfHandler(
                DefaultInit.DoNothing,
                userConfig,
                CreatePrincipal(Roles.Requester),
                apiConnection,
                new MiddlewareClient("http://localhost/"),
                phase,
                null);
        }

        private static StateMatrix CreateMatrix(int lowestInputState = 1, int lowestStartedState = 2, int lowestEndState = 10)
        {
            return new StateMatrix
            {
                LowestInputState = lowestInputState,
                LowestStartedState = lowestStartedState,
                LowestEndState = lowestEndState,
                PhaseActive =
                {
                    [WorkflowPhases.request] = true,
                    [WorkflowPhases.approval] = true,
                    [WorkflowPhases.planning] = true,
                    [WorkflowPhases.implementation] = true,
                    [WorkflowPhases.review] = true
                }
            };
        }

        private static void SetMatrix(WfHandler handler, string taskType, StateMatrix matrix)
        {
            FieldInfo? field = typeof(WfHandler).GetField("stateMatrixDict", BindingFlags.NonPublic | BindingFlags.Instance);
            StateMatrixDict dict = (StateMatrixDict)(field?.GetValue(handler) ?? new StateMatrixDict());
            dict.Matrices[taskType] = matrix;
        }

        [Test]
        public async Task RequestTicketsOverview_DisablesAccessForUnauthorizedUsers()
        {
            await using BunitContext context = new();
            RequestTicketsOverview component = new();
            SetMember(component, "userConfig", CreateUserConfig());
            SetMember(component, "apiConnection", new ThrowingApiConnection());
            SetMember(component, "middlewareClient", new MiddlewareClient("http://localhost/"));

            await InvokePrivateTask(typeof(RequestTicketsOverview), component, "OnInitializedAsync");

            Assert.Multiple(() =>
            {
                Assert.That(GetMember<bool>(component, "accessAllowed"), Is.False);
                Assert.That(GetMember<bool>(component, "InitComplete"), Is.True);
            });
        }

        [Test]
        public async Task RequestTicketsOverview_ResetFiltersToRequestersOwnTickets()
        {
            await using BunitContext context = new();
            RequestCoverageUserConfig userConfig = CreateUserConfig(Roles.Requester);
            WfTicket ownTicket = new()
            {
                Id = 1,
                Requester = new UiUser { DbId = userConfig.User.DbId, Name = "Current" }
            };
            WfTicket foreignTicket = new()
            {
                Id = 2,
                Requester = new UiUser { DbId = 12, Name = "Other" }
            };
            WfHandler handler = CreateHandler(new ThrowingApiConnection(), userConfig, WorkflowPhases.request);
            handler.TicketList = [ownTicket, foreignTicket];

            RequestTicketsOverview component = new();
            SetMember(component, "userConfig", userConfig);
            SetMember(component, "wfHandler", handler);

            await InvokePrivateTask(typeof(RequestTicketsOverview), component, "Reset");

            Assert.Multiple(() =>
            {
                Assert.That(handler.ReadOnlyMode, Is.True);
                Assert.That(handler.TicketList, Has.Count.EqualTo(1));
                Assert.That(handler.TicketList.Single().Id, Is.EqualTo(1));
            });
        }

        [Test]
        public async Task RequestTickets_ResetSwitchesReadOnlyModeForRequester()
        {
            await using BunitContext context = new();
            RequestCoverageUserConfig userConfig = CreateUserConfig(Roles.Requester);
            WfHandler handler = CreateHandler(new ThrowingApiConnection(), userConfig, WorkflowPhases.request);

            RequestTickets component = new();
            SetMember(component, "userConfig", userConfig);
            SetMember(component, "wfHandler", handler);

            await InvokePrivateTask(typeof(RequestTickets), component, "Reset");

            Assert.That(handler.ReadOnlyMode, Is.False);
        }

        [Test]
        public async Task RequestTickets_HandleTicketId_IgnoresInvalidTicketIds()
        {
            RequestTickets component = new();
            SetMember(component, "TicketId", "not-a-number");

            Assert.DoesNotThrowAsync(async () => await InvokePrivateTask(typeof(RequestTickets), component, "HandleTicketId"));
        }

        [Test]
        public async Task DisplayPathAnalysis_LoadsMatchingDevicesAndCloses()
        {
            PathAnalysisApiConnection apiConnection = new()
            {
                PathDevices =
                [
                    new Device { Id = 11, Name = "gw-11" }
                ]
            };
            DisplayPathAnalysis component = new();
            SetMember(component, "apiConnection", apiConnection);
            SetMember(component, "userConfig", new RequestCoverageUserConfig());
            SetMember(component, nameof(DisplayPathAnalysis.Display), true);
            SetMember(component, nameof(DisplayPathAnalysis.ReqTask), new WfReqTask
            {
                Elements =
                [
                    new WfReqElement { Field = ElemFieldType.source.ToString(), Cidr = new Cidr("10.0.0.1/32") },
                    new WfReqElement { Field = ElemFieldType.destination.ToString(), Cidr = new Cidr("10.0.1.1/32") }
                ]
            });

            await InvokePrivateTask(typeof(DisplayPathAnalysis), component, "OnParametersSetAsync");

            GetPrivateMethod(typeof(DisplayPathAnalysis), "Close").Invoke(component, []);
            Assert.That(GetMember<bool>(component, nameof(DisplayPathAnalysis.Display)), Is.False);
            Assert.That(apiConnection.Queries, Does.Contain(NetworkAnalysisQueries.pathAnalysis));
        }

        [Test]
        public async Task DisplayRules_AddsAndRemovesRules()
        {
            await using BunitContext context = new();
            context.Services.AddAuthorizationCore();
            context.Services.AddSingleton<UserConfig>(new RequestCoverageUserConfig());
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<AuthenticationStateProvider>(new TestAuthStateProvider(Roles.Admin));

            List<NwRuleElement> rules = [new() { RuleUid = "rule-1" }];
            IRenderedComponent<DisplayRules> component = context.Render<DisplayRules>(parameters => parameters
                .Add(p => p.Rules, rules)
                .Add(p => p.TaskId, 42)
                .Add(p => p.EditMode, true));

            component.FindAll("input[type=text]")[1].Change("rule-2");
            Assert.That(rules, Has.Count.EqualTo(2));

            component.Find("button").Click();
            Assert.That(rules, Has.Count.EqualTo(1));
            Assert.That(rules.Single().RuleUid, Is.EqualTo("rule-2"));
        }

        [Test]
        public void DeleteObject_PerformCallsDeleteAndResetParent()
        {
            int deleteCalls = 0;
            int resetCalls = 0;
            DeleteObject component = new();
            SetMember(component, nameof(DeleteObject.Display), true);
            SetMember(component, nameof(DeleteObject.Delete), (Func<Task>)(() =>
            {
                deleteCalls++;
                return Task.CompletedTask;
            }));
            SetMember(component, nameof(DeleteObject.ResetParent), (Func<Task>)(() =>
            {
                resetCalls++;
                return Task.CompletedTask;
            }));

            InvokePrivateTask(component, "Perform").GetAwaiter().GetResult();

            Assert.Multiple(() =>
            {
                Assert.That(deleteCalls, Is.EqualTo(1));
                Assert.That(resetCalls, Is.EqualTo(1));
                Assert.That(GetMember<bool>(component, nameof(DeleteObject.Display)), Is.False);
            });
        }

        [Test]
        public void CommentObject_ResetsTextOnlyOnFirstDisplay()
        {
            CommentObject component = new();
            SetMember(component, nameof(CommentObject.Display), true);
            SetMember(component, "commentText", "previous");

            GetPrivateMethod(typeof(CommentObject), "OnParametersSet").Invoke(component, []);

            Assert.That(GetMember<string>(component, "commentText"), Is.Empty);

            SetMember(component, "commentText", "retained");
            GetPrivateMethod(typeof(CommentObject), "OnParametersSet").Invoke(component, []);

            Assert.That(GetMember<string>(component, "commentText"), Is.EqualTo("retained"));
        }

        [Test]
        public void AssignObject_PerformAssignAndBackUpdateState()
        {
            int assignCalls = 0;
            int assignBackCalls = 0;
            int resetCalls = 0;
            WfStatefulObject statefulObject = new()
            {
                AssignedGroup = "cn=original"
            };

            AssignObject component = new();
            SetMember(component, nameof(AssignObject.Display), true);
            SetMember(component, nameof(AssignObject.StatefulObject), statefulObject);
            SetMember(component, nameof(AssignObject.Assign), (Func<WfStatefulObject, Task>)(obj =>
            {
                assignCalls++;
                return Task.CompletedTask;
            }));
            SetMember(component, nameof(AssignObject.AssignBack), (Func<Task>)(() =>
            {
                assignBackCalls++;
                return Task.CompletedTask;
            }));
            SetMember(component, nameof(AssignObject.ResetParent), (Func<Task>)(() =>
            {
                resetCalls++;
                return Task.CompletedTask;
            }));
            SetMember(component, "selectedUserGroup", new UiUser { Dn = "cn=target", Name = "Target" });

            InvokePrivateTask(component, "PerformAssign").GetAwaiter().GetResult();
            Assert.That(statefulObject.AssignedGroup, Is.EqualTo("cn=target"));
            Assert.That(assignCalls, Is.EqualTo(1));
            Assert.That(resetCalls, Is.EqualTo(1));

            SetMember(component, nameof(AssignObject.Display), true);
            InvokePrivateTask(component, "PerformAssignBack").GetAwaiter().GetResult();

            Assert.That(assignBackCalls, Is.EqualTo(1));
            Assert.That(resetCalls, Is.EqualTo(2));
        }

        [Test]
        public async Task DisplayImplTaskTable_ResolvesTicketsDevicesAndPopupActions()
        {
            DisplayImplTaskTable component = new();
            RequestCoverageUserConfig userConfig = CreateUserConfig(Roles.Implementer);
            WfTicket ticket = new()
            {
                Id = 10,
                Editable = true,
                Tasks =
                [
                    new WfReqTask
                    {
                        Id = 20,
                        Owners =
                        [
                            new FwoOwnerDataHelper
                            {
                                Owner = new FwoOwner { Id = 77, Name = "Owner A" }
                            }
                        ]
                    }
                ]
            };
            WfHandler handler = new()
            {
                ActReqTask = ticket.Tasks[0],
                ActTicket = ticket,
                TicketList = [ticket],
                Devices = [new Device { Id = 55, Name = "gw-55" }]
            };
            handler.ActReqTask.ImplementationTasks = [new WfImplTask { Id = 99, TaskType = WfTaskType.access.ToString(), TicketId = 10, ReqTaskId = 20, DeviceId = 55, TaskNumber = 1 }];
            handler.ActReqTask.Elements =
            [
                new WfReqElement { Field = ElemFieldType.source.ToString(), Cidr = new Cidr("10.0.0.1/32") },
                new WfReqElement { Field = ElemFieldType.destination.ToString(), Cidr = new Cidr("10.0.1.1/32") }
            ];
            SetMatrix(handler, WfTaskType.access.ToString(), CreateMatrix());
            SetMember(component, nameof(DisplayImplTaskTable.WfHandler), handler);
            SetMember(component, nameof(DisplayImplTaskTable.States), new WfStateDict { Name = { [1] = "Draft" } });
            SetMember(component, nameof(DisplayImplTaskTable.ImplTaskView), true);
            SetMember(component, nameof(DisplayImplTaskTable.Phase), WorkflowPhases.implementation);
            SetMember(component, "userConfig", userConfig);
            PathAnalysisApiConnection apiConnection = new();
            SetMember(component, "apiConnection", apiConnection);

            await InvokePrivateTask(typeof(DisplayImplTaskTable), component, "OnParametersSetAsync");

            WfImplTask implTask = handler.ActReqTask.ImplementationTasks[0];
            Assert.Multiple(() =>
            {
                Assert.That(GetPrivateMethod(typeof(DisplayImplTaskTable), "ResolveTicket").Invoke(component, [implTask]), Is.EqualTo(ticket));
                Assert.That(GetPrivateMethod(typeof(DisplayImplTaskTable), "GetOwnerName").Invoke(component, [implTask]), Is.EqualTo("Owner A"));
                Assert.That(GetPrivateMethod(typeof(DisplayImplTaskTable), "GetDeviceName").Invoke(component, [implTask]), Is.EqualTo("gw-55"));
                Assert.That(GetPrivateMethod(typeof(DisplayImplTaskTable), "IsEditable").Invoke(component, [implTask]), Is.True);
            });

            GetPrivateMethod(typeof(DisplayImplTaskTable), "ShowImplTask").Invoke(component, [implTask]);
            Assert.That(handler.DisplayImplTaskMode, Is.True);

            GetPrivateMethod(typeof(DisplayImplTaskTable), "EditImplTask").Invoke(component, [implTask]);
            Assert.That(handler.EditImplTaskMode, Is.True);

            GetPrivateMethod(typeof(DisplayImplTaskTable), "DeleteImplTask").Invoke(component, [implTask]);
            Assert.That(handler.DisplayDeleteImplTaskMode, Is.True);

            GetPrivateMethod(typeof(DisplayImplTaskTable), "ShowApprovals").Invoke(component, [implTask]);
            Assert.That(handler.DisplayApprovalImplMode, Is.True);

            GetPrivateMethod(typeof(DisplayImplTaskTable), "AssignImplTask").Invoke(component, [implTask]);
            Assert.That(handler.DisplayAssignImplTaskMode, Is.True);

            GetPrivateMethod(typeof(DisplayImplTaskTable), "CleanupImplTasks").Invoke(component, []);
            Assert.That(handler.DisplayCleanupMode, Is.True);

            GetPrivateMethod(typeof(DisplayImplTaskTable), "AddImplTask").Invoke(component, []);
            Assert.That(handler.DisplayImplTaskMode, Is.True);
            Assert.That(handler.ActImplTask.DeviceId, Is.EqualTo(55));
            Assert.That(handler.ActImplTask.TaskNumber, Is.EqualTo(2));

            await InvokePrivateTask(component, "CheckImplTasks");
            Assert.That(GetMember<bool>(component, "DisplayInfo"), Is.True);
            Assert.That(apiConnection.Queries, Does.Contain(NetworkAnalysisQueries.pathAnalysis));

            await InvokePrivateTask(typeof(DisplayImplTaskTable), component, "ContinueImplPhase", implTask);
        }

        [Test]
        public async Task DisplayImplTaskTable_CheckImplTasks_ShowsEmptyStateWhenNoDevicesAreFound()
        {
            DisplayImplTaskTable component = new();
            RequestCoverageUserConfig userConfig = CreateUserConfig(Roles.Implementer);
            WfTicket ticket = new()
            {
                Id = 10,
                Editable = true,
                Tasks = [new WfReqTask { Id = 20, Elements = [new WfReqElement { Field = ElemFieldType.source.ToString(), Cidr = new Cidr("10.0.0.1/32") }] }]
            };
            WfHandler handler = new()
            {
                ActReqTask = ticket.Tasks[0],
                ActTicket = ticket,
                TicketList = [ticket],
                Devices = []
            };
            SetMatrix(handler, WfTaskType.access.ToString(), CreateMatrix());
            SetMember(component, nameof(DisplayImplTaskTable.WfHandler), handler);
            SetMember(component, nameof(DisplayImplTaskTable.States), new WfStateDict());
            SetMember(component, nameof(DisplayImplTaskTable.ImplTaskView), true);
            SetMember(component, nameof(DisplayImplTaskTable.Phase), WorkflowPhases.implementation);
            SetMember(component, "userConfig", userConfig);
            SetMember(component, "apiConnection", new PathAnalysisApiConnection());

            await InvokePrivateTask(typeof(DisplayImplTaskTable), component, "CheckImplTasks");

            Assert.Multiple(() =>
            {
                Assert.That(GetMember<bool>(component, "DisplayInfo"), Is.True);
                Assert.That(GetMember<List<KeyValuePair<string, bool>>>(component, "deviceCheck"), Is.Empty);
            });
        }

        [Test]
        public void DisplayImplTaskTable_IsEditableUsesTicketFlagForOwnerBasedRequests()
        {
            DisplayImplTaskTable component = new();
            RequestCoverageUserConfig userConfig = CreateUserConfig(Roles.Implementer);
            WfTicket ticket = new()
            {
                Id = 10,
                Editable = false,
                Tasks = [new WfReqTask { Id = 20 }]
            };
            WfHandler handler = new()
            {
                ActReqTask = ticket.Tasks[0],
                ActTicket = ticket,
                TicketList = [ticket],
                Devices = []
            };
            SetMatrix(handler, WfTaskType.access.ToString(), CreateMatrix());
            SetMember(component, nameof(DisplayImplTaskTable.WfHandler), handler);
            SetMember(component, nameof(DisplayImplTaskTable.States), new WfStateDict());
            SetMember(component, nameof(DisplayImplTaskTable.ImplTaskView), true);
            SetMember(component, "userConfig", userConfig);

            WfImplTask implTask = new()
            {
                Id = 30,
                TaskType = WfTaskType.access.ToString(),
                TicketId = 10,
                ReqTaskId = 20
            };

            userConfig.ReqOwnerBased = true;
            Assert.That((bool)GetPrivateMethod(typeof(DisplayImplTaskTable), "IsEditable").Invoke(component, [implTask])!, Is.False);

            userConfig.ReqOwnerBased = false;
            Assert.That((bool)GetPrivateMethod(typeof(DisplayImplTaskTable), "IsEditable").Invoke(component, [implTask])!, Is.True);
        }

        [Test]
        public void DisplayImplTaskTable_AddImplTask_UsesDefaultDeviceWhenNoDevicesExist()
        {
            DisplayImplTaskTable component = new();
            RequestCoverageUserConfig userConfig = CreateUserConfig(Roles.Implementer);
            WfHandler handler = new()
            {
                ActReqTask = new WfReqTask
                {
                    Id = 20,
                    Title = "Request",
                    TaskType = WfTaskType.access.ToString()
                },
                Devices = []
            };
            SetMatrix(handler, WfTaskType.access.ToString(), CreateMatrix());
            SetMember(component, nameof(DisplayImplTaskTable.WfHandler), handler);
            SetMember(component, nameof(DisplayImplTaskTable.States), new WfStateDict());
            SetMember(component, nameof(DisplayImplTaskTable.ImplTaskView), false);
            SetMember(component, "userConfig", userConfig);

            GetPrivateMethod(typeof(DisplayImplTaskTable), "AddImplTask").Invoke(component, []);

            Assert.Multiple(() =>
            {
                Assert.That(handler.DisplayImplTaskMode, Is.True);
                Assert.That(handler.ActImplTask.DeviceId, Is.EqualTo(0));
                Assert.That(handler.ActImplTask.TaskNumber, Is.EqualTo(1));
                Assert.That(handler.ActImplTask.Title, Does.Not.EndWith(": "));
            });
        }

        [Test]
        public void DisplayImplTaskTable_OnParametersSetAsync_ResolvesAllDevicesAndCachedLookups()
        {
            DisplayImplTaskTable component = new();
            RequestCoverageUserConfig userConfig = CreateUserConfig(Roles.Implementer);
            WfReqTask reqTask = new()
            {
                Id = 20,
                TaskType = WfTaskType.access.ToString()
            };
            reqTask.SetDeviceList([WfReqTaskBase.kAllDevicesId]);
            reqTask.Owners = [new FwoOwnerDataHelper { Owner = new FwoOwner { Id = 77, Name = "Owner A" } }];
            WfTicket ticket = new()
            {
                Id = 10,
                Tasks = [reqTask]
            };
            WfImplTask implTask = new()
            {
                Id = 99,
                TicketId = 10,
                ReqTaskId = 20,
                TaskType = WfTaskType.access.ToString(),
                DeviceId = null
            };
            WfHandler handler = new()
            {
                ActReqTask = reqTask,
                ActImplTask = implTask,
                ActTicket = ticket,
                TicketList = [ticket],
                Devices = []
            };
            SetMatrix(handler, WfTaskType.access.ToString(), CreateMatrix());
            SetMember(component, nameof(DisplayImplTaskTable.WfHandler), handler);
            SetMember(component, nameof(DisplayImplTaskTable.States), new WfStateDict { Name = { [1] = "Draft" } });
            SetMember(component, nameof(DisplayImplTaskTable.ImplTaskView), true);
            SetMember(component, "userConfig", userConfig);

            InvokePrivateTask(typeof(DisplayImplTaskTable), component, "OnParametersSetAsync").GetAwaiter().GetResult();

            Assert.Multiple(() =>
            {
                Assert.That(GetPrivateMethod(typeof(DisplayImplTaskTable), "ResolveTicket").Invoke(component, [implTask]), Is.EqualTo(ticket));
                Assert.That(GetPrivateMethod(typeof(DisplayImplTaskTable), "GetOwnerName").Invoke(component, [implTask]), Is.EqualTo("Owner A"));
                Assert.That(GetPrivateMethod(typeof(DisplayImplTaskTable), "GetDeviceName").Invoke(component, [implTask]), Is.EqualTo(userConfig.GetText("all")));
                Assert.That(GetPrivateMethod(typeof(DisplayImplTaskTable), "IsAllDevicesImplTask").Invoke(component, [implTask]), Is.True);
            });
        }

        [Test]
        public async Task DisplayImplTaskTable_AssignAndAssignBack_RefreshTheVisibleTask()
        {
            DisplayImplTaskTable component = new();
            RequestCoverageUserConfig userConfig = CreateUserConfig(Roles.Implementer);
            WfImplTask storedTask = new()
            {
                Id = 99,
                TicketId = 10,
                ReqTaskId = 20,
                TaskType = WfTaskType.access.ToString(),
                AssignedGroup = "cn=old"
            };
            WfTicket ticket = new()
            {
                Id = 10,
                Tasks =
                [
                    new WfReqTask
                    {
                        Id = 20,
                        TaskType = WfTaskType.access.ToString(),
                        ImplementationTasks = [storedTask]
                    }
                ]
            };
            WfHandler handler = new()
            {
                ActReqTask = ticket.Tasks[0],
                ActImplTask = new WfImplTask(storedTask)
                {
                    AssignedGroup = "cn=new",
                    CurrentHandler = new UiUser { Dn = "cn=old", Name = "Old" }
                },
                ActTicket = ticket,
                TicketList = [ticket],
                Devices = []
            };
            SetMatrix(handler, WfTaskType.access.ToString(), CreateMatrix());
            SetMember(component, nameof(DisplayImplTaskTable.WfHandler), handler);
            SetMember(component, nameof(DisplayImplTaskTable.States), new WfStateDict());
            SetMember(component, nameof(DisplayImplTaskTable.ImplTaskView), true);
            SetMember(component, "userConfig", userConfig);
            SetMember(component, "AllImplTasks", new List<WfImplTask> { storedTask });

            await InvokePrivateTask(component, "Assign", new WfStatefulObject { AssignedGroup = "cn=new" });
            Assert.That(GetMember<List<WfImplTask>>(component, "AllImplTasks")[0].AssignedGroup, Is.EqualTo("cn=new"));
            Assert.That(handler.DisplayAssignImplTaskMode, Is.False);

            await InvokePrivateTask(component, "AssignBack");
            Assert.That(GetMember<List<WfImplTask>>(component, "AllImplTasks")[0].AssignedGroup, Is.EqualTo("cn=old"));
            Assert.That(handler.DisplayAssignImplTaskMode, Is.False);
        }

        [Test]
        public async Task DisplayImplTaskTable_ContinueImplPhase_ReassignsTheCurrentHandler()
        {
            DisplayImplTaskTable component = new();
            RequestCoverageUserConfig userConfig = CreateUserConfig(Roles.Implementer);
            WfImplTask implTask = new()
            {
                Id = 99,
                TicketId = 10,
                ReqTaskId = 20,
                TaskType = WfTaskType.access.ToString(),
                CurrentHandler = new UiUser { DbId = 10, Name = "Other" }
            };
            WfTicket ticket = new()
            {
                Id = 10,
                Tasks = [new WfReqTask { Id = 20, TaskType = WfTaskType.access.ToString(), ImplementationTasks = [implTask] }]
            };
            WfHandler handler = new()
            {
                ActReqTask = ticket.Tasks[0],
                ActImplTask = implTask,
                ActTicket = ticket,
                TicketList = [ticket],
                Devices = []
            };
            SetMember(handler, "userConfig", userConfig);
            SetMatrix(handler, WfTaskType.access.ToString(), CreateMatrix());
            SetMember(component, nameof(DisplayImplTaskTable.WfHandler), handler);
            SetMember(component, nameof(DisplayImplTaskTable.States), new WfStateDict());
            SetMember(component, nameof(DisplayImplTaskTable.ImplTaskView), true);
            SetMember(component, "userConfig", userConfig);

            await InvokePrivateTask(component, "ContinueImplPhase", implTask);

            Assert.That(handler.ActImplTask.CurrentHandler?.DbId, Is.EqualTo(userConfig.User.DbId));
        }

        [Test]
        public void DisplayImplTaskTable_RowActions_SetTheExpectedHandlerModes()
        {
            DisplayImplTaskTable component = new();
            RequestCoverageUserConfig userConfig = CreateUserConfig(Roles.Implementer);
            WfImplTask implTask = new()
            {
                Id = 99,
                TicketId = 10,
                ReqTaskId = 20,
                TaskType = WfTaskType.access.ToString()
            };
            WfReqTask reqTask = new()
            {
                Id = 20,
                TaskType = WfTaskType.access.ToString(),
                ImplementationTasks = [implTask]
            };
            WfTicket ticket = new()
            {
                Id = 10,
                Tasks = [reqTask]
            };
            WfHandler handler = new()
            {
                ActReqTask = reqTask,
                ActImplTask = implTask,
                ActTicket = ticket,
                TicketList = [ticket],
                Devices = []
            };
            SetMatrix(handler, WfTaskType.access.ToString(), CreateMatrix());
            SetMember(component, nameof(DisplayImplTaskTable.WfHandler), handler);
            SetMember(component, nameof(DisplayImplTaskTable.States), new WfStateDict());
            SetMember(component, nameof(DisplayImplTaskTable.ImplTaskView), true);
            SetMember(component, "userConfig", userConfig);

            GetPrivateMethod(typeof(DisplayImplTaskTable), "ShowImplTask").Invoke(component, [implTask]);
            Assert.That(handler.DisplayImplTaskMode, Is.True);

            handler.ResetImplTaskActions();
            GetPrivateMethod(typeof(DisplayImplTaskTable), "EditImplTask").Invoke(component, [implTask]);
            Assert.That(handler.DisplayImplTaskMode, Is.True);
            Assert.That(handler.EditImplTaskMode, Is.True);

            handler.ResetImplTaskActions();
            GetPrivateMethod(typeof(DisplayImplTaskTable), "DeleteImplTask").Invoke(component, [implTask]);
            Assert.That(handler.DisplayDeleteImplTaskMode, Is.True);

            handler.ResetImplTaskActions();
            GetPrivateMethod(typeof(DisplayImplTaskTable), "ShowApprovals").Invoke(component, [implTask]);
            Assert.That(handler.DisplayApprovalImplMode, Is.True);

            handler.ResetImplTaskActions();
            GetPrivateMethod(typeof(DisplayImplTaskTable), "AssignImplTask").Invoke(component, [implTask]);
            Assert.That(handler.DisplayAssignImplTaskMode, Is.True);

            handler.ResetImplTaskActions();
            GetPrivateMethod(typeof(DisplayImplTaskTable), "CleanupImplTasks").Invoke(component, []);
            Assert.That(handler.DisplayCleanupMode, Is.True);
        }

        [Test]
        public async Task DisplayTicketTable_AddAndEditTicket_UseExpectedTicketModes()
        {
            DisplayTicketTable component = new();
            RequestCoverageUserConfig userConfig = CreateUserConfig(Roles.Requester);
            WfHandler handler = new()
            {
                MasterStateMatrix = CreateMatrix(),
                ActTicket = new WfTicket { Id = 11, Title = "Ticket", StateId = 3 },
                ReadOnlyMode = false
            };
            SetMember(component, nameof(DisplayTicketTable.WfHandler), handler);
            SetMember(component, nameof(DisplayTicketTable.Phase), WorkflowPhases.request);
            SetMember(component, "userConfig", userConfig);

            await InvokePrivateTask(typeof(DisplayTicketTable), component, "AddTicket");
            Assert.Multiple(() =>
            {
                Assert.That(handler.DisplayTicketMode, Is.True);
                Assert.That(handler.EditTicketMode, Is.True);
                Assert.That(handler.AddTicketMode, Is.True);
                Assert.That(handler.ActTicket.Requester, Is.EqualTo(userConfig.User));
            });

            await InvokePrivateTask(typeof(DisplayTicketTable), component, "ShowTicketDetails", new WfTicket { Id = 22, Title = "Details", StateId = 4 });
            Assert.That(handler.DisplayTicketMode, Is.True);

            await InvokePrivateTask(typeof(DisplayTicketTable), component, "EditTicket", new WfTicket { Id = 23, Title = "Edit", StateId = 4 });
            Assert.That(handler.EditTicketMode, Is.True);
        }

        [Test]
        public void DisplayTicketTable_CanEditTicketInPhase_RespectsStateBounds()
        {
            DisplayTicketTable component = new();
            SetMember(component, nameof(DisplayTicketTable.WfHandler), new WfHandler
            {
                MasterStateMatrix = CreateMatrix(lowestInputState: 2, lowestEndState: 7),
                ReadOnlyMode = false
            });

            bool editable = (bool)GetPrivateMethod(typeof(DisplayTicketTable), "CanEditTicketInPhase").Invoke(component, [new WfTicket { StateId = 4 }])!;
            bool notEditableLow = (bool)GetPrivateMethod(typeof(DisplayTicketTable), "CanEditTicketInPhase").Invoke(component, [new WfTicket { StateId = 1 }])!;
            bool notEditableHigh = (bool)GetPrivateMethod(typeof(DisplayTicketTable), "CanEditTicketInPhase").Invoke(component, [new WfTicket { StateId = 7 }])!;

            Assert.Multiple(() =>
            {
                Assert.That(editable, Is.True);
                Assert.That(notEditableLow, Is.False);
                Assert.That(notEditableHigh, Is.False);
            });
        }

        [Test]
        public async Task DisplayApprovals_TogglesPopupModesAndAddsComments()
        {
            await using BunitContext context = new();
            context.Services.AddAuthorizationCore();
            context.Services.AddLocalization();
            context.Services.AddSingleton<UserConfig>(new RequestCoverageUserConfig());
            context.Services.AddSingleton(new MiddlewareClient("http://localhost/"));
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<AuthenticationStateProvider>(new TestAuthStateProvider(Roles.Approver));

            IRenderedComponent<DisplayApprovals> component = context.Render<DisplayApprovals>(parameters => parameters
                .Add(p => p.Display, true)
                .Add(p => p.WfHandler, new WfHandler())
                .Add(p => p.ResetParent, DefaultInit.DoNothing)
                .Add(p => p.Approvals, [new WfApproval { Id = 5, StateId = 3, Comments = [] }])
                .Add(p => p.States, new WfStateDict()));

            WfHandler handler = component.Instance.WfHandler;
            WfApproval approval = component.Instance.Approvals[0];

            await InvokePrivateTask(component.Instance, "InitAddComment", approval);
            Assert.That(handler.DisplayApprovalCommentMode, Is.True);

            handler.ResetApprovalActions();
            await InvokePrivateTask(component.Instance, "AssignApproval", approval);
            Assert.That(handler.DisplayAssignApprovalMode, Is.True);

            await component.InvokeAsync(async () => await InvokePrivateTask(component.Instance, "ConfAddComment", "test comment"));
            Assert.That(handler.ActApproval.Comments, Has.Count.EqualTo(1));
            Assert.That(handler.DisplayApprovalCommentMode, Is.False);
        }

        [Test]
        public void ImplOptSelection_InitializesOwnerOptionsWithoutTicketsForNonAdminReducedView()
        {
            ImplOptSelection component = new();
            RequestCoverageUserConfig userConfig = CreateUserConfig(Roles.Requester);
            userConfig.ReqOwnerBased = true;
            userConfig.ReqReducedView = true;

            SetMember(component, "userConfig", userConfig);
            SetMember(component, nameof(ImplOptSelection.Owners), new List<FwoOwner>
            {
                new() { Id = 2, Name = "Beta" },
                new() { Id = 1, Name = "Alpha" }
            });
            SetMember(component, nameof(ImplOptSelection.WfHandler), new WfHandler());

            GetPrivateMethod(typeof(ImplOptSelection), "OnInitialized").Invoke(component, []);

            List<FwoOwner> ownerOptions = GetMember<List<FwoOwner>>(component, "ownerOptions");

            Assert.Multiple(() =>
            {
                Assert.That(ownerOptions.Select(owner => owner.Id), Is.EqualTo(kOwnerOptionIds));
                Assert.That(GetMember<FwoOwner>(component, "selectedOwnerOpt").Id, Is.EqualTo(-1));
                Assert.That(ownerOptions.Any(owner => owner.Id == -3), Is.False);
            });
        }

        [Test]
        public void ImplOptSelection_InitializesDeviceOptionsWithTicketsForAdminReducedView()
        {
            ImplOptSelection component = new();
            RequestCoverageUserConfig userConfig = CreateUserConfig(Roles.Admin);
            userConfig.ReqOwnerBased = false;
            userConfig.ReqReducedView = true;

            SetMember(component, "userConfig", userConfig);
            SetMember(component, nameof(ImplOptSelection.WfHandler), new WfHandler
            {
                Devices =
                [
                    new Device { Id = 11, Name = "gw-11" },
                    new Device { Id = 12, Name = "gw-12" }
                ]
            });

            GetPrivateMethod(typeof(ImplOptSelection), "OnInitialized").Invoke(component, []);

            List<Device> deviceOptions = GetMember<List<Device>>(component, "deviceOptions");

            Assert.Multiple(() =>
            {
                Assert.That(deviceOptions.Select(device => device.Id), Is.EqualTo(kDeviceOptionIds));
                Assert.That(GetMember<Device>(component, "selectedDeviceOpt").Id, Is.EqualTo(-1));
            });
        }

        [Test]
        public async Task ImplOptSelection_SelectionCallbacksUpdateSelectionAndInvokeDelegates()
        {
            ImplOptSelection ownerComponent = new();
            RequestCoverageUserConfig ownerConfig = CreateUserConfig(Roles.Requester);
            ownerConfig.ReqOwnerBased = true;
            SetMember(ownerComponent, "userConfig", ownerConfig);
            SetMember(ownerComponent, nameof(ImplOptSelection.WfHandler), new WfHandler());

            int ownerCalls = 0;
            FwoOwner selectedOwner = new();
            SetMember(ownerComponent, nameof(ImplOptSelection.SelectOwner), (Func<FwoOwner, Task>)(owner =>
            {
                ownerCalls++;
                selectedOwner = owner;
                return Task.CompletedTask;
            }));

            await InvokePrivateTask(typeof(ImplOptSelection), ownerComponent, "OwnerSelectionChanged", new FwoOwner { Id = 21, Name = "Selected owner" });

            ImplOptSelection deviceComponent = new();
            RequestCoverageUserConfig deviceConfig = CreateUserConfig();
            deviceConfig.ReqOwnerBased = false;
            SetMember(deviceComponent, "userConfig", deviceConfig);
            SetMember(deviceComponent, nameof(ImplOptSelection.WfHandler), new WfHandler());

            int deviceCalls = 0;
            Device selectedDevice = new();
            SetMember(deviceComponent, nameof(ImplOptSelection.SelectDevice), (Func<Device, Task>)(device =>
            {
                deviceCalls++;
                selectedDevice = device;
                return Task.CompletedTask;
            }));

            await InvokePrivateTask(typeof(ImplOptSelection), deviceComponent, "DeviceSelectionChanged", new Device { Id = 33, Name = "gw-33" });

            Assert.Multiple(() =>
            {
                Assert.That(GetMember<FwoOwner>(ownerComponent, "selectedOwnerOpt").Id, Is.EqualTo(21));
                Assert.That(ownerCalls, Is.EqualTo(1));
                Assert.That(selectedOwner.Id, Is.EqualTo(21));
                Assert.That(GetMember<Device>(deviceComponent, "selectedDeviceOpt").Id, Is.EqualTo(33));
                Assert.That(deviceCalls, Is.EqualTo(1));
                Assert.That(selectedDevice.Id, Is.EqualTo(33));
            });
        }

        [Test]
        public void DisplayRequestTask_NeedsManualDeviceSelection_RequiresSelectionWhenPlanningIsInactive()
        {
            DisplayRequestTask component = new();
            RequestCoverageUserConfig userConfig = CreateUserConfig();
            userConfig.ReqAutoCreateImplTasks = AutoCreateImplTaskOptions.enterInReqTask;

            SetMember(component, "userConfig", userConfig);
            SetMember(component, nameof(DisplayRequestTask.WfHandler), new WfHandler
            {
                ActStateMatrix = new StateMatrix
                {
                    PhaseActive =
                    {
                        [WorkflowPhases.planning] = false
                    }
                },
                ActReqTask = new WfReqTask()
            });

            bool needsManualSelection = InvokePrivateBool(component, "NeedsManualDeviceSelection");

            Assert.That(needsManualSelection, Is.True);
        }

        [Test]
        public void DisplayRequestTask_NeedsManualDeviceSelection_ReturnsFalseWhenPlanningIsActive()
        {
            DisplayRequestTask component = new();
            RequestCoverageUserConfig userConfig = CreateUserConfig();
            userConfig.ReqAutoCreateImplTasks = AutoCreateImplTaskOptions.oneTaskForAllDevices;

            SetMember(component, "userConfig", userConfig);
            SetMember(component, nameof(DisplayRequestTask.WfHandler), new WfHandler
            {
                ActStateMatrix = new StateMatrix
                {
                    PhaseActive =
                    {
                        [WorkflowPhases.planning] = true
                    }
                },
                ActReqTask = new WfReqTask()
            });

            bool needsManualSelection = InvokePrivateBool(component, "NeedsManualDeviceSelection");

            Assert.That(needsManualSelection, Is.False);
        }

        [Test]
        public void DisplayImplementationTask_DisplayObjectAndServiceElements_UseFlowIdsWhenNoNamesAreAvailable()
        {
            DisplayImplementationTask component = new();
            SetMember(component, "userConfig", CreateUserConfig());

            string objectDisplay = (string)GetPrivateMethod(typeof(DisplayImplementationTask), "DisplayObjectElement").Invoke(component, [new NwObjectElement { FlowNetworkObjectId = 41 }])!;
            string serviceDisplay = (string)GetPrivateMethod(typeof(DisplayImplementationTask), "DisplayServiceElement").Invoke(component, [new NwServiceElement { FlowServiceGroupId = 42 }])!;

            Assert.Multiple(() =>
            {
                Assert.That(objectDisplay, Is.EqualTo("41"));
                Assert.That(serviceDisplay, Is.EqualTo("42"));
            });
        }

        [Test]
        public void DisplayImplementationTask_DisplayDevice_ReturnsAllWhenRequestUsesAllDevices()
        {
            DisplayImplementationTask component = new();
            RequestCoverageUserConfig userConfig = CreateUserConfig();
            WfReqTask reqTask = new()
            {
                Id = 20,
                TaskType = WfTaskType.access.ToString()
            };
            reqTask.SetDeviceList([WfReqTaskBase.kAllDevicesId]);

            SetMember(component, "userConfig", userConfig);
            SetMember(component, nameof(DisplayImplementationTask.WfHandler), new WfHandler
            {
                ActReqTask = reqTask,
                ActImplTask = new WfImplTask
                {
                    ReqTaskId = 20,
                    TaskType = WfTaskType.access.ToString(),
                    DeviceId = null
                }
            });

            string displayDevice = (string)GetPrivateMethod(typeof(DisplayImplementationTask), "DisplayDevice").Invoke(component, [])!;

            Assert.That(displayDevice, Is.EqualTo(userConfig.GetText("all")));
        }

        [Test]
        public void DisplayImplementationTask_RequestAssignOwner_SetsConfirmationState()
        {
            DisplayImplementationTask component = new();
            SetMember(component, "userConfig", CreateUserConfig());

            GetPrivateMethod(typeof(DisplayImplementationTask), "RequestAssignOwner").Invoke(component, []);

            Assert.Multiple(() =>
            {
                Assert.That(GetMember<bool>(component, "assignOwnerMode"), Is.True);
                Assert.That(GetMember<string>(component, "message"), Is.EqualTo("U8004"));
            });
        }

        [Test]
        public void DisplayImplementationTask_OnParametersSetAsync_InitializesCachedFields()
        {
            DisplayImplementationTask component = new();
            RequestCoverageUserConfig userConfig = CreateUserConfig(Roles.Implementer);
            WfReqTask reqTask = new()
            {
                Id = 20,
                TaskType = WfTaskType.access.ToString()
            };
            reqTask.Owners = [new FwoOwnerDataHelper { Owner = new FwoOwner { Id = 55, Name = "Owner A" } }];
            reqTask.SetAddInfo(AdditionalInfoKeys.GrpName, "group-a");
            WfImplTask implTask = new()
            {
                Id = 99,
                TicketId = 10,
                ReqTaskId = 20,
                TaskType = WfTaskType.access.ToString(),
                DeviceId = 66,
                RuleAction = 3,
                Tracking = 4,
                Comments = [new WfCommentDataHelper(new WfComment { CommentText = "first comment" })]
            };
            WfTicket ticket = new()
            {
                Id = 10,
                Tasks = [reqTask]
            };
            WfHandler handler = new()
            {
                DisplayImplTaskMode = true,
                DisplayImplTaskCommentMode = true,
                ActReqTask = reqTask,
                ActImplTask = implTask,
                ActTicket = ticket,
                TicketList = [ticket],
                Devices = [new Device { Id = 66, Name = "gw-66" }]
            };
            SetMatrix(handler, WfTaskType.access.ToString(), CreateMatrix());
            SetMember(component, "userConfig", userConfig);
            SetMember(component, nameof(DisplayImplementationTask.WfHandler), handler);
            SetMember(component, nameof(DisplayImplementationTask.StateMatrix), CreateMatrix());
            SetMember(component, nameof(DisplayImplementationTask.States), new WfStateDict { Name = { [1] = "Open" } });
            SetMember(component, nameof(DisplayImplementationTask.Phase), WorkflowPhases.implementation);
            SetMember(component, nameof(DisplayImplementationTask.IncludePopups), true);
            SetMember(component, "firstParamSet", true);
            SetMember(component, "ruleActions", new List<RuleAction> { new() { Id = 3, Name = "Allow" } });
            SetMember(component, "trackings", new List<Tracking> { new() { Id = 4, Name = "Track" } });

            InvokePrivateTask(typeof(DisplayImplementationTask), component, "OnParametersSetAsync").GetAwaiter().GetResult();

            Assert.Multiple(() =>
            {
                Assert.That(GetMember<Device?>(component, "actDevice")?.Id, Is.EqualTo(66));
                Assert.That(GetMember<RuleAction?>(component, "actRuleAction")?.Id, Is.EqualTo(3));
                Assert.That(GetMember<Tracking?>(component, "actTracking")?.Id, Is.EqualTo(4));
                Assert.That(GetMember<FwoOwner?>(component, "actOwner")?.Id, Is.EqualTo(55));
                Assert.That(GetMember<FwoOwner?>(component, "oldOwner")?.Id, Is.EqualTo(55));
                Assert.That(GetMember<bool>(component, "newOwnerAssigned"), Is.False);
                Assert.That(GetMember<string?>(component, "actGrpName"), Is.EqualTo("group-a"));
                Assert.That(GetMember<string>(component, "allComments"), Does.Contain("first comment"));
                Assert.That(handler.DisplayImplTaskCommentMode, Is.False);
            });
        }

        [Test]
        public void DisplayImplementationTask_InitPromoteAndCancelPromote_TogglePopupMode()
        {
            DisplayImplementationTask component = new();
            WfHandler handler = new()
            {
                DisplayPromoteImplTaskMode = false
            };
            SetMember(component, nameof(DisplayImplementationTask.WfHandler), handler);
            SetMember(component, "userConfig", CreateUserConfig());

            GetPrivateMethod(typeof(DisplayImplementationTask), "InitPromoteImplTask").Invoke(component, []);
            bool cancelResult = (bool)GetPrivateMethod(typeof(DisplayImplementationTask), "CancelPromote").Invoke(component, [])!;

            Assert.Multiple(() =>
            {
                Assert.That(handler.DisplayPromoteImplTaskMode, Is.False);
                Assert.That(cancelResult, Is.True);
            });
        }

        [Test]
        public void DisplayImplementationTask_CheckImplTaskValues_RejectsInvalidServicePorts()
        {
            List<string> messages = [];
            DisplayImplementationTask component = new();
            SetMember(component, "userConfig", CreateUserConfig());
            SetMember(component, nameof(DisplayImplementationTask.WfHandler), new WfHandler
            {
                ActImplTask = new WfImplTask
                {
                    Id = 99,
                    TaskType = WfTaskType.access.ToString(),
                    ImplElements =
                    [
                        new WfImplElement { Id = 1, ImplTaskId = 99, Field = ElemFieldType.service.ToString(), Port = 0, ProtoId = 6, ServiceId = null }
                    ]
                }
            });
            SetMember(component, "DisplayMessageInUi", (Action<Exception?, string, string, bool>)((_, _, message, _) => messages.Add(message)));

            bool valid = InvokePrivateBool(component, "CheckImplTaskValues");

            Assert.Multiple(() =>
            {
                Assert.That(valid, Is.False);
                Assert.That(messages, Does.Contain("Port invalid"));
            });
        }

        [Test]
        public void DisplayImplementationTask_SetChangedOwner_AddsOldAndNewOwners()
        {
            DisplayImplementationTask component = new();
            WfHandler handler = new()
            {
                ActReqTask = new WfReqTask()
            };
            SetMember(component, nameof(DisplayImplementationTask.WfHandler), handler);
            SetMember(component, "actOwner", new FwoOwner { Id = 2, Name = "New" });
            SetMember(component, "oldOwner", new FwoOwner { Id = 1, Name = "Old" });

            GetPrivateMethod(typeof(DisplayImplementationTask), "SetChangedOwner").Invoke(component, []);

            Assert.Multiple(() =>
            {
                Assert.That(handler.ActReqTask.RemovedOwners, Has.Count.EqualTo(1));
                Assert.That(handler.ActReqTask.RemovedOwners[0].Id, Is.EqualTo(1));
                Assert.That(handler.ActReqTask.NewOwners, Has.Count.EqualTo(1));
                Assert.That(handler.ActReqTask.NewOwners[0].Id, Is.EqualTo(2));
            });
        }

        [Test]
        public void DisplayImplementationTask_InitAddCommentAndReadonlyActionChecks_UseExpectedFlags()
        {
            DisplayImplementationTask component = new();
            WfHandler handler = new()
            {
                DisplayImplTaskCommentMode = false
            };
            SetMember(component, nameof(DisplayImplementationTask.WfHandler), handler);
            SetMember(component, "userConfig", CreateUserConfig());

            GetPrivateMethod(typeof(DisplayImplementationTask), "InitAddComment").Invoke(component, []);
            bool readOnlyAction = InvokePrivateBool(component, "CanShowConfiguredActionButton",
                new WfStateAction { ActionType = StateActionTypes.DisplayConnection.ToString() });
            bool hiddenAction = InvokePrivateBool(component, "CanShowConfiguredActionButton",
                new WfStateAction { ActionType = StateActionTypes.DoNothing.ToString() });

            Assert.Multiple(() =>
            {
                Assert.That(handler.DisplayImplTaskCommentMode, Is.True);
                Assert.That(readOnlyAction, Is.True);
                Assert.That(hiddenAction, Is.False);
            });
        }

        [Test]
        public void DisplayImplementationTask_UpdateElements_ReconcilesListsAndRules()
        {
            DisplayImplementationTask component = new();
            WfImplElement sourceElem = new() { Id = 1, ImplTaskId = 42, Field = ElemFieldType.source.ToString(), Name = "old source" };
            WfImplElement destinationElem = new() { Id = 2, ImplTaskId = 42, Field = ElemFieldType.destination.ToString(), Name = "old destination" };
            WfImplElement serviceElem = new() { Id = 3, ImplTaskId = 42, Field = ElemFieldType.service.ToString(), Port = 443, ProtoId = 6, Name = "old service" };
            WfImplElement ruleElem = new() { Id = 4, ImplTaskId = 42, Field = ElemFieldType.rule.ToString(), RuleUid = "rule-old", Name = "old rule" };
            WfHandler handler = new()
            {
                ActImplTask = new WfImplTask
                {
                    Id = 42,
                    ImplElements = [sourceElem, destinationElem, serviceElem, ruleElem]
                }
            };
            NwObjectElement newSource = new() { ElemId = 5, TaskId = 42, Name = "new source" };
            NwObjectElement newDestination = new() { ElemId = 6, TaskId = 42, Name = "new destination" };
            NwServiceElement newService = new() { ElemId = 7, TaskId = 42, Port = 80, ProtoId = 6, Name = "new service" };
            NwRuleElement newRule = new() { ElemId = 8, TaskId = 42, RuleUid = "rule-new", Name = "new rule" };

            SetMember(component, nameof(DisplayImplementationTask.WfHandler), handler);
            SetMember(component, "actSources", new List<NwObjectElement> { newSource });
            SetMember(component, "actDestinations", new List<NwObjectElement> { newDestination });
            SetMember(component, "actServices", new List<NwServiceElement> { newService });
            SetMember(component, "actRules", new List<NwRuleElement> { newRule });
            SetMember(component, "sourcesToDelete", new List<NwObjectElement> { new NwObjectElement { ElemId = 1, TaskId = 42, Name = "old source" } });
            SetMember(component, "destinationsToDelete", new List<NwObjectElement> { new NwObjectElement { ElemId = 2, TaskId = 42, Name = "old destination" } });
            SetMember(component, "servicesToDelete", new List<NwServiceElement> { new NwServiceElement { ElemId = 3, TaskId = 42, Port = 443, ProtoId = 6, Name = "old service" } });
            SetMember(component, "sourcesToAdd", new List<NwObjectElement> { new NwObjectElement { ElemId = 9, TaskId = 42, Name = "added source" } });
            SetMember(component, "destinationsToAdd", new List<NwObjectElement> { new NwObjectElement { ElemId = 10, TaskId = 42, Name = "added destination" } });
            SetMember(component, "servicesToAdd", new List<NwServiceElement> { new NwServiceElement { ElemId = 11, TaskId = 42, Port = 22, ProtoId = 6, Name = "added service" } });

            GetPrivateMethod(typeof(DisplayImplementationTask), "UpdateElements").Invoke(component, []);

            Assert.Multiple(() =>
            {
                Assert.That(handler.ActImplTask.ImplElements.Any(element => element.Id == 1), Is.False);
                Assert.That(handler.ActImplTask.ImplElements.Any(element => element.Id == 2), Is.False);
                Assert.That(handler.ActImplTask.ImplElements.Any(element => element.Id == 3), Is.False);
                Assert.That(handler.ActImplTask.ImplElements.Any(element => element.Id == 4), Is.False);
                Assert.That(handler.ActImplTask.ImplElements.Any(element => element.Id == 8 && element.Field == ElemFieldType.rule.ToString()), Is.True);
                Assert.That(handler.ActImplTask.ImplElements.Any(element => element.Id == 9 && element.Field == ElemFieldType.source.ToString()), Is.True);
                Assert.That(handler.ActImplTask.ImplElements.Any(element => element.Id == 10 && element.Field == ElemFieldType.destination.ToString()), Is.True);
                Assert.That(handler.ActImplTask.ImplElements.Any(element => element.Id == 11 && element.Field == ElemFieldType.service.ToString()), Is.True);
                Assert.That(GetMember<List<NwObjectElement>>(component, "sourcesToAdd"), Is.Empty);
                Assert.That(GetMember<List<NwObjectElement>>(component, "sourcesToDelete"), Is.Empty);
                Assert.That(GetMember<List<NwObjectElement>>(component, "destinationsToAdd"), Is.Empty);
                Assert.That(GetMember<List<NwObjectElement>>(component, "destinationsToDelete"), Is.Empty);
                Assert.That(GetMember<List<NwServiceElement>>(component, "servicesToAdd"), Is.Empty);
                Assert.That(GetMember<List<NwServiceElement>>(component, "servicesToDelete"), Is.Empty);
            });
        }

        [Test]
        public void DisplayTicket_CheckTicketValues_RejectsEmptyTitle()
        {
            List<string> messages = [];
            DisplayTicket component = new();
            RequestCoverageUserConfig userConfig = CreateUserConfig();
            SetMember(component, "userConfig", userConfig);
            SetMember(component, nameof(DisplayTicket.WfHandler), new WfHandler
            {
                ActTicket = new WfTicket { Title = "" }
            });
            SetMember(component, "DisplayMessageInUi", (Action<Exception?, string, string, bool>)((_, _, message, _) => messages.Add(message)));

            bool valid = InvokePrivateBool(component, "CheckTicketValues");

            Assert.Multiple(() =>
            {
                Assert.That(valid, Is.False);
                Assert.That(messages, Does.Contain("Missing name or reason"));
            });
        }

        [Test]
        public void DisplayTicket_Cancel_ResetsPromoteAndSaveModes()
        {
            DisplayTicket component = new();
            WfHandler handler = new()
            {
                DisplaySaveTicketMode = true,
                DisplayPromoteTicketMode = true
            };
            SetMember(component, nameof(DisplayTicket.WfHandler), handler);

            bool cancelResult = InvokePrivateBool(component, "Cancel");

            Assert.Multiple(() =>
            {
                Assert.That(cancelResult, Is.True);
                Assert.That(handler.DisplaySaveTicketMode, Is.False);
                Assert.That(handler.DisplayPromoteTicketMode, Is.False);
            });
        }

        [Test]
        public void DisplayRequestTask_SetDeviceAndSetDevices_HandleAllAndConcreteSelections()
        {
            DisplayRequestTask component = new();
            RequestCoverageUserConfig userConfig = CreateUserConfig();
            Device allDevice = new() { Id = WfReqTaskBase.kAllDevicesId, Name = userConfig.GetText("all") };
            Device gw1 = new() { Id = 1, Name = "gw-1" };
            Device gw2 = new() { Id = 2, Name = "gw-2" };

            SetMember(component, "userConfig", userConfig);
            SetMember(component, "selectableDevices", new List<Device> { allDevice, gw1, gw2 });

            SetMember(component, "selectedDevices", new List<Device> { gw1 });
            GetPrivateMethod(typeof(DisplayRequestTask), "SetDevices").Invoke(component, [new List<Device> { allDevice, gw2 }]);
            Assert.That(GetMember<IEnumerable<Device>>(component, "selectedDevices").Select(device => device.Id), Is.EqualTo(kAllDeviceSelectionIds));

            GetPrivateMethod(typeof(DisplayRequestTask), "SetDevice").Invoke(component, [allDevice]);
            Assert.That(GetPrivateMethod(typeof(DisplayRequestTask), "DisplayDevices").Invoke(component, []), Is.EqualTo(userConfig.GetText("all")));
            GetPrivateMethod(typeof(DisplayRequestTask), "SetDevices").Invoke(component, [new List<Device> { allDevice, gw1 }]);
            Assert.That(GetMember<IEnumerable<Device>>(component, "selectedDevices").Select(device => device.Id), Is.EqualTo(kSingleDeviceSelectionIds));

            GetPrivateMethod(typeof(DisplayRequestTask), "SetDevices").Invoke(component, [new List<Device> { gw1, gw2 }]);
            Assert.That(GetMember<IEnumerable<Device>>(component, "selectedDevices").Select(device => device.Id), Is.EqualTo(kTwoDeviceSelectionIds));
            Assert.That(GetPrivateMethod(typeof(DisplayRequestTask), "DisplayDevices").Invoke(component, []), Is.EqualTo("gw-1, gw-2"));
        }

        [Test]
        public void DisplayRequestTask_UpdateElements_ReconcilesAddedRemovedAndRuleEntries()
        {
            DisplayRequestTask component = new();
            WfReqTask reqTask = new()
            {
                Id = 42,
                RequestAction = RequestAction.modify.ToString(),
                Elements =
                [
                    new WfReqElement { Id = 1, TaskId = 42, Field = ElemFieldType.source.ToString(), Name = "old src" },
                    new WfReqElement { Id = 2, TaskId = 42, Field = ElemFieldType.destination.ToString(), Name = "old dst" },
                    new WfReqElement { Id = 3, TaskId = 42, Field = ElemFieldType.service.ToString(), Port = 443, ProtoId = 6, Name = "old svc" },
                    new WfReqElement { Id = 4, TaskId = 42, Field = ElemFieldType.rule.ToString(), RuleUid = "rule-old", Name = "old rule", DeviceId = 9 }
                ]
            };
            NwObjectElement oldSource = new() { ElemId = 1, TaskId = 42, Name = "old src" };
            NwObjectElement oldDestination = new() { ElemId = 2, TaskId = 42, Name = "old dst" };
            NwServiceElement oldService = new() { ElemId = 3, TaskId = 42, Port = 443, ProtoId = 6, Name = "old svc" };
            NwRuleElement rule = new() { ElemId = 4, TaskId = 42, RuleUid = "rule-new", Name = "new rule" };
            NwObjectElement newSource = new() { ElemId = 5, Name = "new src" };
            NwObjectElement newDestination = new() { ElemId = 6, Name = "new dst" };
            NwServiceElement newService = new() { ElemId = 7, Port = 80, ProtoId = 6, Name = "new svc" };

            SetMember(component, nameof(DisplayRequestTask.WfHandler), new WfHandler { ActReqTask = reqTask });
            SetMember(component, "actTaskType", WfTaskType.access);
            SetMember(component, "actRuleDevice", new Device { Id = 77, Name = "gw-77" });
            SetMember(component, "actSources", new List<NwObjectElement> { oldSource });
            SetMember(component, "sourcesToDelete", new List<NwObjectElement> { oldSource });
            SetMember(component, "sourcesToAdd", new List<NwObjectElement> { newSource });
            SetMember(component, "actDestinations", new List<NwObjectElement> { oldDestination });
            SetMember(component, "destinationsToDelete", new List<NwObjectElement> { oldDestination });
            SetMember(component, "destinationsToAdd", new List<NwObjectElement> { newDestination });
            SetMember(component, "actServices", new List<NwServiceElement> { oldService });
            SetMember(component, "servicesToDelete", new List<NwServiceElement> { oldService });
            SetMember(component, "servicesToAdd", new List<NwServiceElement> { newService });
            SetMember(component, "actRules", new List<NwRuleElement> { rule });

            GetPrivateMethod(typeof(DisplayRequestTask), "UpdateElements").Invoke(component, []);

            Assert.Multiple(() =>
            {
                Assert.That(reqTask.RemovedElements.Select(element => element.Id), Is.EquivalentTo(kRemovedElementIds));
                Assert.That(reqTask.Elements.Any(element => element.Id is 1 or 2 or 3), Is.False);
                Assert.That(reqTask.Elements.Any(element => element.Id == 5 && element.Field == ElemFieldType.source.ToString()), Is.True);
                Assert.That(reqTask.Elements.Any(element => element.Id == 6 && element.Field == ElemFieldType.destination.ToString()), Is.True);
                Assert.That(reqTask.Elements.Any(element => element.Id == 7 && element.Field == ElemFieldType.service.ToString()), Is.True);
                Assert.That(reqTask.Elements.Single(element => element.Field == ElemFieldType.rule.ToString()).DeviceId, Is.EqualTo(77));
                Assert.That(reqTask.Elements.Single(element => element.Field == ElemFieldType.rule.ToString()).RequestAction, Is.EqualTo(RequestAction.modify.ToString()));
                Assert.That(GetMember<List<NwObjectElement>>(component, "actSources").Single().ElemId, Is.EqualTo(5));
                Assert.That(GetMember<List<NwObjectElement>>(component, "actDestinations").Single().ElemId, Is.EqualTo(6));
                Assert.That(GetMember<List<NwServiceElement>>(component, "actServices").Single().ElemId, Is.EqualTo(7));
            });
        }

        [Test]
        public async Task DisplayRequestTask_PrepareReqTaskForSave_StoresEditedValues()
        {
            DisplayRequestTask component = new();
            RequestCoverageUserConfig userConfig = CreateUserConfig(Roles.Admin);
            WfReqTask reqTask = new()
            {
                Id = 7,
                Title = "Interface change",
                Reason = "Need access",
                TaskType = WfTaskType.access.ToString()
            };
            WfHandler handler = new()
            {
                ActReqTask = reqTask,
                AllOwners =
                [
                    new FwoOwner { Id = 11, Name = "Allowed owner" },
                    new FwoOwner { Id = 12, Name = "Other owner" }
                ]
            };

            SetMember(component, "userConfig", userConfig);
            SetMember(component, nameof(DisplayRequestTask.WfHandler), handler);
            SetMember(component, "actTaskType", WfTaskType.new_interface);
            SetMember(component, "actOwner", new FwoOwner { Id = 21, Name = "Current owner" });
            SetMember(component, "oldOwner", new FwoOwner { Id = 22, Name = "Previous owner" });
            SetMember(component, "actManagement", new Management { Id = 44, Name = "Mgmt" });
            SetMember(component, "managements", new List<Management> { new() { Id = 44, Name = "Mgmt" } });
            SetMember(component, "actRuleAction", new RuleAction { Id = 3, Name = "Allow" });
            SetMember(component, "actTracking", new Tracking { Id = 4, Name = "Track" });
            SetMember(component, "actRequestingOwner", new FwoOwner { Id = 11, Name = "Allowed owner" });
            SetMember(component, "selectedDevices", new List<Device>
            {
                new() { Id = 101, Name = "gw-101" },
                new() { Id = 102, Name = "gw-102" }
            });

            bool saved = await InvokePrivateTaskResult<bool>(component, "PrepareReqTaskForSave");

            Assert.Multiple(() =>
            {
                Assert.That(saved, Is.True);
                Assert.That(reqTask.TaskType, Is.EqualTo(WfTaskType.new_interface.ToString()));
                Assert.That(reqTask.RuleAction, Is.EqualTo(3));
                Assert.That(reqTask.Tracking, Is.EqualTo(4));
                Assert.That(reqTask.ManagementId, Is.EqualTo(44));
                Assert.That(reqTask.GetAddInfoIntValue(AdditionalInfoKeys.ReqOwner), Is.EqualTo(11));
                Assert.That(reqTask.GetDeviceList(), Is.EqualTo(kDeviceListIds));
                Assert.That(reqTask.Owners, Has.Count.EqualTo(1));
                Assert.That(reqTask.Owners[0].Owner.Id, Is.EqualTo(21));
                Assert.That(reqTask.RemovedOwners, Has.Count.EqualTo(1));
                Assert.That(reqTask.RemovedOwners[0].Id, Is.EqualTo(22));
            });
        }

        [Test]
        public async Task DisplayRequestTask_CheckTaskValues_RejectsMissingTitleAndInvalidTaskShapes()
        {
            List<string> messages = [];

            DisplayRequestTask missingTitleComponent = new();
            SetMember(missingTitleComponent, "userConfig", CreateUserConfig());
            SetMember(missingTitleComponent, nameof(DisplayRequestTask.WfHandler), new WfHandler
            {
                ActReqTask = new WfReqTask { Title = "", TaskType = WfTaskType.access.ToString() }
            });
            SetMember(missingTitleComponent, "actTaskType", WfTaskType.access);
            SetMember(missingTitleComponent, "DisplayMessageInUi", (Action<Exception?, string, string, bool>)((_, _, message, _) => messages.Add(message)));

            bool missingTitleValid = await InvokePrivateTaskResult<bool>(missingTitleComponent, "CheckTaskValues");

            DisplayRequestTask accessComponent = new();
            SetMember(accessComponent, "userConfig", CreateUserConfig());
            SetMember(accessComponent, nameof(DisplayRequestTask.WfHandler), new WfHandler
            {
                ActReqTask = new WfReqTask { Title = "Valid", TaskType = WfTaskType.access.ToString() }
            });
            SetMember(accessComponent, "actTaskType", WfTaskType.access);
            SetMember(accessComponent, "DisplayMessageInUi", (Action<Exception?, string, string, bool>)((_, _, message, _) => messages.Add(message)));

            bool accessValid = await InvokePrivateTaskResult<bool>(accessComponent, "CheckTaskValues");

            DisplayRequestTask groupComponent = new();
            SetMember(groupComponent, "userConfig", CreateUserConfig());
            SetMember(groupComponent, nameof(DisplayRequestTask.WfHandler), new WfHandler
            {
                ActReqTask = new WfReqTask { Title = "Valid", TaskType = WfTaskType.group_create.ToString() }
            });
            SetMember(groupComponent, "actTaskType", WfTaskType.group_create);
            SetMember(groupComponent, "actGrpName", "");
            SetMember(groupComponent, "DisplayMessageInUi", (Action<Exception?, string, string, bool>)((_, _, message, _) => messages.Add(message)));

            bool groupValid = await InvokePrivateTaskResult<bool>(groupComponent, "CheckTaskValues");

            Assert.Multiple(() =>
            {
                Assert.That(missingTitleValid, Is.False);
                Assert.That(accessValid, Is.False);
                Assert.That(groupValid, Is.False);
                Assert.That(messages, Has.Count.EqualTo(3));
                Assert.That(messages, Is.All.EqualTo("Missing name or reason"));
            });
        }

        [Test]
        public void DisplayRequestTask_CanEditAndSaveFields_UsesApproverConfig()
        {
            DisplayRequestTask component = new();
            RequestCoverageUserConfig userConfig = CreateUserConfig();
            ApproverAllowedChangesConfig config = new();
            config.SetTaskField(WfTaskType.access, WorkflowEditableFieldKeys.Services, true);
            config.SetTaskField(WfTaskType.access, WorkflowEditableFieldKeys.Reason, true);
            userConfig.ReqAllowedChangesByApprover = config.ToConfigValue();

            SetMember(component, "userConfig", userConfig);
            SetMember(component, nameof(DisplayRequestTask.WfHandler), new WfHandler
            {
                ActReqTask = new WfReqTask
                {
                    Title = "Valid",
                    TaskType = WfTaskType.access.ToString()
                },
                ApproveReqTaskMode = true
            });
            SetMember(component, "Phase", WorkflowPhases.approval);
            SetMember(component, "actTaskType", WfTaskType.access);

            bool canEdit = InvokePrivateBool(component, "CanEditReqTaskField", WorkflowEditableFieldKeys.Services);
            bool canSave = InvokePrivateBool(component, "CanSaveReqTaskChanges");

            Assert.Multiple(() =>
            {
                Assert.That(canEdit, Is.True);
                Assert.That(canSave, Is.True);
            });
        }

        private sealed class RequestCoverageUserConfig : SimulatedUserConfig
        {
            public RequestCoverageUserConfig()
            {
                DummyTranslate["all_readonly"] = "all_readonly";
                DummyTranslate["path_analysis"] = "path_analysis";
                DummyTranslate["rule_uid"] = "rule_uid";
                DummyTranslate["no_gws_found"] = "no_gws_found";
            }

            public override string GetText(string key)
            {
                return DummyTranslate.TryGetValue(key, out string? value) ? value : key;
            }
        }

        private sealed class ThrowingApiConnection : SimulatedApiConnection
        {
            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                throw new NotImplementedException($"Unexpected query: {query}");
            }

            public override GraphQlApiSubscription<SubscriptionResponseType> GetSubscription<SubscriptionResponseType>(Action<Exception> exceptionHandler, GraphQlApiSubscription<SubscriptionResponseType>.SubscriptionUpdate subscriptionUpdateHandler, string subscription, object? variables = null, string? operationName = null)
            {
                return null!;
            }
        }

        private sealed class PathAnalysisApiConnection : SimulatedApiConnection
        {
            public List<string> Queries { get; } = [];
            public List<Device> PathDevices { get; set; } = [];

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                Queries.Add(query);
                if (query == NetworkAnalysisQueries.pathAnalysis)
                {
                    return Task.FromResult((QueryResponseType)(object)PathDevices);
                }

                throw new NotImplementedException($"Unexpected query: {query}");
            }

            public override GraphQlApiSubscription<SubscriptionResponseType> GetSubscription<SubscriptionResponseType>(Action<Exception> exceptionHandler, GraphQlApiSubscription<SubscriptionResponseType>.SubscriptionUpdate subscriptionUpdateHandler, string subscription, object? variables = null, string? operationName = null)
            {
                return null!;
            }
        }

        private sealed class TestAuthStateProvider : AuthenticationStateProvider
        {
            private readonly ClaimsPrincipal principal;

            public TestAuthStateProvider(params string[] roles)
            {
                principal = CreatePrincipal(roles);
            }

            public override Task<AuthenticationState> GetAuthenticationStateAsync()
            {
                return Task.FromResult(new AuthenticationState(principal));
            }
        }
    }
}
