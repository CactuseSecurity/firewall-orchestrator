using AngleSharp.Dom;
using Bunit;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Config.Api.Data;
using FWO.Data;
using FWO.Ui.Pages.Settings;
using FWO.Ui.Services;
using FWO.Ui.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using NUnit.Framework;
using System.Collections;
using System.Reflection;

namespace FWO.Test
{
    /// <summary>
    /// Renders the notification settings page: the form with its notification editors and initiator keys,
    /// the save button for admins and auditors, adding initiator keys, saving and the error paths.
    /// </summary>
    [TestFixture]
    [Parallelizable]
    internal class UiSettingsNotificationsRenderingTest
    {
        private static readonly object[] kJsonNullArgument = ["null"];
        private const string kInitiatorKeys = "{\"user\":\"Expiry by user\"}";

        [Test]
        public async Task Page_ForAdmin_ShowsFormWithInitiatorKeysEditorsAndEnabledSave()
        {
            await using PageContext context = new(Roles.Admin);

            IRenderedComponent<SettingsNotifications> page = context.Render();

            Assert.Multiple(() =>
            {
                Assert.That(page.FindComponents<EditNotifications>(), Has.Count.EqualTo(2));
                Assert.That(page.Markup, Does.Contain("Expiry by user"));
                Assert.That(page.Markup, Does.Contain(context.UserConfig.GetText("ruleExpiryEmailBody")));
                Assert.That(SaveButton(page).HasAttribute("disabled"), Is.False);
            });
        }

        [Test]
        public async Task LanguageDropdown_ListsGlobalDefaultAndUiLanguages()
        {
            await using PageContext context = new(Roles.Admin);
            IRenderedComponent<SettingsNotifications> page = context.Render();

            page.FindAll("input[id^='dropdown-input-']")[0].Focus();

            List<string> options = [.. page.FindAll("button.dropdown-item").Select(button => button.TextContent.Trim())];
            Assert.Multiple(() =>
            {
                Assert.That(options, Has.Some.EqualTo(context.UserConfig.GetText("useGlobalDefault")));
                Assert.That(options, Has.Some.EqualTo(context.UserConfig.GetText("English")));
            });
        }

        [Test]
        public async Task Page_ForAuditor_ShowsDisabledSave()
        {
            await using PageContext context = new(Roles.Auditor);

            IRenderedComponent<SettingsNotifications> page = context.Render();

            Assert.That(SaveButton(page).HasAttribute("disabled"), Is.True);
        }

        [Test]
        public async Task AddInitiatorKey_AddsEnteredKeyAndSaveStoresIt()
        {
            await using PageContext context = new(Roles.Admin);
            IRenderedComponent<SettingsNotifications> page = context.Render();

            page.Find($"input[placeholder='{context.UserConfig.GetText("key")}']").Change(" nsb ");
            page.Find($"input[placeholder='{context.UserConfig.GetText("text")}']").Change(" Expiry by NSB ");
            AddButton(page, context).Click();
            SaveButton(page).Click();

            Assert.Multiple(() =>
            {
                Assert.That(page.Markup, Does.Contain("Expiry by NSB"));
                Assert.That(context.ApiConnection.UpsertedItems.Any(item => item.Key == "ruleExpiryInitiatorKeys" && item.Value != null && item.Value.Contains("\"nsb\":\"Expiry by NSB\"")), Is.True);
                Assert.That(context.Messages.Single().Message, Is.EqualTo(context.UserConfig.GetText("U5301")));
            });
        }

        [Test]
        public async Task AddInitiatorKey_IgnoresBlankKey()
        {
            await using PageContext context = new(Roles.Admin);
            IRenderedComponent<SettingsNotifications> page = context.Render();

            page.Find($"input[placeholder='{context.UserConfig.GetText("text")}']").Change("Text without key");
            AddButton(page, context).Click();

            // the entered text stays in its input field, but no list entry is created
            Assert.That(page.FindAll("div.col-sm-7.border").Select(entry => entry.TextContent), Has.None.Contains("Text without key"));
        }

