using System.Reflection;
using Bunit;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Config.Api.Data;
using FWO.Data;
using FWO.Data.Ai;
using FWO.Middleware.Client;
using FWO.Test.Mocks;
using FWO.Ui.Pages.Settings;
using FWO.Ui.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FWO.Test;

/// <summary>
/// Tests the AI settings page: it offers the credentials that can hold an API key, checks the settings like the AI
/// harness does before it stores them, and lets only an admin save.
/// </summary>
[TestFixture]
[NonParallelizable]
internal class UiSettingsAiTest
{
    private const string kModel = "gpt-test";
    private const string kMarkupPayload = "<img src=x onerror=\"alert(1)\">'\"";
    private const int kApiKeyCredentialId = 3;
    private const string kApiKeyCredentialName = "openai-key";
    private const int kKeyPairCredentialId = 4;
    private const int kAnthropicCredentialId = 5;
    private const string kAnthropicCredentialName = "anthropic-key";
    private const int kUnknownCredentialId = 99;
    private const string kCredentialInputSelector = "#dropdown-input-aiCredential";
    private const string kCredentialChoiceSelector = "button[id^='dropdown-menu-element-aiCredential']";
    private const string kSelectedCredentialSelector = "button[id^='dropdown-menu-selected-aiCredential']";
    private static readonly List<int> kOfferedCredentialIds = [kAnthropicCredentialId, kApiKeyCredentialId];

    /// <summary>
    /// Verifies that an admin can save.
    /// </summary>
    [Test]
    public async Task SettingsAi_RendersEnabledSaveForAdmin()
    {
        await using BunitContext context = CreateContext(Roles.Admin, CreateApiConnection(), CreateGlobalConfig());
        IRenderedComponent<CascadingAuthenticationState> wrapper = RenderComponent(context);

        wrapper.WaitForAssertion(() => Assert.That(wrapper.Find("button").HasAttribute("disabled"), Is.False));
    }

    /// <summary>
    /// Verifies that an auditor sees the settings, but cannot save.
    /// </summary>
    [Test]
    public async Task SettingsAi_RendersDisabledSaveForAuditor()
    {
        await using BunitContext context = CreateContext(Roles.Auditor, CreateApiConnection(), CreateGlobalConfig());
        IRenderedComponent<CascadingAuthenticationState> wrapper = RenderComponent(context);

        wrapper.WaitForAssertion(() => Assert.That(wrapper.Find("button").HasAttribute("disabled"), Is.True));
    }

    /// <summary>
    /// Verifies that credential names are shown as text, so that markup in a name is not executed.
    /// </summary>
    [Test]
    public async Task SettingsAi_RendersCredentialNamesAsText()
    {
        RecordingSettingsApiConn apiConnection = CreateApiConnection();
        apiConnection.Credentials[0].Name = kMarkupPayload;
        await using BunitContext context = CreateContext(Roles.Admin, apiConnection, CreateGlobalConfig());
        IRenderedComponent<CascadingAuthenticationState> wrapper = RenderComponent(context);

        await OpenCredentialDropdown(wrapper);

        List<string> choiceTexts = [.. wrapper.FindAll(kCredentialChoiceSelector).Select(choice => choice.TextContent.Trim())];
        Assert.Multiple(() =>
        {
            Assert.That(choiceTexts, Does.Contain(kMarkupPayload));
            Assert.That(wrapper.FindAll("img"), Is.Empty);
        });
    }

    /// <summary>
    /// Verifies that the stored settings are loaded, and that only credentials that can hold an API key are offered,
    /// sorted by name.
    /// </summary>
    [Test]
    public async Task OnInitializedAsync_LoadsSettingsAndApiKeyCredentials()
    {
        SimulatedGlobalConfig globalConfig = CreateGlobalConfig();
        globalConfig.AiProvider = AiProvider.OpenAi;
        globalConfig.AiModel = kModel;
        globalConfig.AiCredentialId = kApiKeyCredentialId;
        globalConfig.AiContextWindowTokens = 128000;
        SettingsAi component = CreateComponent(CreateApiConnection(), globalConfig);

        await InvokePrivateTask(component, "OnInitializedAsync");

        AiSettings aiSettings = GetMember<AiSettings>(component, "aiSettings");
        List<Credential> credentials = GetMember<List<Credential>>(component, "credentials");
        Credential? selectedCredential = GetMember<Credential?>(component, "selectedCredential");
        Assert.Multiple(() =>
        {
            Assert.That(aiSettings.Provider, Is.EqualTo(AiProvider.OpenAi));
            Assert.That(aiSettings.Model, Is.EqualTo(kModel));
            Assert.That(aiSettings.CredentialId, Is.EqualTo(kApiKeyCredentialId));
            Assert.That(aiSettings.ContextWindowTokens, Is.EqualTo(128000));
            Assert.That(credentials.Select(credential => credential.Id), Is.EqualTo(kOfferedCredentialIds));
            Assert.That(selectedCredential, Is.SameAs(credentials.Single(credential => credential.Id == kApiKeyCredentialId)));
        });
    }

