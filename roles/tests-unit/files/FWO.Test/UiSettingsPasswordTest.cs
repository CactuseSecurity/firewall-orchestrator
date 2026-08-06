using Bunit;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Middleware.Client;
using FWO.Test.Mocks;
using FWO.Ui.Pages.Settings;
using FWO.Ui.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using RestSharp;
using System.Net;
using System.Reflection;

namespace FWO.Test
{
    [TestFixture]
    internal class UiSettingsPasswordTest
    {
        [SetUp]
        public void SetUp()
        {
            SimulatedUserConfig.DummyTranslate["change_password"] = "Change Password";
            SimulatedUserConfig.DummyTranslate["old_password"] = "Old Password";
            SimulatedUserConfig.DummyTranslate["new_password"] = "New Password";
            SimulatedUserConfig.DummyTranslate["U5401"] = "Password changed.";
            SimulatedUserConfig.DummyTranslate["U5411"] = "Change the password for the current user";
        }

        [Test]
        public async Task SettingsPassword_RendersEnabledButtonForAdmin()
        {
            await using BunitContext context = CreateContext(Roles.Admin, out _, out _, out _);

            IRenderedComponent<CascadingAuthenticationState> wrapper = RenderComponent(context);

            wrapper.WaitForAssertion(() =>
            {
                Assert.That(wrapper.FindAll("input[type='password']"), Has.Count.EqualTo(3));
                Assert.That(wrapper.Find("button.btn-primary").HasAttribute("disabled"), Is.False);
            });
        }

        [Test]
        public async Task SettingsPassword_RendersDisabledButtonForAuditor()
        {
            await using BunitContext context = CreateContext(Roles.Auditor, out _, out _, out _);

            IRenderedComponent<CascadingAuthenticationState> wrapper = RenderComponent(context);

            wrapper.WaitForAssertion(() =>
            {
                Assert.That(wrapper.FindAll("input[type='password']"), Has.Count.EqualTo(3));
                Assert.That(wrapper.Find("button.btn-primary").HasAttribute("disabled"), Is.True);
            });
        }

        [Test]
        public async Task ChangePassword_SucceedsAndShowsSuccessMessage()
        {
            List<(Exception? Exception, string Title, string Message, bool IsError)> messages = [];
            await using BunitContext context = CreateContext(Roles.Admin, out MockMiddlewareClient middlewareClient, out _, out _);
            SettingsPassword component = RenderComponent(context, (exception, title, message, isError) =>
            {
                messages.Add((exception, title, message, isError));
            }).FindComponent<SettingsPassword>().Instance;

            SetMember(component, "oldPassword", "OldPassword123!");
            SetMember(component, "newPassword1", "NewPassword123!");
            SetMember(component, "newPassword2", "NewPassword123!");

            await InvokePrivateTask(component, "ChangePassword");

            Assert.Multiple(() =>
            {
                Assert.That(middlewareClient.ChangePasswordCallCount, Is.EqualTo(1));
                Assert.That(messages, Has.Count.EqualTo(1));
                Assert.That(messages[0].Title, Is.EqualTo("Change Password"));
                Assert.That(messages[0].Message, Is.EqualTo("Password changed."));
                Assert.That(messages[0].IsError, Is.False);
            });
        }

        [Test]
        public async Task ChangePassword_ShowsMiddlewareErrorMessage()
        {
            List<(Exception? Exception, string Title, string Message, bool IsError)> messages = [];
            await using BunitContext context = CreateContext(Roles.Admin, out MockMiddlewareClient middlewareClient, out _, out _);
            middlewareClient.ChangePasswordResponse = new RestResponse<string>(new RestRequest())
            {
                StatusCode = HttpStatusCode.OK,
                Data = "Password rejected",
                ResponseStatus = ResponseStatus.Completed,
                IsSuccessStatusCode = true
            };
            SettingsPassword component = RenderComponent(context, (exception, title, message, isError) =>
            {
                messages.Add((exception, title, message, isError));
            }).FindComponent<SettingsPassword>().Instance;

            SetMember(component, "oldPassword", "OldPassword123!");
            SetMember(component, "newPassword1", "NewPassword123!");
            SetMember(component, "newPassword2", "NewPassword123!");

            await InvokePrivateTask(component, "ChangePassword");

            Assert.Multiple(() =>
            {
                Assert.That(middlewareClient.ChangePasswordCallCount, Is.EqualTo(1));
                Assert.That(messages, Has.Count.EqualTo(1));
                Assert.That(messages[0].Title, Is.EqualTo("Change Password"));
                Assert.That(messages[0].Message, Is.EqualTo("Password rejected"));
                Assert.That(messages[0].IsError, Is.True);
            });
        }