        [Test]
        public async Task Save_KeepsConfiguredConcreteNotificationLanguage()
        {
            await using PageContext context = new(Roles.Admin, notificationLanguage: "English");
            IRenderedComponent<SettingsNotifications> page = context.Render();

            SaveButton(page).Click();

            Assert.Multiple(() =>
            {
                Assert.That(context.ApiConnection.UpsertedItems.Any(item => item.Key == "notificationLanguage"), Is.False);
                Assert.That(context.Messages.Single().Exception, Is.Null);
            });
        }

        [Test]
        public async Task Save_ReportsErrorWhenWritingConfigFails()
        {
            await using PageContext context = new(Roles.Admin);
            context.ApiConnection.ThrowOnUpsert = true;
            IRenderedComponent<SettingsNotifications> page = context.Render();

            page.Find($"input[placeholder='{context.UserConfig.GetText("key")}']").Change("app");
            AddButton(page, context).Click();
            SaveButton(page).Click();

            Assert.Multiple(() =>
            {
                Assert.That(context.Messages.Single().Exception, Is.Not.Null);
                Assert.That(context.Messages.Single().IsError, Is.True);
            });
        }

        [Test]
        public async Task Initialization_ReportsErrorAndKeepsLoadingWhenConfigCannotBeRead()
        {
            await using PageContext context = new(Roles.Admin);
            context.GlobalConfig.Dispose();

            IRenderedComponent<SettingsNotifications> page = context.Render();

            Assert.Multiple(() =>
            {
                Assert.That(context.Messages.Single().Title, Is.EqualTo(context.UserConfig.GetText("read_config")));
                Assert.That(page.FindComponents<EditNotifications>(), Is.Empty);
            });
        }