    /// <summary>
    /// Verifies that a referenced credential that is not offered, e.g. a key pair, stays visible as its id.
    /// </summary>
    [Test]
    public async Task SettingsAi_ShowsReferencedCredentialThatIsNotOffered()
    {
        SimulatedGlobalConfig globalConfig = CreateGlobalConfig();
        globalConfig.AiCredentialId = kUnknownCredentialId;
        await using BunitContext context = CreateContext(Roles.Admin, CreateApiConnection(), globalConfig);
        IRenderedComponent<CascadingAuthenticationState> wrapper = RenderComponent(context);

        wrapper.WaitForState(() => GetCredentialText(wrapper) == kUnknownCredentialId.ToString());

        Assert.Multiple(() =>
        {
            Assert.That(GetCredentialText(wrapper), Is.EqualTo(kUnknownCredentialId.ToString()));
            Assert.That(GetAiSettings(wrapper).CredentialId, Is.EqualTo(kUnknownCredentialId));
        });
    }

    /// <summary>
    /// Verifies that the stored credential is shown with its name once the credentials are loaded, although the page is
    /// rendered before, while they load. A select lost the stored credential this way and showed none.
    /// </summary>
    [Test]
    public async Task SettingsAi_ShowsStoredCredentialOnceCredentialsAreLoaded()
    {
        SimulatedGlobalConfig globalConfig = CreateGlobalConfig();
        globalConfig.AiCredentialId = kApiKeyCredentialId;
        RecordingSettingsApiConn apiConnection = CreateApiConnection();
        TaskCompletionSource credentialsReleased = new(TaskCreationOptions.RunContinuationsAsynchronously);
        apiConnection.CredentialsReleased = credentialsReleased.Task;
        await using BunitContext context = CreateContext(Roles.Admin, apiConnection, globalConfig);
        IRenderedComponent<CascadingAuthenticationState> wrapper = RenderComponent(context);

        wrapper.WaitForState(() => GetCredentialText(wrapper) != null);
        string? textWhileLoading = GetCredentialText(wrapper);
        credentialsReleased.SetResult();
        wrapper.WaitForState(() => GetCredentialText(wrapper) == kApiKeyCredentialName);

        Assert.Multiple(() =>
        {
            Assert.That(textWhileLoading, Is.EqualTo(kApiKeyCredentialId.ToString()));
            Assert.That(GetCredentialText(wrapper), Is.EqualTo(kApiKeyCredentialName));
            Assert.That(GetAiSettings(wrapper).CredentialId, Is.EqualTo(kApiKeyCredentialId));
        });
    }

    /// <summary>
    /// Verifies that selecting a credential in the dropdown sets its id, and that removing the selection sets none.
    /// </summary>
    [Test]
    public async Task SettingsAi_SelectingAndRemovingCredentialSetsCredentialId()
    {
        await using BunitContext context = CreateContext(Roles.Admin, CreateApiConnection(), CreateGlobalConfig());
        IRenderedComponent<CascadingAuthenticationState> wrapper = RenderComponent(context);
        wrapper.WaitForState(() => GetCredentialText(wrapper) != null);
        string? textWithoutCredential = GetCredentialText(wrapper);

        await OpenCredentialDropdown(wrapper);
        wrapper.FindAll(kCredentialChoiceSelector).Single(choice => choice.TextContent.Trim() == kAnthropicCredentialName).Click();
        wrapper.WaitForState(() => GetAiSettings(wrapper).CredentialId == kAnthropicCredentialId);
        string? textWithCredential = GetCredentialText(wrapper);

        await OpenCredentialDropdown(wrapper);
        wrapper.Find(kSelectedCredentialSelector).Click();
        wrapper.WaitForState(() => GetAiSettings(wrapper).CredentialId == 0);

        Assert.Multiple(() =>
        {
            Assert.That(textWithoutCredential, Is.EqualTo("None"));
            Assert.That(textWithCredential, Is.EqualTo(kAnthropicCredentialName));
            Assert.That(GetAiSettings(wrapper).CredentialId, Is.Zero);
            Assert.That(GetCredentialText(wrapper), Is.EqualTo(textWithoutCredential));
        });
    }

