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
using FWO.Ui.Pages.Request;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Reflection;
using System.Security.Claims;

namespace FWO.Test
{
    [TestFixture]
    internal partial class UiRequestCoverageTest
    {
        private static readonly int[] kOwnerOptionIds = [-2, -1, 1, 2];
        private static readonly int[] kDeviceOptionIds = [11, 12, 0, -1];
        private static readonly long[] kRemovedElementIds = [1L, 2L, 3L];
        internal static readonly int[] kDeviceListIds = [101, 102];

        internal static void SetMember(object instance, string memberName, object? value)
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

        internal static T GetMember<T>(object instance, string memberName)
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

        internal static MethodInfo GetPrivateMethod(Type type, string methodName)
        {
            return type.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
                ?? throw new MissingMethodException(type.FullName, methodName);
        }

        internal static async Task InvokePrivateTask(object instance, string methodName, params object[] args)
        {
            Task task = (Task)GetPrivateMethod(instance.GetType(), methodName).Invoke(instance, args)!;
            await task;
        }

        internal static async Task InvokePrivateTask(Type type, object instance, string methodName, params object[] args)
        {
            Task task = (Task)GetPrivateMethod(type, methodName).Invoke(instance, args)!;
            await task;
        }

        internal static async Task<T> InvokePrivateTaskResult<T>(object instance, string methodName, params object[] args)
        {
            Task<T> task = (Task<T>)GetPrivateMethod(instance.GetType(), methodName).Invoke(instance, args)!;
            return await task;
        }

        internal static bool InvokePrivateBool(object instance, string methodName, params object[] args)
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

        internal static RequestCoverageUserConfig CreateUserConfig(params string[] roles)
        {
            RequestCoverageUserConfig userConfig = new();
            userConfig.User.Roles = [.. roles];
            userConfig.User.DbId = 77;
            userConfig.User.Dn = "cn=current";
            return userConfig;
        }

        internal static WfHandler CreateHandler(ApiConnection apiConnection, RequestCoverageUserConfig userConfig, WorkflowPhases phase = WorkflowPhases.request)
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

        internal static StateMatrix CreateMatrix(int lowestInputState = 1, int lowestStartedState = 2, int lowestEndState = 10)
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

        internal static void SetMatrix(WfHandler handler, string taskType, StateMatrix matrix)
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

        internal sealed class RequestCoverageUserConfig : SimulatedUserConfig
        {
            public RequestCoverageUserConfig()
            {
            }

            public override string GetText(string key)
            {
                return DummyTranslate.TryGetValue(key, out string? value) ? value : key;
            }
        }

        internal sealed class ThrowingApiConnection : SimulatedApiConnection
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

        internal sealed class PathAnalysisApiConnection : SimulatedApiConnection
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

        internal sealed class TestAuthStateProvider : AuthenticationStateProvider
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