        [Test]
        public async Task ChangePassword_ShowsExceptionWhenMiddlewareThrows()
        {
            List<(Exception? Exception, string Title, string Message, bool IsError)> messages = [];
            await using BunitContext context = CreateContext(Roles.Admin, out MockMiddlewareClient middlewareClient, out _, out _);
            middlewareClient.ChangePasswordException = new InvalidOperationException("middleware unavailable");
            SettingsPassword component = RenderComponent(context, (exception, title, message, isError) =>
            {
                messages.Add((exception, title, message, isError));
            }).FindComponent<SettingsPassword>().Instance;

            SetMember(component, "oldPassword", "OldPassword123!");
            SetMember(component, "newPassword1", "NewPassword123!");
            SetMember(component, "newPassword2", "NewPassword123!");

            await InvokePrivateTask(component, "ChangePassword");

            Assert.Multiple(() =>
            {
                Assert.That(middlewareClient.ChangePasswordCallCount, Is.EqualTo(1));
                Assert.That(messages, Has.Count.EqualTo(1));
                Assert.That(messages[0].Title, Is.EqualTo("Change Password"));
                Assert.That(messages[0].Message, Is.EqualTo("middleware unavailable"));
                Assert.That(messages[0].Exception, Is.Null);
                Assert.That(messages[0].IsError, Is.True);
            });
        }

        [Test]
        public async Task ChangePassword_DoesNothingForAuditor()
        {
            await using BunitContext context = CreateContext(Roles.Auditor, out MockMiddlewareClient middlewareClient, out _, out _);
            SettingsPassword component = RenderComponent(context).FindComponent<SettingsPassword>().Instance;

            SetMember(component, "oldPassword", "OldPassword123!");
            SetMember(component, "newPassword1", "NewPassword123!");
            SetMember(component, "newPassword2", "NewPassword123!");

            await InvokePrivateTask(component, "ChangePassword");

            Assert.That(middlewareClient.ChangePasswordCallCount, Is.EqualTo(0));
        }

        private static BunitContext CreateContext(
            string role,
            out MockMiddlewareClient middlewareClient,
            out SimulatedUserConfig userConfig,
            out SimulatedGlobalConfig globalConfig)
        {
            BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<AuthenticationStateProvider>(new AllowAllAuthStateProvider(role));
            context.Services.AddSingleton<DomEventService>();

            middlewareClient = new MockMiddlewareClient();
            userConfig = new SimulatedUserConfig();
            userConfig.User.Roles = new List<string> { role };
            globalConfig = new SimulatedGlobalConfig();

            context.Services.AddSingleton<MiddlewareClient>(middlewareClient);
            context.Services.AddSingleton<UserConfig>(userConfig);
            context.Services.AddSingleton<GlobalConfig>(globalConfig);

            return context;
        }

        private static IRenderedComponent<CascadingAuthenticationState> RenderComponent(
            BunitContext context,
            Action<Exception?, string, string, bool>? displayMessageInUi = null)
        {
            Action<Exception?, string, string, bool> callback = displayMessageInUi ?? ((_, _, _, _) => { });
            return context.Render<CascadingAuthenticationState>(parameters => parameters
                .AddChildContent<CascadingValue<Action<Exception?, string, string, bool>>>(child => child
                    .Add(p => p.Value, callback)
                    .AddChildContent<SettingsPassword>()));
        }

        private static void SetMember<T>(object instance, string memberName, T value)
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

            throw new MissingMemberException(type.FullName, memberName);
        }

        private static async Task InvokePrivateTask(object instance, string methodName, params object?[] args)
        {
            MethodInfo method = instance.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMethodException(instance.GetType().FullName, methodName);
            Task task = (Task)(method.Invoke(instance, args) ?? throw new InvalidOperationException($"{methodName} returned null task."));
            await task;
        }
    }
}