    /// <summary>
    /// Verifies that the stored credential stays selected and visible by its id if the credentials cannot be read.
    /// </summary>
    [Test]
    public async Task OnInitializedAsync_KeepsStoredCredentialWhenCredentialsCannotBeRead()
    {
        List<(Exception? Exception, string Title, string Message, bool IsError)> messages = [];
        SimulatedGlobalConfig globalConfig = CreateGlobalConfig();
        globalConfig.AiCredentialId = kApiKeyCredentialId;
        RecordingSettingsApiConn apiConnection = CreateApiConnection();
        apiConnection.CredentialsReleased = Task.FromException(new HttpRequestException("API not reachable"));
        SettingsAi component = CreateComponent(apiConnection, globalConfig, messages);

        await InvokePrivateTask(component, "OnInitializedAsync");

        Credential? selectedCredential = GetMember<Credential?>(component, "selectedCredential");
        Assert.Multiple(() =>
        {
            Assert.That(messages.Single().Exception, Is.TypeOf<HttpRequestException>());
            Assert.That(selectedCredential?.Id, Is.EqualTo(kApiKeyCredentialId));
            Assert.That(selectedCredential?.Name, Is.EqualTo(kApiKeyCredentialId.ToString()));
            Assert.That(GetMember<AiSettings>(component, "aiSettings").CredentialId, Is.EqualTo(kApiKeyCredentialId));
        });
    }

    /// <summary>
    /// Verifies that a failure to read the config is shown.
    /// </summary>
    [Test]
    public async Task OnInitializedAsync_ShowsErrorWhenConfigAccessFails()
    {
        List<(Exception? Exception, string Title, string Message, bool IsError)> messages = [];
        SimulatedGlobalConfig globalConfig = CreateGlobalConfig();
        globalConfig.Dispose();
        SettingsAi component = CreateComponent(CreateApiConnection(), globalConfig, messages);

        await InvokePrivateTask(component, "OnInitializedAsync");

        Assert.Multiple(() =>
        {
            Assert.That(messages, Has.Count.EqualTo(1));
            Assert.That(messages[0].Message, Is.EqualTo(GetMember<UserConfig>(component, "userConfig").GetText("E5301")));
            Assert.That(messages[0].Exception, Is.TypeOf<ObjectDisposedException>());
        });
    }

    /// <summary>
    /// Verifies that invalid settings are not stored, and that each problem is shown with its text.
    /// </summary>
    [Test]
    public async Task Save_WithInvalidSettings_ShowsProblemsAndStoresNothing()
    {
        List<(Exception? Exception, string Title, string Message, bool IsError)> messages = [];
        RecordingSettingsApiConn apiConnection = CreateApiConnection();
        SettingsAi component = CreateComponent(apiConnection, CreateGlobalConfig(), messages);
        await InvokePrivateTask(component, "OnInitializedAsync");
        SetMember(component, "aiSettings", new AiSettings { Provider = AiProvider.OpenAi, ContextWindowTokens = 1000 });

        await InvokePrivateTask(component, "Save");

        Assert.Multiple(() =>
        {
            Assert.That(apiConnection.UpsertConfigCallCount, Is.Zero);
            Assert.That(messages.Single().IsError, Is.True);
            Assert.That(messages.Single().Message, Does.Contain(AiSettingsProblemTexts.GetTextKey(AiSettingsProblem.ModelMissing)));
            Assert.That(messages.Single().Message, Does.Contain(AiSettingsProblemTexts.GetTextKey(AiSettingsProblem.CredentialMissing)));
        });
    }

