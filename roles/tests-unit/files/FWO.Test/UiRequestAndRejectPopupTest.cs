using Bunit;
using GraphQL;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Middleware.Client;
using FWO.Services;
using FWO.Services.Modelling;
using FWO.Ui.Pages.NetworkModelling;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Security.Claims;
using System.Reflection;
using static FWO.Basics.Placeholder;

namespace FWO.Test
{
    [TestFixture]
    internal class UiRequestAndRejectPopupTest
    {
        [Test]
        public void RequestInterfacePopup_OnInitialized_SetsDefaultOwnerAndInterfaceName()
        {
            using BunitContext context = CreateContext(Roles.Modeller);
            SimulatedUserConfig userConfig = (SimulatedUserConfig)context.Services.GetRequiredService<UserConfig>();
            userConfig.ModReqInterfaceName = "req-interface";
            FwoOwner selectedApp = new() { Id = 11, Name = "Selected" };
            FwoOwner requestingOwner = new() { Id = 12, Name = "Requester" };

            IRenderedComponent<RequestInterfacePopup> component = RenderRequestInterfacePopup(
                context,
                selectedApp,
                requestingOwner);

            Assert.Multiple(() =>
            {
                Assert.That(component.FindAll("label").Any(label => label.TextContent.Contains(selectedApp.Name, StringComparison.Ordinal)), Is.True);
                Assert.That(component.Find("input[type='text']").GetAttribute("value"), Is.EqualTo("req-interface"));
            });
        }

        [Test]
        public async Task RequestInterfacePopup_SendRequest_RejectsMissingData()
        {
            using BunitContext context = CreateContext(Roles.Modeller);
            SimulatedUserConfig userConfig = (SimulatedUserConfig)context.Services.GetRequiredService<UserConfig>();
            userConfig.ModReqInterfaceName = "";
            FwoOwner selectedApp = new() { Id = 11, Name = "Selected" };
            FwoOwner requestingOwner = new() { Id = 12, Name = "Requester" };
            List<(Exception? Exception, string Title, string Message, bool IsError)> messages = [];

            IRenderedComponent<RequestInterfacePopup> component = RenderRequestInterfacePopup(
                context,
                selectedApp,
                requestingOwner,
                messageSink: (exception, title, message, isError) => messages.Add((exception, title, message, isError)));

            component.Find("input[type='text']").Change("");
            component.Find("textarea").Change("");
            await component.InvokeAsync(() => component.FindAll("button.btn-primary").Single().Click());

            Assert.Multiple(() =>
            {
                Assert.That(messages, Has.Count.EqualTo(1));
                Assert.That(messages[0].Title, Is.EqualTo(userConfig.GetText("add_new_request")));
                Assert.That(messages[0].Message, Is.EqualTo(userConfig.GetText("E5102")));
                Assert.That(messages[0].IsError, Is.True);
                Assert.That(component.Instance.Display, Is.True);
            });
        }

        [Test]
        public async Task RequestInterfacePopup_SendRequest_RejectsWhenSelectedOwnerMatchesRequestingOwner()
        {
            using BunitContext context = CreateContext(Roles.Modeller);
            SimulatedUserConfig userConfig = (SimulatedUserConfig)context.Services.GetRequiredService<UserConfig>();
            userConfig.ModReqInterfaceName = "req-interface";
            FwoOwner owner = new() { Id = 11, Name = "Owner" };

            IRenderedComponent<RequestInterfacePopup> component = RenderRequestInterfacePopup(
                context,
                owner,
                owner);

            component.Find("input[type='text']").Change("branch-if");
            component.Find("textarea").Change("needed");
            await component.InvokeAsync(() => component.FindAll("button.btn-primary").Single().Click());

            Assert.That(component.Instance.Display, Is.True);
        }

