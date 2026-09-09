using AngleSharp.Dom;
using Bunit;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Config.Api.Data;
using FWO.Data;
using FWO.Ui.Pages.Settings;
using FWO.Ui.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Linq;
using System.Reflection;

namespace FWO.Test
{
    [TestFixture]
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    internal class UiSettingsModellingNotificationsTest
    {
        [Test]
        public async Task Page_RendersThreeNotificationEditorsWithExpectedSections()
        {
            await using BunitContext context = CreateContext();

            IRenderedComponent<CascadingAuthenticationState> wrapper = RenderPage(context);
            IRenderedComponent<SettingsModellingNotifications> page = wrapper.FindComponent<SettingsModellingNotifications>();

            page.WaitForAssertion(() =>
            {
                Assert.That(page.Markup, Does.Contain("Notifications"));
                Assert.That(page.Markup, Does.Contain("Initial request"));
                Assert.That(page.Markup, Does.Contain("Reminder"));
                Assert.That(page.Markup, Does.Contain("Decommission"));

                List<IRenderedComponent<EditNotifications>> editors = page.FindComponents<EditNotifications>().ToList();
                Assert.That(editors, Has.Count.EqualTo(3));
                Assert.That(editors[0].Instance.Client, Is.EqualTo(NotificationClient.InterfaceRequest));
                Assert.That(editors[0].Instance.DeadlineFilter, Is.EqualTo(NotificationDeadline.None));
                Assert.That(editors[1].Instance.Client, Is.EqualTo(NotificationClient.InterfaceRequest));
                Assert.That(editors[1].Instance.DeadlineFilter, Is.EqualTo(NotificationDeadline.RequestDate));
                Assert.That(editors[2].Instance.Client, Is.EqualTo(NotificationClient.InterfaceDecomm));
                Assert.That(editors[2].Instance.DeadlineFilter, Is.EqualTo(NotificationDeadline.None));
            });
        }

        [Test]
        public async Task Save_WritesChangedConfigData()
        {
            await using BunitContext context = CreateContext();
            IRenderedComponent<CascadingAuthenticationState> wrapper = RenderPage(context);
            IRenderedComponent<SettingsModellingNotifications> page = wrapper.FindComponent<SettingsModellingNotifications>();

            page.WaitForAssertion(() => Assert.That(page.FindAll("form"), Has.Count.EqualTo(1)));

            IElement interfaceNameInput = page.FindAll("label")
                .First(label => label.TextContent.Contains("Name of requested interface"))
                .ParentElement!
                .QuerySelector("input")!;
            interfaceNameInput.Change("New interface name");

            IElement saveButton = page.FindAll("button")
                .Last(button => button.QuerySelector("span[title='Save']") != null);
            saveButton.Click();

            page.WaitForAssertion(() =>
            {
                SettingsModellingNotificationsApiConn apiConnection = context.Services.GetRequiredService<ApiConnection>() as SettingsModellingNotificationsApiConn
                    ?? throw new InvalidOperationException("Test api connection missing.");
                Assert.That(apiConnection.UpsertConfigCallCount, Is.EqualTo(1));
            });

            SettingsModellingNotificationsApiConn apiConn = context.Services.GetRequiredService<ApiConnection>() as SettingsModellingNotificationsApiConn
                ?? throw new InvalidOperationException("Test api connection missing.");
            Assert.That(apiConn.LastUpsertConfigItems, Has.Count.EqualTo(1));
            Assert.That(apiConn.LastUpsertConfigItems[0].Key, Is.EqualTo("modReqInterfaceName"));
            Assert.That(apiConn.LastUpsertConfigItems[0].Value, Is.EqualTo("New interface name"));
        }

        [Test]
        public async Task Page_ShowsLoadingAndReportsErrorWhenGlobalConfigIsUnavailable()
        {
            await using BunitContext context = CreateContext(disposeGlobalConfig: true);
            List<(Exception? Exception, string Title, string Message, bool IsError)> messages = [];

            IRenderedComponent<CascadingAuthenticationState> wrapper = RenderPage(context, (exception, title, message, isError) => messages.Add((exception, title, message, isError)));
            IRenderedComponent<SettingsModellingNotifications> page = wrapper.FindComponent<SettingsModellingNotifications>();

            page.WaitForAssertion(() =>
            {
                Assert.That(messages, Has.Count.EqualTo(1));
                Assert.That(messages[0].Title, Is.EqualTo(new SimulatedUserConfig().GetText("read_config")));
                Assert.That(messages[0].Message, Is.EqualTo(new SimulatedUserConfig().GetText("E5301")));
                Assert.That(messages[0].IsError, Is.False);
                Assert.That(page.FindAll("div[role='status']"), Has.Count.EqualTo(1));
            });
        }

        [Test]
        public async Task Save_ReportsErrorWhenConfigWriteFails()
        {
            await using BunitContext context = CreateContext(throwOnUpsert: true);
            List<(Exception? Exception, string Title, string Message, bool IsError)> messages = [];

            IRenderedComponent<CascadingAuthenticationState> wrapper = RenderPage(context, (exception, title, message, isError) => messages.Add((exception, title, message, isError)));
            IRenderedComponent<SettingsModellingNotifications> page = wrapper.FindComponent<SettingsModellingNotifications>();

            page.WaitForAssertion(() => Assert.That(page.FindAll("form"), Has.Count.EqualTo(1)));

            IElement interfaceNameInput = page.FindAll("label")
                .First(label => label.TextContent.Contains("Name of requested interface"))
                .ParentElement!
                .QuerySelector("input")!;
            interfaceNameInput.Change("New interface name");

            IElement saveButton = page.FindAll("button")
                .Last(button => button.QuerySelector("span[title='Save']") != null);
            saveButton.Click();

            page.WaitForAssertion(() =>
            {
                Assert.That(messages, Has.Count.EqualTo(1));
                Assert.That(messages[0].Title, Is.EqualTo(new SimulatedUserConfig().GetText("interfaces")));
                Assert.That(messages[0].IsError, Is.True);
            });
        }

        private static BunitContext CreateContext(bool disposeGlobalConfig = false, bool throwOnUpsert = false)
        {
            SimulatedGlobalConfig globalConfig = new()
            {
                ModReqInterfaceName = "Old interface name",
                ModReqTicketTitle = "Old ticket title",
                ModReqTaskTitle = "Old task title"
            };
            if (disposeGlobalConfig)
            {
                globalConfig.Dispose();
            }

            SettingsModellingNotificationsApiConn apiConnection = new()
            {
                ThrowOnUpsert = throwOnUpsert,
                OwnerResponsibleTypes =
                [
                    new() { Id = 1, Active = true, Name = "Main", SortOrder = 1 },
                    new() { Id = 2, Active = true, Name = "Supporting", SortOrder = 2 }
                ]
            };

            SimulatedUserConfig userConfig = new();
            userConfig.User.Roles = [Roles.Admin];
            userConfig.SetExecutionMode(Roles.Admin);

            BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddLocalization();
            context.Services.AddAuthorizationCore();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<AuthenticationStateProvider>(new AllowAllAuthStateProvider(Roles.Admin));
            context.Services.AddSingleton<ApiConnection>(apiConnection);
            context.Services.AddSingleton<GlobalConfig>(globalConfig);
            context.Services.AddSingleton<UserConfig>(userConfig);
            context.Services.AddSingleton(typeof(IStringLocalizer<>), typeof(EmptyStringLocalizer<>));
            return context;
        }

        private static IRenderedComponent<CascadingAuthenticationState> RenderPage(
            BunitContext context,
            Action<Exception?, string, string, bool>? messageSink = null)
        {
            return context.Render<CascadingAuthenticationState>(parameters => parameters
                .AddChildContent(builder =>
                {
                    builder.OpenComponent<CascadingValue<Action<Exception?, string, string, bool>>>(0);
                    builder.AddAttribute(1, "Value", messageSink ?? ((_, _, _, _) => { }));
                    builder.AddAttribute(2, "IsFixed", true);
                    builder.AddAttribute(3, "ChildContent", (RenderFragment)(childBuilder =>
                    {
                        childBuilder.OpenComponent<SettingsModellingNotifications>(0);
                        childBuilder.CloseComponent();
                    }));
                    builder.CloseComponent();
                }));
        }

        private sealed class SettingsModellingNotificationsApiConn : NotificationTestApiConn
        {
            public List<OwnerResponsibleType> OwnerResponsibleTypes { get; init; } = [];
            public int UpsertConfigCallCount { get; private set; }
            public List<ConfigItem> LastUpsertConfigItems { get; private set; } = [];
            public bool ThrowOnUpsert { get; init; }

            public override async Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                if (query == OwnerQueries.getOwnerResponsibleTypes && typeof(QueryResponseType) == typeof(List<OwnerResponsibleType>))
                {
                    return (QueryResponseType)(object)OwnerResponsibleTypes;
                }

                if (query == ConfigQueries.upsertConfigItems)
                {
                    UpsertConfigCallCount++;
                    if (ThrowOnUpsert)
                    {
                        throw new InvalidOperationException("Config write failed.");
                    }
                    if (variables != null)
                    {
                        PropertyInfo? configItemsProperty = variables.GetType().GetProperty("config_items");
                        LastUpsertConfigItems = configItemsProperty == null
                            ? []
                            : ((IEnumerable<ConfigItem>)configItemsProperty.GetValue(variables)!).ToList();
                    }

                    return default!;
                }

                return await base.SendQueryAsync<QueryResponseType>(query, variables, operationName, chunkingOptions);
            }
        }

        private sealed class EmptyStringLocalizer<T> : IStringLocalizer<T>
        {
            public LocalizedString this[string name] => new(name, name, resourceNotFound: true);

            public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(name, arguments), resourceNotFound: true);

            public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];

            public IStringLocalizer WithCulture(System.Globalization.CultureInfo culture) => this;
        }
    }
}