    /// <summary>
    /// Verifies that valid settings are stored as global config items, with trimmed texts.
    /// </summary>
    [Test]
    public async Task Save_WithValidSettings_StoresConfigItems()
    {
        List<(Exception? Exception, string Title, string Message, bool IsError)> messages = [];
        RecordingSettingsApiConn apiConnection = CreateApiConnection();
        SettingsAi component = CreateComponent(apiConnection, CreateGlobalConfig(), messages);
        await InvokePrivateTask(component, "OnInitializedAsync");
        SetMember(component, "aiSettings", new AiSettings
        {
            Provider = AiProvider.Anthropic,
            Model = $" {kModel} ",
            CredentialId = kApiKeyCredentialId,
            ContextWindowTokens = 200000,
            ChatEnabled = true
        });

        await InvokePrivateTask(component, "Save");

        Dictionary<string, string?> storedValues = apiConnection.LastUpsertConfigItems.ToDictionary(item => item.Key, item => (string?)item.Value);
        Assert.Multiple(() =>
        {
            Assert.That(messages.Single().IsError, Is.False);
            Assert.That(apiConnection.LastUpsertConfigItems.All(item => item.User == 0), Is.True);
            Assert.That(storedValues[AiSettings.kProviderKey], Is.EqualTo("Anthropic"));
            Assert.That(storedValues[AiSettings.kModelKey], Is.EqualTo(kModel));
            Assert.That(storedValues[AiSettings.kCredentialIdKey], Is.EqualTo(kApiKeyCredentialId.ToString()));
            Assert.That(storedValues[AiSettings.kContextWindowTokensKey], Is.EqualTo("200000"));
            Assert.That(storedValues[AiSettings.kChatKey], Is.EqualTo("True").IgnoreCase);
            Assert.That(AiSettings.FromConfigValues(storedValues).Provider, Is.EqualTo(AiProvider.Anthropic));
            Assert.That(AiSettings.FromConfigValues(storedValues).ChatEnabled, Is.True);
            Assert.That(AiSettings.FromConfigValues(storedValues).ReasonCheckEnabled, Is.False);
        });
    }

    /// <summary>
    /// Verifies that the page shows the stored switches of the AI functions, each in its own checkbox.
    /// </summary>
    [TestCase(true, false)]
    [TestCase(false, true)]
    public async Task SettingsAi_ShowsSwitchesOfAiFunctions(bool chatEnabled, bool reasonCheckEnabled)
    {
        SimulatedGlobalConfig globalConfig = CreateGlobalConfig();
        globalConfig.AiChatEnabled = chatEnabled;
        globalConfig.AiReasonCheckEnabled = reasonCheckEnabled;
        await using BunitContext context = CreateContext(Roles.Admin, CreateApiConnection(), globalConfig);
        IRenderedComponent<CascadingAuthenticationState> wrapper = RenderComponent(context);

        wrapper.WaitForAssertion(() =>
        {
            Assert.That(wrapper.Find("#aiChat").HasAttribute("checked"), Is.EqualTo(chatEnabled));
            Assert.That(wrapper.Find("#aiReasonCheck").HasAttribute("checked"), Is.EqualTo(reasonCheckEnabled));
        });
    }

    /// <summary>
    /// Verifies that no provider is shown with the localized text for none.
    /// </summary>
    [Test]
    public void GetProviderName_ShowsNoneLocalized()
    {
        SettingsAi component = CreateComponent(CreateApiConnection(), CreateGlobalConfig());

        Assert.That(InvokePrivate<string>(component, "GetProviderName", AiProvider.None), Is.EqualTo("None"));
        Assert.That(InvokePrivate<string>(component, "GetProviderName", AiProvider.Google), Is.EqualTo("Google"));
    }

    private static RecordingSettingsApiConn CreateApiConnection()
    {
        return new RecordingSettingsApiConn
        {
            Credentials =
            [
                new Credential { Id = kApiKeyCredentialId, Name = kApiKeyCredentialName },
                new Credential { Id = kKeyPairCredentialId, Name = "firewall-key-pair", IsKeyPair = true },
                new Credential { Id = kAnthropicCredentialId, Name = kAnthropicCredentialName }
            ]
        };
    }