        [Test]
        public async Task Save_ReportsErrorWhenCalledBeforeConfigIsLoaded()
        {
            SettingsNotifications component = new();
            List<(Exception? Exception, string Title, string Message, bool IsError)> messages = [];
            SetProperty(component, "DisplayMessageInUi", (Action<Exception?, string, string, bool>)((exception, title, message, isError) => messages.Add((exception, title, message, isError))));
            SetProperty(component, "userConfig", new SimulatedUserConfig());

            await (Task)typeof(SettingsNotifications).GetMethod("Save", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(component, null)!;

            Assert.That(messages.Single().Exception, Is.TypeOf<ArgumentException>());
        }

        [Test]
        public void ParseInitiatorKeys_ReturnsEmptyListForJsonNull()
        {
            MethodInfo parse = typeof(SettingsNotifications).GetMethod("ParseInitiatorKeys", BindingFlags.NonPublic | BindingFlags.Static)!;

            IList? entries = (IList?)parse.Invoke(null, kJsonNullArgument);

            Assert.That(entries, Is.Empty);
        }

        private static IElement SaveButton(IRenderedComponent<SettingsNotifications> page)
        {
            // the save button of the page shows the plain text, the buttons of the embedded editors show icons
            string saveText = new SimulatedUserConfig().GetText("save");
            return page.FindAll("button.btn-primary").Single(button => button.TextContent.Trim() == saveText);
        }

        private static IElement AddButton(IRenderedComponent<SettingsNotifications> page, PageContext context)
        {
            IElement keyInput = page.Find($"input[placeholder='{context.UserConfig.GetText("key")}']");
            return keyInput.ParentElement!.QuerySelector("button")!;
        }

        private static void SetProperty(object target, string name, object value)
        {
            PropertyInfo property = target.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMemberException(target.GetType().FullName, name);
            property.SetValue(target, value);
        }

        /// <summary>
        /// Render context of the page with a user of the given role, a recording API and a message sink.
        /// </summary>
        private sealed class PageContext : IAsyncDisposable
        {
            private readonly BunitContext context = new();

            public PageContext(string role, string notificationLanguage = "")
            {
                GlobalConfig = new SimulatedGlobalConfig
                {
                    RuleExpiryInitiatorKeys = kInitiatorKeys,
                    NotificationLanguage = notificationLanguage,
                    UiLanguages =
                    [
                        new Language { Name = "German", CultureInfo = "de-DE" },
                        new Language { Name = "English", CultureInfo = "en-US" }
                    ]
                };
                UserConfig = new SimulatedUserConfig();
                UserConfig.User.Roles = [role];
                UserConfig.SetExecutionMode(role);
                context.JSInterop.Mode = JSRuntimeMode.Loose;
                context.Services.AddLocalization();
                context.Services.AddAuthorizationCore();
                context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
                context.Services.AddSingleton<AuthenticationStateProvider>(new AllowAllAuthStateProvider(role));
                context.Services.AddSingleton<DomEventService>();
                context.Services.AddSingleton<ApiConnection>(ApiConnection);
                context.Services.AddSingleton<GlobalConfig>(GlobalConfig);
                context.Services.AddSingleton<UserConfig>(UserConfig);
                context.Services.AddSingleton(typeof(IStringLocalizer<>), typeof(PassThroughStringLocalizer<>));
            }

            public SettingsNotificationsApiConn ApiConnection { get; } = new();
            public SimulatedGlobalConfig GlobalConfig { get; }
            public SimulatedUserConfig UserConfig { get; }
            public List<(Exception? Exception, string Title, string Message, bool IsError)> Messages { get; } = [];

            public IRenderedComponent<SettingsNotifications> Render()
            {
                IRenderedComponent<CascadingAuthenticationState> wrapper = context.Render<CascadingAuthenticationState>(parameters => parameters
                    .AddChildContent(builder =>
                    {
                        builder.OpenComponent<CascadingValue<Action<Exception?, string, string, bool>>>(0);
                        builder.AddAttribute(1, "Value", (Action<Exception?, string, string, bool>)((exception, title, message, isError) => Messages.Add((exception, title, message, isError))));
                        builder.AddAttribute(2, "IsFixed", true);
                        builder.AddAttribute(3, "ChildContent", (RenderFragment)(childBuilder =>
                        {
                            childBuilder.OpenComponent<SettingsNotifications>(0);
                            childBuilder.CloseComponent();
                        }));
                        builder.CloseComponent();
                    }));
                return wrapper.FindComponent<SettingsNotifications>();
            }

            public ValueTask DisposeAsync()
            {
                return context.DisposeAsync();
            }
        }

        /// <summary>
        /// Serves the queries of the embedded notification editors and records config writes.
        /// </summary>
        private sealed class SettingsNotificationsApiConn : SimulatedApiConnection
        {
            public bool ThrowOnUpsert { get; set; }
            public List<ConfigItem> UpsertedItems { get; } = [];

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                if (query == NotificationQueries.getNotifications)
                {
                    return Task.FromResult((QueryResponseType)(object)new List<FwoNotification>());
                }
                if (query == OwnerQueries.getOwnerResponsibleTypes)
                {
                    return Task.FromResult((QueryResponseType)(object)new List<OwnerResponsibleType>());
                }
                if (query == ConfigQueries.upsertConfigItems)
                {
                    if (ThrowOnUpsert)
                    {
                        throw new InvalidOperationException("config write failed");
                    }
                    PropertyInfo itemsProperty = variables?.GetType().GetProperty("config_items")
                        ?? throw new ArgumentException("Missing config_items variable.");
                    UpsertedItems.AddRange((IEnumerable<ConfigItem>)itemsProperty.GetValue(variables)!);
                    return Task.FromResult(default(QueryResponseType)!);
                }
                throw new NotSupportedException($"Unexpected query: {query}");
            }
        }

        private sealed class PassThroughStringLocalizer<T> : IStringLocalizer<T>
        {
            public LocalizedString this[string name] => new(name, name, resourceNotFound: true);

            public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(name, arguments), resourceNotFound: true);

            public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
        }
    }
}