        [Test]
        public async Task RequestInterfacePopup_LoadImmediateRequestNotification_ReturnsDeadlineNoneNotification()
        {
            using BunitContext context = CreateContext(new RequestPopupNotificationApiConn(), Roles.Modeller);
            IRenderedComponent<RequestInterfacePopup> component = RenderRequestInterfacePopup(
                context,
                new FwoOwner { Id = 11, Name = "Selected", ExtAppId = "APP-42" },
                new FwoOwner { Id = 12, Name = "Requester" });

            MethodInfo loadImmediateNotification = typeof(RequestInterfacePopup).GetMethod("LoadImmediateRequestNotification", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("LoadImmediateRequestNotification method not found.");
            Task<FwoNotification?> task = (Task<FwoNotification?>)(loadImmediateNotification.Invoke(component.Instance, Array.Empty<object?>())
                ?? throw new InvalidOperationException("LoadImmediateRequestNotification returned null task."));
            FwoNotification? notification = await task;

            Assert.Multiple(() =>
            {
                Assert.That(notification, Is.Not.Null);
                Assert.That(notification!.Deadline, Is.EqualTo(NotificationDeadline.None));
                Assert.That(notification.EmailSubject, Is.EqualTo("immediate-subject"));
            });
        }

        [Test]
        public async Task RequestInterfacePopup_LoadImmediateRequestNotification_ReturnsNullWhenNoImmediateNotificationExists()
        {
            using BunitContext context = CreateContext(new RequestPopupNoImmediateApiConn(), Roles.Modeller);
            IRenderedComponent<RequestInterfacePopup> component = RenderRequestInterfacePopup(
                context,
                new FwoOwner { Id = 11, Name = "Selected", ExtAppId = "APP-42" },
                new FwoOwner { Id = 12, Name = "Requester" });

            MethodInfo loadImmediateNotification = typeof(RequestInterfacePopup).GetMethod("LoadImmediateRequestNotification", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("LoadImmediateRequestNotification method not found.");
            Task<FwoNotification?> task = (Task<FwoNotification?>)(loadImmediateNotification.Invoke(component.Instance, Array.Empty<object?>())
                ?? throw new InvalidOperationException("LoadImmediateRequestNotification returned null task."));
            FwoNotification? notification = await task;

            Assert.That(notification, Is.Null);
        }

        [Test]
        public async Task RequestInterfacePopup_SendEmail_DisplaysMissingNotificationErrorWhenNoImmediateNotificationExists()
        {
            using BunitContext context = CreateContext(new RequestPopupNoImmediateApiConn(), Roles.Modeller);
            SimulatedUserConfig userConfig = (SimulatedUserConfig)context.Services.GetRequiredService<UserConfig>();
            userConfig.UiHostName = "https://fwo.example";
            IRenderedComponent<RequestInterfacePopup> component = RenderRequestInterfacePopup(
                context,
                new FwoOwner { Id = 11, Name = "Selected", ExtAppId = "APP-42" },
                new FwoOwner { Id = 12, Name = "Requester" },
                messageSink: null);
            List<(Exception? Exception, string Title, string Message, bool IsError)> messages = [];
            SetPrivateMember(component.Instance, "DisplayMessageInUi", (Action<Exception?, string, string, bool>)((exception, title, message, isError) => messages.Add((exception, title, message, isError))));
            SetPrivateMember(component.Instance, "middlewareClient", null);

            MethodInfo sendEmail = typeof(RequestInterfacePopup).GetMethod("SendEmail", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("SendEmail method not found.");
            Task task = (Task)(sendEmail.Invoke(component.Instance, new object?[] { 123L }) ?? throw new InvalidOperationException("SendEmail returned null task."));
            await task;

            Assert.Multiple(() =>
            {
                Assert.That(messages, Has.Count.EqualTo(1));
                Assert.That(messages[0].Title, Is.EqualTo(userConfig.GetText("send_email")));
                Assert.That(messages[0].Message, Is.EqualTo(userConfig.GetText("E9011")));
                Assert.That(messages[0].IsError, Is.True);
            });
        }

        [Test]
        public async Task RequestInterfacePopup_SendEmail_DisplaysMissingRecipientErrorWhenNotificationHasNoRecipients()
        {
            using BunitContext context = CreateContext(new RequestPopupNoImmediateApiConn(), Roles.Modeller);
            SimulatedUserConfig userConfig = (SimulatedUserConfig)context.Services.GetRequiredService<UserConfig>();
            userConfig.UiHostName = "https://fwo.example";
            IRenderedComponent<RequestInterfacePopup> component = RenderRequestInterfacePopup(
                context,
                new FwoOwner { Id = 11, Name = "Selected", ExtAppId = "APP-42" },
                new FwoOwner { Id = 12, Name = "Requester" },
                messageSink: null);
            List<(Exception? Exception, string Title, string Message, bool IsError)> messages = [];
            SetPrivateMember(component.Instance, "DisplayMessageInUi", (Action<Exception?, string, string, bool>)((exception, title, message, isError) => messages.Add((exception, title, message, isError))));
            SetPrivateMember(component.Instance, "middlewareClient", null);

            RequestPopupNoRecipientsApiConn apiConn = new();
            SetPrivateMember(component.Instance, "apiConnection", apiConn);

            MethodInfo sendEmail = typeof(RequestInterfacePopup).GetMethod("SendEmail", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("SendEmail method not found.");
            Task task = (Task)(sendEmail.Invoke(component.Instance, new object?[] { 123L }) ?? throw new InvalidOperationException("SendEmail returned null task."));
            await task;

            Assert.Multiple(() =>
            {
                Assert.That(messages, Has.Count.EqualTo(1));
                Assert.That(messages[0].Title, Is.EqualTo(userConfig.GetText("send_email")));
                Assert.That(messages[0].Message, Is.EqualTo(userConfig.GetText("E9011")));
                Assert.That(messages[0].IsError, Is.True);
            });
        }

        [Test]
        public async Task RequestInterfacePopup_BuildRequestPlaceholderValues_UsesCurrentRequestState()
        {
            using BunitContext context = CreateContext(new RequestPopupNotificationApiConn(), Roles.Modeller);
            SimulatedUserConfig userConfig = (SimulatedUserConfig)context.Services.GetRequiredService<UserConfig>();
            userConfig.UiHostName = "https://fwo.example";
            userConfig.ModReqInterfaceName = "req-interface";
            FwoOwner selectedApp = new() { Id = 11, Name = "Selected", ExtAppId = "APP-42" };
            FwoOwner requestingOwner = new() { Id = 12, Name = "Requester" };

            IRenderedComponent<RequestInterfacePopup> component = RenderRequestInterfacePopup(
                context,
                selectedApp,
                requestingOwner);

            component.Find("input[type='text']").Change("branch-if");
            component.Find("textarea").Change("needed");

            MethodInfo buildPlaceholderValues = typeof(RequestInterfacePopup).GetMethod("BuildRequestPlaceholderValues", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("BuildRequestPlaceholderValues method not found.");
            NotificationPlaceholderResolver.NotificationPlaceholderValues values = (NotificationPlaceholderResolver.NotificationPlaceholderValues)(buildPlaceholderValues.Invoke(component.Instance, new object?[] { 123L })
                ?? throw new InvalidOperationException("BuildRequestPlaceholderValues returned null."));

            Assert.Multiple(() =>
            {
                Assert.That(values.Application, Is.SameAs(selectedApp));
                Assert.That(values.RequestingOwner, Is.SameAs(requestingOwner));
                Assert.That(values.InterfaceName, Is.EqualTo("branch-if"));
                Assert.That(values.InterfaceLinkText, Is.EqualTo(userConfig.GetText("request_interface")));
                Assert.That(values.InterfaceLinkUrl, Is.EqualTo("https://fwo.example/networkmodelling/APP-42/123"));
                Assert.That(values.NewInterfaceName, Is.EqualTo("branch-if"));
                Assert.That(values.NewInterfaceLinkText, Is.EqualTo(userConfig.GetText("request_interface")));
                Assert.That(values.NewInterfaceLinkUrl, Is.EqualTo("https://fwo.example/networkmodelling/APP-42/123"));
                Assert.That(values.RequesterName, Is.EqualTo(userConfig.User.Name));
                Assert.That(values.UserName, Is.EqualTo(userConfig.User.Name));
                Assert.That(values.RequestDate, Is.EqualTo(DateTime.Now.ToString("dd.MM.yyyy")));
            });
        }

        [Test]
        public async Task RequestInterfacePopup_SendRequest_StopsWhenAlreadyInProgress()
        {
            using BunitContext context = CreateContext(new RequestPopupNotificationApiConn(), Roles.Modeller);
            IRenderedComponent<RequestInterfacePopup> component = RenderRequestInterfacePopup(
                context,
                new FwoOwner { Id = 11, Name = "Selected", ExtAppId = "APP-42" },
                new FwoOwner { Id = 12, Name = "Requester" });
            SetPrivateMember(component.Instance, "WorkInProgress", true);

            MethodInfo sendRequest = typeof(RequestInterfacePopup).GetMethod("SendRequest", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("SendRequest method not found.");
            Task task = (Task)(sendRequest.Invoke(component.Instance, Array.Empty<object?>()) ?? throw new InvalidOperationException("SendRequest returned null task."));
            await task;

            Assert.That(component.Instance.Display, Is.True);
        }

        [Test]
        public void RequestInterfacePopup_Close_HidesPopup()
        {
            using BunitContext context = CreateContext(Roles.Modeller);
            IRenderedComponent<RequestInterfacePopup> component = RenderRequestInterfacePopup(
                context,
                new FwoOwner { Id = 11, Name = "Selected", ExtAppId = "APP-42" },
                new FwoOwner { Id = 12, Name = "Requester" });

            MethodInfo close = typeof(RequestInterfacePopup).GetMethod("Close", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Close method not found.");
            close.Invoke(component.Instance, Array.Empty<object?>());

            Assert.That(component.Instance.Display, Is.False);
        }

        [Test]
        public void RequestInterfacePopup_BuildRequestEmailSubjectAndBody_ReplacesPlaceholders()
        {
            using BunitContext context = CreateContext(Roles.Modeller);
            SimulatedUserConfig userConfig = (SimulatedUserConfig)context.Services.GetRequiredService<UserConfig>();
            userConfig.UiHostName = "https://fwo.example";
            userConfig.ModReqInterfaceName = "req-interface";
            FwoOwner selectedApp = new() { Id = 11, Name = "Selected", ExtAppId = "APP-42" };
            FwoOwner requestingOwner = new() { Id = 12, Name = "Requester" };

            IRenderedComponent<RequestInterfacePopup> component = RenderRequestInterfacePopup(
                context,
                selectedApp,
                requestingOwner);

            component.Find("input[type='text']").Change("branch-if");
            component.Find("textarea").Change("needed");

            FwoNotification notification = new()
            {
                EmailSubject = $"{REQUESTER}/{APPNAME}/{INTERFACE_LINK}",
                EmailBody = $"Body:{REQUESTER}/{APPNAME}/{INTERFACE_NAME}/{INTERFACE_LINK}"
            };
            NotificationPlaceholderResolver.NotificationPlaceholderValues values =
                (NotificationPlaceholderResolver.NotificationPlaceholderValues)(typeof(RequestInterfacePopup)
                    .GetMethod("BuildRequestPlaceholderValues", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.Invoke(component.Instance, new object?[] { 123L })
                    ?? throw new InvalidOperationException("BuildRequestPlaceholderValues method not found."));

            string subject = (string)(typeof(RequestInterfacePopup)
                .GetMethod("BuildRequestEmailSubject", BindingFlags.Static | BindingFlags.NonPublic)
                ?.Invoke(null, new object?[] { notification, values })
                ?? throw new InvalidOperationException("BuildRequestEmailSubject method not found."));
            string body = (string)(typeof(RequestInterfacePopup)
                .GetMethod("BuildRequestEmailBody", BindingFlags.Static | BindingFlags.NonPublic)
                ?.Invoke(null, new object?[] { notification, values })
                ?? throw new InvalidOperationException("BuildRequestEmailBody method not found."));

            Assert.Multiple(() =>
            {
                Assert.That(subject, Is.EqualTo($"{userConfig.User.Name}/Selected/https://fwo.example/networkmodelling/APP-42/123"));
                Assert.That(body, Is.EqualTo($"Body:{userConfig.User.Name}/Selected/branch-if/<a target=\"_blank\" href=\"https://fwo.example/networkmodelling/APP-42/123\">Request Interface: branch-if</a>"));
            });
        }

        [Test]
        public void RejectInterfacePopup_OnParametersSet_SetsMessageAndAdminReason()
        {
            using BunitContext context = CreateContext(Roles.Admin, Roles.Modeller);
            SimulatedUserConfig userConfig = (SimulatedUserConfig)context.Services.GetRequiredService<UserConfig>();
            userConfig.User.Roles = [Roles.Admin, Roles.Modeller];
            userConfig.SetExecutionMode(Roles.Admin);
            ModellingConnectionHandler handler = CreateConnectionHandler(
                new RejectInterfacePopupTestApiConn(),
                userConfig,
                new ModellingConnection { Id = 21, Name = "iface21", Reason = "reason", IsInterface = true });

            IRenderedComponent<RejectInterfacePopup> component = RenderRejectInterfacePopup(
                context,
                handler,
                allowAdminReject: true);

            Assert.Multiple(() =>
            {
                Assert.That(component.Markup, Does.Contain(userConfig.GetText("reject_interface")));
                Assert.That(component.Markup, Does.Contain(userConfig.GetText("U9017") + "iface21?"));
                Assert.That(component.Find("textarea").GetAttribute("value"), Is.EqualTo(userConfig.GetText("U9036")));
            });
        }

        [Test]
        public void RejectInterfacePopup_BuildsNotificationContextWithRejectReason()
        {
            using BunitContext context = CreateContext(Roles.Admin, Roles.Modeller);
            SimulatedUserConfig userConfig = (SimulatedUserConfig)context.Services.GetRequiredService<UserConfig>();
            userConfig.UiHostName = "https://fwo.example";
            userConfig.User.Roles = [Roles.Admin, Roles.Modeller];
            userConfig.SetExecutionMode(Roles.Admin);
            ModellingConnection actConn = new()
            {
                Id = 21,
                Name = "iface21",
                App = new FwoOwner { Name = "Requesting App", ExtAppId = "REQ-APP" },
                CreationDate = new DateTime(2025, 1, 2),
                IsInterface = true
            };
            ModellingConnectionHandler handler = CreateConnectionHandler(new RejectInterfacePopupTestApiConn(), userConfig, actConn);
            handler.Application.ExtAppId = "OWNER-APP";
            IRenderedComponent<RejectInterfacePopup> component = RenderRejectInterfacePopup(
                context, handler, allowAdminReject: true);

            component.Find("textarea").Change("not approved");
            MethodInfo method = typeof(RejectInterfacePopup).GetMethod("BuildNotificationPlaceholderData", BindingFlags.Instance | BindingFlags.NonPublic)!;
            NotificationPlaceholderData data = (NotificationPlaceholderData)method.Invoke(component.Instance, null)!;

            Assert.Multiple(() =>
            {
                Assert.That(data.InterfaceName, Is.EqualTo("iface21"));
                Assert.That(data.RequestingAppName, Is.EqualTo("Requesting App"));
                Assert.That(data.RequestingAppId, Is.EqualTo("REQ-APP"));
                Assert.That(data.Content, Is.EqualTo("not approved"));
                Assert.That(data.RequestDate, Is.EqualTo("02.01.2025"));
                Assert.That(data.InterfaceLinkUrl, Is.EqualTo("https://fwo.example/networkmodelling/OWNER-APP/21"));
            });
        }

        [Test]
        public async Task RejectInterfacePopup_Reject_SavesPropertiesAndRemovesSelection()
        {
            using BunitContext context = CreateContext(Roles.Admin, Roles.Modeller);
            SimulatedUserConfig userConfig = (SimulatedUserConfig)context.Services.GetRequiredService<UserConfig>();
            userConfig.User.Roles = [Roles.Admin, Roles.Modeller];
            userConfig.SetExecutionMode(Roles.Admin);
            RejectInterfacePopupTestApiConn apiConn = new();
            ModellingConnection actConn = new()
            {
                Id = 21,
                Name = "iface21",
                Reason = "reason",
                IsInterface = true
            };
            ModellingConnectionHandler handler = CreateConnectionHandler(apiConn, userConfig, actConn);
            bool displayChanged = true;
            int refreshCalls = 0;

            IRenderedComponent<RejectInterfacePopup> component = RenderRejectInterfacePopup(
                context,
                handler,
                allowAdminReject: true,
                displayChanged: value => displayChanged = value,
                refreshParent: () =>
                {
                    refreshCalls++;
                    return Task.CompletedTask;
                });

            component.Find("textarea").Change("planned removal");
            await component.InvokeAsync(() => component.FindAll("button.btn-primary").Single().Click());

            Assert.Multiple(() =>
            {
                Assert.That(apiConn.UpdateConnectionPropertiesCalls, Is.EqualTo(1));
                Assert.That(apiConn.RemoveSelectedConnectionCalls, Is.EqualTo(1));
                Assert.That(refreshCalls, Is.EqualTo(1));
                Assert.That(displayChanged, Is.False);
                Assert.That(component.Instance.Display, Is.False);
                Assert.That(handler.ActConn.GetBoolProperty(ConState.Rejected.ToString()), Is.True);
            });
        }

        private static BunitContext CreateContext(params string[] roles)
        {
            return CreateContext(null, roles);
        }

        private static BunitContext CreateContext(ApiConnection? apiConnection, params string[] roles)
        {
            BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddLocalization();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<AuthenticationStateProvider>(new PopupAuthStateProvider(roles));
            context.Services.AddSingleton<ApiConnection>(apiConnection ?? new SimulatedApiConnection());
            context.Services.AddSingleton(new MiddlewareClient("http://localhost/"));
            context.Services.AddSingleton<UserConfig>(CreateUserConfig());
            return context;
        }

        private static SimulatedUserConfig CreateUserConfig()
        {
            return new SimulatedUserConfig
            {
                ModNamingConvention = "{}",
                ModReqInterfaceName = "req-interface",
                User = { Roles = [Roles.Modeller, Roles.Admin], Name = "tester" }
            };
        }

        private static ModellingConnectionHandler CreateConnectionHandler(
            ApiConnection apiConn,
            SimulatedUserConfig userConfig,
            ModellingConnection actConn)
        {
            return new ModellingConnectionHandler(
                apiConn,
                userConfig,
                new FwoOwner { Id = 77, Name = "owner" },
                [actConn],
                actConn,
                addMode: false,
                readOnly: false,
                displayMessageInUi: (_, _, _, _) => { },
                refreshParent: () => Task.CompletedTask,
                isOwner: true);
        }

        private static IRenderedComponent<RequestInterfacePopup> RenderRequestInterfacePopup(
            BunitContext context,
            FwoOwner selectedApp,
            FwoOwner requestingOwner,
            Action<Exception?, string, string, bool>? messageSink = null)
        {
            RenderFragment fragment = builder =>
            {
                builder.OpenComponent<CascadingValue<Action<Exception?, string, string, bool>>>(0);
                builder.AddAttribute(1, "Value", messageSink ?? ((_, _, _, _) => { }));
                builder.AddAttribute(2, "IsFixed", true);
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(childBuilder =>
                {
                    childBuilder.OpenComponent<CascadingAuthenticationState>(0);
                    childBuilder.AddAttribute(1, "ChildContent", (RenderFragment)(popupBuilder =>
                    {
                        popupBuilder.OpenComponent<RequestInterfacePopup>(0);
                        popupBuilder.AddAttribute(1, "Display", true);
                        popupBuilder.AddAttribute(2, "Apps", new List<FwoOwner> { selectedApp });
                        popupBuilder.AddAttribute(3, "RequestingOwner", requestingOwner);
                        popupBuilder.AddAttribute(4, "RefreshParent", (Func<Task>)(() => Task.CompletedTask));
                        popupBuilder.CloseComponent();
                    }));
                    childBuilder.CloseComponent();
                }));
                builder.CloseComponent();
            };

            return context.Render(fragment).FindComponent<RequestInterfacePopup>();
        }

        private static IRenderedComponent<RejectInterfacePopup> RenderRejectInterfacePopup(
            BunitContext context,
            ModellingConnectionHandler handler,
            bool allowAdminReject = false,
            Action<bool>? displayChanged = null,
            Func<Task>? refreshParent = null,
            Action<Exception?, string, string, bool>? messageSink = null)
        {
            RenderFragment fragment = builder =>
            {
                builder.OpenComponent<CascadingValue<Action<Exception?, string, string, bool>>>(0);
                builder.AddAttribute(1, "Value", messageSink ?? ((_, _, _, _) => { }));
                builder.AddAttribute(2, "IsFixed", true);
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(childBuilder =>
                {
                    childBuilder.OpenComponent<CascadingAuthenticationState>(0);
                    childBuilder.AddAttribute(1, "ChildContent", (RenderFragment)(popupBuilder =>
                    {
                        popupBuilder.OpenComponent<RejectInterfacePopup>(0);
                        popupBuilder.AddAttribute(1, "Display", true);
                        popupBuilder.AddAttribute(2, "DisplayChanged", EventCallback.Factory.Create<bool>(context, value => displayChanged?.Invoke(value)));
                        popupBuilder.AddAttribute(3, "ConnHandler", handler);
                        popupBuilder.AddAttribute(4, "RefreshParent", refreshParent ?? (() => Task.CompletedTask));
                        popupBuilder.AddAttribute(5, "AllowAdminReject", allowAdminReject);
                        popupBuilder.CloseComponent();
                    }));
                    childBuilder.CloseComponent();
                }));
                builder.CloseComponent();
            };

            return context.Render(fragment).FindComponent<RejectInterfacePopup>();
        }

        private static void SetPrivateMember(object target, string memberName, object? value)
        {
            FieldInfo? field = target.GetType().GetField(memberName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field != null)
            {
                field.SetValue(target, value);
                return;
            }

            PropertyInfo? property = target.GetType().GetProperty(memberName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (property != null)
            {
                property.SetValue(target, value);
                return;
            }

            throw new MissingMemberException(target.GetType().FullName, memberName);
        }

        private sealed class RequestPopupNotificationApiConn : SimulatedApiConnection
        {
            public override async Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                await Task.CompletedTask;
                if (typeof(QueryResponseType) == typeof(List<FwoNotification>) && query == NotificationQueries.getNotifications)
                {
                    GraphQLResponse<dynamic> response = new()
                    {
                        Data = new List<FwoNotification>
                        {
                            new()
                            {
                                Id = 1,
                                Deadline = NotificationDeadline.RequestDate,
                                EmailSubject = "request-subject"
                            },
                            new()
                            {
                                Id = 2,
                                Deadline = NotificationDeadline.None,
                                EmailSubject = "immediate-subject"
                            }
                        }
                    };
                    return response.Data;
                }

                if (typeof(QueryResponseType) == typeof(List<OwnerResponsibleType>) && query == OwnerQueries.getOwnerResponsibleTypes)
                {
                    return (QueryResponseType)(object)new List<OwnerResponsibleType>
                    {
                        new() { Id = GlobalConst.kOwnerResponsibleTypeMain, Name = "Main", Active = true, SortOrder = 10 }
                    };
                }

                if (typeof(QueryResponseType) == typeof(List<UiUser>) && query == AuthQueries.getUserEmails)
                {
                    return (QueryResponseType)(object)new List<UiUser>
                    {
                        new() { Dn = "cn=requester,dc=test", Email = "requester@example.test" }
                    };
                }

                throw new AssertionException($"Unexpected query: {query}");
            }
        }

        private sealed class RequestPopupNoImmediateApiConn : SimulatedApiConnection
        {
            public override async Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                await Task.CompletedTask;
                if (typeof(QueryResponseType) == typeof(List<FwoNotification>) && query == NotificationQueries.getNotifications)
                {
                    GraphQLResponse<dynamic> response = new()
                    {
                        Data = new List<FwoNotification>
                        {
                            new()
                            {
                                Id = 1,
                                Deadline = NotificationDeadline.RequestDate,
                                EmailSubject = "request-subject"
                            }
                        }
                    };
                    return response.Data;
                }

                if (typeof(QueryResponseType) == typeof(List<OwnerResponsibleType>) && query == OwnerQueries.getOwnerResponsibleTypes)
                {
                    return (QueryResponseType)(object)new List<OwnerResponsibleType>
                    {
                        new() { Id = GlobalConst.kOwnerResponsibleTypeMain, Name = "Main", Active = true, SortOrder = 10 }
                    };
                }

                if (typeof(QueryResponseType) == typeof(List<UiUser>) && query == AuthQueries.getUserEmails)
                {
                    return (QueryResponseType)(object)new List<UiUser>
                    {
                        new() { Dn = "cn=requester,dc=test", Email = "requester@example.test" }
                    };
                }

                throw new AssertionException($"Unexpected query: {query}");
            }
        }

        private sealed class RequestPopupNoRecipientsApiConn : SimulatedApiConnection
        {
            public override async Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                await Task.CompletedTask;
                if (typeof(QueryResponseType) == typeof(List<FwoNotification>) && query == NotificationQueries.getNotifications)
                {
                    GraphQLResponse<dynamic> response = new()
                    {
                        Data = new List<FwoNotification>
                        {
                            new()
                            {
                                Id = 2,
                                Deadline = NotificationDeadline.None,
                                RecipientTo = EmailRecipientOption.OtherAddresses,
                                EmailAddressTo = ""
                            }
                        }
                    };
                    return response.Data;
                }

                if (typeof(QueryResponseType) == typeof(List<OwnerResponsibleType>) && query == OwnerQueries.getOwnerResponsibleTypes)
                {
                    return (QueryResponseType)(object)new List<OwnerResponsibleType>
                    {
                        new() { Id = GlobalConst.kOwnerResponsibleTypeMain, Name = "Main", Active = true, SortOrder = 10 }
                    };
                }

                if (typeof(QueryResponseType) == typeof(List<UiUser>) && query == AuthQueries.getUserEmails)
                {
                    return (QueryResponseType)(object)new List<UiUser>
                    {
                        new() { Dn = "cn=requester,dc=test", Email = "requester@example.test" }
                    };
                }

                throw new AssertionException($"Unexpected query: {query}");
            }
        }

        private sealed class PopupAuthStateProvider : AuthenticationStateProvider
        {
            private readonly ClaimsPrincipal principal;

            public PopupAuthStateProvider(params string[] roles)
            {
                principal = new ClaimsPrincipal(new ClaimsIdentity(
                    roles.Select(role => new Claim(ClaimTypes.Role, role)),
                    "Test",
                    ClaimTypes.Name,
                    ClaimTypes.Role));
            }

            public override Task<AuthenticationState> GetAuthenticationStateAsync()
            {
                return Task.FromResult(new AuthenticationState(principal));
            }
        }

        private sealed class RejectInterfacePopupTestApiConn : SimulatedApiConnection
        {
            public int UpdateConnectionPropertiesCalls { get; private set; }
            public int RemoveSelectedConnectionCalls { get; private set; }

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                if (query == ModellingQueries.updateConnectionProperties)
                {
                    UpdateConnectionPropertiesCalls++;
                    return Task.FromResult((QueryResponseType)(object)new ReturnId { AffectedRows = 1 });
                }
                if (query == ModellingQueries.removeSelectedConnection)
                {
                    RemoveSelectedConnectionCalls++;
                    return Task.FromResult((QueryResponseType)(object)new ReturnId { AffectedRows = 1 });
                }
                throw new AssertionException($"Unexpected query: {query}");
            }
        }
    }
}