    /// <summary>
    /// Returns the text the credential dropdown shows, null while it is not rendered.
    /// </summary>
    private static string? GetCredentialText(IRenderedComponent<CascadingAuthenticationState> wrapper)
    {
        return wrapper.FindAll(kCredentialInputSelector).SingleOrDefault()?.GetAttribute("value");
    }

    /// <summary>
    /// Returns the AI settings edited by the rendered page.
    /// </summary>
    private static AiSettings GetAiSettings(IRenderedComponent<CascadingAuthenticationState> wrapper)
    {
        return GetMember<AiSettings>(wrapper.FindComponent<SettingsAi>().Instance, "aiSettings");
    }

    /// <summary>
    /// Opens the menu of the credential dropdown by focusing its input and waits until it offers credentials.
    /// </summary>
    private static async Task OpenCredentialDropdown(IRenderedComponent<CascadingAuthenticationState> wrapper)
    {
        wrapper.WaitForState(() => wrapper.FindAll(kCredentialInputSelector).Count == 1);
        await wrapper.InvokeAsync(() => wrapper.Find(kCredentialInputSelector).Focus());
        wrapper.WaitForState(() => wrapper.FindAll(kCredentialChoiceSelector).Count > 0);
    }

    private static BunitContext CreateContext(string role, RecordingSettingsApiConn apiConnection, SimulatedGlobalConfig globalConfig)
    {
        BunitContext context = new();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddAuthorizationCore();
        context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
        context.Services.AddSingleton<AuthenticationStateProvider>(new AllowAllAuthStateProvider(role));
        context.Services.AddSingleton<DomEventService>();
        context.Services.AddSingleton<ApiConnection>(apiConnection);
        context.Services.AddSingleton<GlobalConfig>(globalConfig);
        context.Services.AddSingleton<UserConfig>(CreateUserConfig(role));
        context.Services.AddSingleton<MiddlewareClient>(new MockMiddlewareClient());
        return context;
    }

    private static IRenderedComponent<CascadingAuthenticationState> RenderComponent(BunitContext context)
    {
        return context.Render<CascadingAuthenticationState>(parameters => parameters
            .AddChildContent<CascadingValue<Action<Exception?, string, string, bool>>>(child => child
                .Add(p => p.Value, (_, _, _, _) => { })
                .AddChildContent<SettingsAi>()));
    }

    private static SettingsAi CreateComponent(ApiConnection apiConnection, GlobalConfig globalConfig,
        List<(Exception? Exception, string Title, string Message, bool IsError)>? messages = null)
    {
        SettingsAi component = new();
        SetMember(component, "apiConnection", apiConnection);
        SetMember(component, "globalConfig", globalConfig);
        SetMember(component, "userConfig", CreateUserConfig(Roles.Admin));
        SetMember(component, "DisplayMessageInUi", (Action<Exception?, string, string, bool>)((exception, title, message, isError) =>
        {
            messages?.Add((exception, title, message, isError));
        }));
        return component;
    }

    private static SimulatedGlobalConfig CreateGlobalConfig()
    {
        return new SimulatedGlobalConfig
        {
            UiLanguages =
            [
                new Language { Name = GlobalConst.kEnglish, CultureInfo = "en-US" }
            ]
        };
    }

    private static SimulatedUserConfig CreateUserConfig(string role)
    {
        SimulatedUserConfig userConfig = new();
        userConfig.User.Roles = [role];
        return userConfig;
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
        FieldInfo field = type.GetField(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new MissingMemberException(type.FullName, memberName);
        field.SetValue(instance, value);
    }

    private static T GetMember<T>(object instance, string memberName)
    {
        Type type = instance.GetType();
        PropertyInfo? property = type.GetProperty(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (property != null)
        {
            return (T)property.GetValue(instance)!;
        }
        FieldInfo field = type.GetField(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new MissingMemberException(type.FullName, memberName);
        return (T)field.GetValue(instance)!;
    }

    private static T InvokePrivate<T>(object instance, string methodName, params object?[] args)
    {
        MethodInfo method = instance.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new MissingMethodException(instance.GetType().FullName, methodName);
        return (T)method.Invoke(instance, args)!;
    }

    private static async Task InvokePrivateTask(object instance, string methodName)
    {
        await InvokePrivate<Task>(instance, methodName);
    }
}
