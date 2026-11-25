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
/// Tests the AI settings page: it offers the credentials that can hold an API key and the roles of the internal LDAP for
/// the role filters of the AI functions, checks the settings like the AI harness does before it stores them, and lets
/// only an admin save.
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
    private const string kProviderInputSelector = "#dropdown-input-aiProvider";
    private const string kProviderChoiceSelector = "button[id^='dropdown-menu-element-aiProvider']";
    private const string kAuthMethodInputSelector = "#dropdown-input-aiAuthMethod";
    private const string kAuthMethodChoiceSelector = "button[id^='dropdown-menu-element-aiAuthMethod']";
    private const string kTokenUrlSelector = "#aiTokenUrl";
    private const string kOAuthTextKey = "ai_auth_oauth2_client_credentials";
    private static readonly string kSaveIconSelector = "span." + Icons.Save.Replace(' ', '.');
    private const string kCredentialChoiceSelector = "button[id^='dropdown-menu-element-aiCredential']";
    private const string kSelectedCredentialSelector = "button[id^='dropdown-menu-selected-aiCredential']";
    private const string kChatRolesInputSelector = "#dropdown-input-aiChatRoles";
    private const string kReasonCheckRolesInputSelector = "#dropdown-input-aiReasonCheckRoles";
    private const string kChatRoleChoiceSelector = "button[id^='dropdown-menu-element-aiChatRoles']";
    private const string kSelectedChatRoleSelector = "button[id^='dropdown-menu-selected-aiChatRoles']";
    private const string kStoredOnlyRole = "legacy-role";
    private static readonly List<int> kOfferedCredentialIds = [kAnthropicCredentialId, kApiKeyCredentialId];
    private static readonly List<string> kLdapRoleNames =
        [Roles.Reporter, Roles.Admin, Roles.MiddlewareServer, Roles.Modeller, Roles.Anonymous, Roles.AiHarness];
    private static readonly List<string> kOfferedLdapRoles = [Roles.Admin, Roles.Modeller, Roles.Reporter];
    private static readonly List<string> kOfferedRolesWithStoredRole = [Roles.Admin, kStoredOnlyRole, Roles.Modeller, Roles.Reporter];
    private static readonly List<string> kModellerRoles = [Roles.Modeller];
    private static readonly List<string> kModellerAndAdminRoles = [Roles.Modeller, Roles.Admin];
    private static readonly List<string> kAdminAndModellerRoles = [Roles.Admin, Roles.Modeller];
    private static readonly List<string> kMarkupPayloadRoles = [kMarkupPayload];
    private const string kSaveButtonSelector = "div.btn-group button";
    private const string kCustomHeaderAddSelector = "#aiCustomHeaderAdd";
    private const string kCustomHeaderNamePrefix = "aiCustomHeaderName";
    private const string kCustomHeaderValuePrefix = "aiCustomHeaderValue";
    private const string kCustomHeaderRemovePrefix = "aiCustomHeaderRemove";
    private const string kProfileHeader = "x-gateway-profile";
    private const string kProfileValue = "my-profile";
    private const string kIssuerHeader = "token-issuer";
    private const string kIssuerValue = "keycloak";
    private static readonly List<string> kProfileAndIssuerHeaders = [kProfileHeader, kIssuerHeader];
    private const string kRequestTimeoutSelector = "#aiRequestTimeoutSeconds";
    private const int kStoredRequestTimeoutSeconds = 900;
    private const int kChangedRequestTimeoutSeconds = 1800;

    /// <summary>
    /// Verifies that an admin can save.
    /// </summary>
    [Test]
    public async Task SettingsAi_RendersEnabledSaveForAdmin()
    {
        await using BunitContext context = CreateContext(Roles.Admin, CreateApiConnection(), CreateGlobalConfig());
        IRenderedComponent<CascadingAuthenticationState> wrapper = RenderComponent(context);

        wrapper.WaitForState(() => wrapper.FindAll(kSaveButtonSelector).Count == 1 && !wrapper.Find(kSaveButtonSelector).HasAttribute("disabled"));
        Assert.That(wrapper.Find(kSaveButtonSelector).HasAttribute("disabled"), Is.False);
    }

    /// <summary>
    /// Verifies that an auditor sees the settings, but cannot save.
    /// </summary>
    [Test]
    public async Task SettingsAi_RendersDisabledSaveForAuditor()
    {
        await using BunitContext context = CreateContext(Roles.Auditor, CreateApiConnection(), CreateGlobalConfig());
        IRenderedComponent<CascadingAuthenticationState> wrapper = RenderComponent(context);

        wrapper.WaitForState(() => wrapper.FindAll(kSaveButtonSelector).Count == 1 && wrapper.Find(kSaveButtonSelector).HasAttribute("disabled"));
        Assert.That(wrapper.Find(kSaveButtonSelector).HasAttribute("disabled"), Is.True);
    }

    /// <summary>
    /// Verifies that rows of additional headers can be added, edited and removed, the remaining row keeping its values.
    /// </summary>
    [Test]
    public async Task SettingsAi_AddsEditsAndRemovesCustomHeaders()
    {
        await using BunitContext context = CreateContext(Roles.Admin, CreateApiConnection(), CreateGlobalConfig());
        IRenderedComponent<CascadingAuthenticationState> wrapper = RenderComponent(context);
        wrapper.WaitForState(() => wrapper.FindAll(kChatRolesInputSelector).Count == 1);

        wrapper.Find(kCustomHeaderAddSelector).Click();
        wrapper.Find(kCustomHeaderAddSelector).Click();
        wrapper.Find(GetCustomHeaderSelector(kCustomHeaderNamePrefix, 0)).Change(kProfileHeader);
        wrapper.Find(GetCustomHeaderSelector(kCustomHeaderValuePrefix, 0)).Change(kProfileValue);
        wrapper.Find(GetCustomHeaderSelector(kCustomHeaderNamePrefix, 1)).Change(kIssuerHeader);
        wrapper.Find(GetCustomHeaderSelector(kCustomHeaderValuePrefix, 1)).Change(kIssuerValue);
        List<string> namesBeforeRemoval = [.. GetAiSettings(wrapper).CustomHeaders.Select(header => header.Name)];
        wrapper.Find(GetCustomHeaderSelector(kCustomHeaderRemovePrefix, 0)).Click();

        List<AiCustomHeader> headers = GetAiSettings(wrapper).CustomHeaders;
        Assert.Multiple(() =>
        {
            Assert.That(namesBeforeRemoval, Is.EqualTo(kProfileAndIssuerHeaders));
            Assert.That(headers.Single().Name, Is.EqualTo(kIssuerHeader));
            Assert.That(headers.Single().Value, Is.EqualTo(kIssuerValue));
            Assert.That(wrapper.FindAll(GetCustomHeaderSelector(kCustomHeaderNamePrefix, 1)), Is.Empty);
            Assert.That(wrapper.Find(GetCustomHeaderSelector(kCustomHeaderNamePrefix, 0)).GetAttribute("value"), Is.EqualTo(kIssuerHeader));
            Assert.That(wrapper.Find(GetCustomHeaderSelector(kCustomHeaderValuePrefix, 0)).GetAttribute("value"), Is.EqualTo(kIssuerValue));
        });
    }

    /// <summary>
    /// Verifies that stored headers are shown in their inputs as text, so that markup in a value is not executed.
    /// </summary>
    [Test]
    public async Task SettingsAi_ShowsStoredCustomHeadersAsText()
    {
        SimulatedGlobalConfig globalConfig = CreateGlobalConfig();
        List<AiCustomHeader> storedHeaders = [new AiCustomHeader { Name = kIssuerHeader, Value = kMarkupPayload }];
        globalConfig.AiCustomHeaders = AiCustomHeaderList.Serialize(storedHeaders);
        await using BunitContext context = CreateContext(Roles.Admin, CreateApiConnection(), globalConfig);
        IRenderedComponent<CascadingAuthenticationState> wrapper = RenderComponent(context);

        wrapper.WaitForState(() => wrapper.FindAll(GetCustomHeaderSelector(kCustomHeaderValuePrefix, 0)).Count == 1);

        Assert.Multiple(() =>
        {
            Assert.That(wrapper.Find(GetCustomHeaderSelector(kCustomHeaderNamePrefix, 0)).GetAttribute("value"), Is.EqualTo(kIssuerHeader));
            Assert.That(wrapper.Find(GetCustomHeaderSelector(kCustomHeaderValuePrefix, 0)).GetAttribute("value"), Is.EqualTo(kMarkupPayload));
            Assert.That(wrapper.FindAll("img"), Is.Empty);
            Assert.That(wrapper.Find(kCustomHeaderAddSelector).HasAttribute("disabled"), Is.False);
        });
    }

    /// <summary>
    /// Verifies that the stored request timeout is shown with its bounds and that a changed value reaches the settings.
    /// </summary>
    [Test]
    public async Task SettingsAi_ShowsAndEditsRequestTimeout()
    {
        SimulatedGlobalConfig globalConfig = CreateGlobalConfig();
        globalConfig.AiRequestTimeoutSeconds = kStoredRequestTimeoutSeconds;
        await using BunitContext context = CreateContext(Roles.Admin, CreateApiConnection(), globalConfig);
        IRenderedComponent<CascadingAuthenticationState> wrapper = RenderComponent(context);
        wrapper.WaitForState(() => wrapper.FindAll(kChatRolesInputSelector).Count == 1);

        string shownValue = wrapper.Find(kRequestTimeoutSelector).GetAttribute("value") ?? "";
        wrapper.Find(kRequestTimeoutSelector).Change(kChangedRequestTimeoutSeconds.ToString());

        Assert.Multiple(() =>
        {
            Assert.That(shownValue, Is.EqualTo(kStoredRequestTimeoutSeconds.ToString()));
            Assert.That(wrapper.Find(kRequestTimeoutSelector).GetAttribute("min"), Is.EqualTo(AiSettings.kMinRequestTimeoutSeconds.ToString()));
            Assert.That(wrapper.Find(kRequestTimeoutSelector).GetAttribute("max"), Is.EqualTo(AiSettings.kMaxRequestTimeoutSeconds.ToString()));
            Assert.That(GetAiSettings(wrapper).RequestTimeoutSeconds, Is.EqualTo(kChangedRequestTimeoutSeconds));
        });
    }

    /// <summary>
    /// Verifies that no header can be added once the maximum number of headers is reached.
    /// </summary>
    [Test]
    public async Task SettingsAi_WithMaximumOfCustomHeaders_DisablesAdd()
    {
        SimulatedGlobalConfig globalConfig = CreateGlobalConfig();
        List<AiCustomHeader> storedHeaders = [.. Enumerable.Range(0, AiCustomHeaderList.kMaxHeaders)
            .Select(index => new AiCustomHeader { Name = $"x-header-{index}", Value = kIssuerValue })];
        globalConfig.AiCustomHeaders = AiCustomHeaderList.Serialize(storedHeaders);
        await using BunitContext context = CreateContext(Roles.Admin, CreateApiConnection(), globalConfig);
        IRenderedComponent<CascadingAuthenticationState> wrapper = RenderComponent(context);

        string lastRowSelector = GetCustomHeaderSelector(kCustomHeaderNamePrefix, AiCustomHeaderList.kMaxHeaders - 1);
        wrapper.WaitForState(() => wrapper.FindAll(lastRowSelector).Count == 1);

        Assert.That(wrapper.Find(kCustomHeaderAddSelector).HasAttribute("disabled"), Is.True);
    }

    /// <summary>
    /// Verifies that the additional headers are stored without the rows left blank.
    /// </summary>
    [Test]
    public async Task Save_StoresCustomHeaders()
    {
        List<(Exception? Exception, string Title, string Message, bool IsError)> messages = [];
        RecordingSettingsApiConn apiConnection = CreateApiConnection();
        SettingsAi component = CreateComponent(apiConnection, CreateGlobalConfig(), messages);
        await InvokePrivateTask(component, "OnInitializedAsync");
        AiSettings settings = CreateValidAnthropicSettings();
        settings.CustomHeaders.Add(new AiCustomHeader { Name = $" {kProfileHeader} ", Value = kProfileValue });
        settings.CustomHeaders.Add(new AiCustomHeader());
        settings.CustomHeaders.Add(new AiCustomHeader { Name = kIssuerHeader, Value = kIssuerValue });
        SetMember(component, "aiSettings", settings);

        await InvokePrivateTask(component, "Save");

        string? storedHeaders = apiConnection.LastUpsertConfigItems.Single(item => item.Key == AiSettings.kCustomHeadersKey).Value;
        Assert.Multiple(() =>
        {
            Assert.That(messages.Single().IsError, Is.False);
            Assert.That(AiCustomHeaderList.TryParse(storedHeaders, out List<AiCustomHeader> readHeaders), Is.True);
            Assert.That(readHeaders.Select(header => header.Name), Is.EqualTo(kProfileAndIssuerHeaders));
        });
    }

    /// <summary>
    /// Verifies that a header carrying credentials is not stored, and that the problem is shown with its text.
    /// </summary>
    [Test]
    public async Task Save_WithReservedCustomHeader_ShowsProblemAndStoresNothing()
    {
        List<(Exception? Exception, string Title, string Message, bool IsError)> messages = [];
        RecordingSettingsApiConn apiConnection = CreateApiConnection();
        SettingsAi component = CreateComponent(apiConnection, CreateGlobalConfig(), messages);
        await InvokePrivateTask(component, "OnInitializedAsync");
        AiSettings settings = CreateValidAnthropicSettings();
        settings.CustomHeaders.Add(new AiCustomHeader { Name = "x-api-key", Value = kProfileValue });
        SetMember(component, "aiSettings", settings);

        await InvokePrivateTask(component, "Save");

        Assert.Multiple(() =>
        {
            Assert.That(apiConnection.UpsertConfigCallCount, Is.Zero);
            Assert.That(messages.Single().IsError, Is.True);
            Assert.That(messages.Single().Message, Does.Contain(AiSettingsProblemTexts.GetTextKey(AiSettingsProblem.CustomHeaderNameReserved)));
        });
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
        SetMember(component, "aiSettings", new AiSettings { Provider = AiProvider.OpenAi, ContextWindowTokens = 1000, RequestTimeoutSeconds = 0 });

        await InvokePrivateTask(component, "Save");

        Assert.Multiple(() =>
        {
            Assert.That(apiConnection.UpsertConfigCallCount, Is.Zero);
            Assert.That(messages.Single().IsError, Is.True);
            Assert.That(messages.Single().Message, Does.Contain(AiSettingsProblemTexts.GetTextKey(AiSettingsProblem.ModelMissing)));
            Assert.That(messages.Single().Message, Does.Contain(AiSettingsProblemTexts.GetTextKey(AiSettingsProblem.CredentialMissing)));
            Assert.That(messages.Single().Message, Does.Contain(AiSettingsProblemTexts.GetTextKey(AiSettingsProblem.RequestTimeoutInvalid)));
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
            RequestTimeoutSeconds = kChangedRequestTimeoutSeconds,
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
            Assert.That(storedValues[AiSettings.kRequestTimeoutSecondsKey], Is.EqualTo(kChangedRequestTimeoutSeconds.ToString()));
            Assert.That(storedValues[AiSettings.kChatKey], Is.EqualTo("True").IgnoreCase);
            Assert.That(AiSettings.FromConfigValues(storedValues).Provider, Is.EqualTo(AiProvider.Anthropic));
            Assert.That(AiSettings.FromConfigValues(storedValues).ChatEnabled, Is.True);
            Assert.That(AiSettings.FromConfigValues(storedValues).ReasonCheckEnabled, Is.False);
        });
    }

    /// <summary>
    /// Verifies that the dropdowns of provider and authentication method show the stored settings, and that the token
    /// URL field is shown for OAuth 2.0.
    /// </summary>
    [Test]
    public async Task SettingsAi_DropdownsShowStoredProviderAndAuthMethod()
    {
        SimulatedGlobalConfig globalConfig = CreateGlobalConfig();
        globalConfig.AiProvider = AiProvider.OpenAi;
        globalConfig.AiAuthMethod = AiAuthMethod.OAuth2ClientCredentials;
        string oauthText = CreateUserConfig(Roles.Admin).GetText(kOAuthTextKey);
        await using BunitContext context = CreateContext(Roles.Admin, CreateApiConnection(), globalConfig);
        IRenderedComponent<CascadingAuthenticationState> wrapper = RenderComponent(context);

        wrapper.WaitForState(() => GetInputText(wrapper, kProviderInputSelector) == nameof(AiProvider.OpenAi)
            && GetInputText(wrapper, kAuthMethodInputSelector) == oauthText);

        Assert.Multiple(() =>
        {
            Assert.That(GetInputText(wrapper, kProviderInputSelector), Is.EqualTo(nameof(AiProvider.OpenAi)));
            Assert.That(GetInputText(wrapper, kAuthMethodInputSelector), Is.EqualTo(oauthText));
            Assert.That(wrapper.FindAll(kTokenUrlSelector), Has.Count.EqualTo(1));
            Assert.That(GetAiSettings(wrapper).Provider, Is.EqualTo(AiProvider.OpenAi));
            Assert.That(GetAiSettings(wrapper).AuthMethod, Is.EqualTo(AiAuthMethod.OAuth2ClientCredentials));
        });
    }

    /// <summary>
    /// Verifies that choosing a provider and an authentication method in their dropdowns sets them, and that choosing
    /// OAuth 2.0 shows the token URL field.
    /// </summary>
    [Test]
    public async Task SettingsAi_ChoosingProviderAndAuthMethodSetsThem()
    {
        string oauthText = CreateUserConfig(Roles.Admin).GetText(kOAuthTextKey);
        await using BunitContext context = CreateContext(Roles.Admin, CreateApiConnection(), CreateGlobalConfig());
        IRenderedComponent<CascadingAuthenticationState> wrapper = RenderComponent(context);
        // the role dropdowns are rendered once the settings are loaded, which would replace an earlier choice
        wrapper.WaitForState(() => wrapper.FindAll(kChatRolesInputSelector).Count == 1);
        int tokenUrlFieldsBefore = wrapper.FindAll(kTokenUrlSelector).Count;

        await OpenDropdown(wrapper, kProviderInputSelector, kProviderChoiceSelector);
        wrapper.FindAll(kProviderChoiceSelector).Single(choice => choice.TextContent.Trim() == nameof(AiProvider.Anthropic)).Click();
        wrapper.WaitForState(() => GetAiSettings(wrapper).Provider == AiProvider.Anthropic);
        await OpenDropdown(wrapper, kAuthMethodInputSelector, kAuthMethodChoiceSelector);
        wrapper.FindAll(kAuthMethodChoiceSelector).Single(choice => choice.TextContent.Trim() == oauthText).Click();
        wrapper.WaitForState(() => wrapper.FindAll(kTokenUrlSelector).Count == 1);

        Assert.Multiple(() =>
        {
            Assert.That(tokenUrlFieldsBefore, Is.Zero);
            Assert.That(GetAiSettings(wrapper).Provider, Is.EqualTo(AiProvider.Anthropic));
            Assert.That(GetAiSettings(wrapper).AuthMethod, Is.EqualTo(AiAuthMethod.OAuth2ClientCredentials));
            Assert.That(GetInputText(wrapper, kProviderInputSelector), Is.EqualTo(nameof(AiProvider.Anthropic)));
            Assert.That(GetInputText(wrapper, kAuthMethodInputSelector), Is.EqualTo(oauthText));
        });
    }

    /// <summary>
    /// Verifies that the save button shows the save icon, for admins (enabled) and auditors (disabled).
    /// </summary>
    [TestCase(Roles.Admin)]
    [TestCase(Roles.Auditor)]
    public async Task SettingsAi_SaveButtonShowsSaveIcon(string role)
    {
        await using BunitContext context = CreateContext(role, CreateApiConnection(), CreateGlobalConfig());
        IRenderedComponent<CascadingAuthenticationState> wrapper = RenderComponent(context);

        wrapper.WaitForState(() => wrapper.FindAll(kSaveButtonSelector).Count == 1);

        Assert.That(wrapper.Find(kSaveButtonSelector).QuerySelector(kSaveIconSelector), Is.Not.Null);
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

        wrapper.WaitForState(() => IsChecked(wrapper, "#aiChat") == chatEnabled && IsChecked(wrapper, "#aiReasonCheck") == reasonCheckEnabled);
        Assert.Multiple(() =>
        {
            Assert.That(wrapper.Find("#aiChat").HasAttribute("checked"), Is.EqualTo(chatEnabled));
            Assert.That(wrapper.Find("#aiReasonCheck").HasAttribute("checked"), Is.EqualTo(reasonCheckEnabled));
        });
    }

    /// <summary>
    /// Verifies that each role filter is disabled exactly when its AI function is switched off.
    /// </summary>
    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(true, true)]
    public async Task SettingsAi_DisablesRoleFiltersOfInactiveFunctions(bool chatEnabled, bool reasonCheckEnabled)
    {
        SimulatedGlobalConfig globalConfig = CreateGlobalConfig();
        globalConfig.AiChatEnabled = chatEnabled;
        globalConfig.AiReasonCheckEnabled = reasonCheckEnabled;
        await using BunitContext context = CreateContext(Roles.Admin, CreateApiConnection(), globalConfig);
        IRenderedComponent<CascadingAuthenticationState> wrapper = RenderComponent(context);

        wrapper.WaitForState(() => IsDisabled(wrapper, kChatRolesInputSelector) == !chatEnabled
            && IsDisabled(wrapper, kReasonCheckRolesInputSelector) == !reasonCheckEnabled && IsDisabled(wrapper, kCredentialInputSelector) == false);
        Assert.Multiple(() =>
        {
            Assert.That(wrapper.Find(kChatRolesInputSelector).Matches(":disabled"), Is.EqualTo(!chatEnabled));
            Assert.That(wrapper.Find(kReasonCheckRolesInputSelector).Matches(":disabled"), Is.EqualTo(!reasonCheckEnabled));
            Assert.That(wrapper.Find(kCredentialInputSelector).Matches(":disabled"), Is.False);
        });
    }

    /// <summary>
    /// Verifies that toggling a function updates only its own role filter and preserves the stored selections.
    /// </summary>
    [TestCase("#aiChat", kChatRolesInputSelector, kReasonCheckRolesInputSelector)]
    [TestCase("#aiReasonCheck", kReasonCheckRolesInputSelector, kChatRolesInputSelector)]
    public async Task SettingsAi_TogglingFunctionPreservesRoles(string checkboxSelector, string rolesSelector, string otherRolesSelector)
    {
        SimulatedGlobalConfig globalConfig = CreateGlobalConfig();
        globalConfig.AiChatEnabled = false;
        globalConfig.AiReasonCheckEnabled = false;
        globalConfig.AiChatRoles = AiFunctionRoles.Serialize(kModellerRoles);
        globalConfig.AiReasonCheckRoles = AiFunctionRoles.Serialize(kModellerRoles);
        await using BunitContext context = CreateContext(Roles.Admin, CreateApiConnection(), globalConfig);
        IRenderedComponent<CascadingAuthenticationState> wrapper = RenderComponent(context);
        wrapper.WaitForState(() => wrapper.FindAll(rolesSelector).Count == 1);

        wrapper.Find(checkboxSelector).Change(true);
        Assert.Multiple(() =>
        {
            Assert.That(wrapper.Find(rolesSelector).Matches(":disabled"), Is.False);
            Assert.That(wrapper.Find(otherRolesSelector).Matches(":disabled"), Is.True);
        });

        wrapper.Find(checkboxSelector).Change(false);
        Assert.Multiple(() =>
        {
            Assert.That(wrapper.Find(rolesSelector).Matches(":disabled"), Is.True);
            Assert.That(wrapper.Find(otherRolesSelector).Matches(":disabled"), Is.True);
            Assert.That(GetAiSettings(wrapper).ChatRoles, Is.EqualTo(kModellerRoles));
            Assert.That(GetAiSettings(wrapper).ReasonCheckRoles, Is.EqualTo(kModellerRoles));
            Assert.That(GetInputText(wrapper, rolesSelector), Is.EqualTo(Roles.Modeller));
        });
    }

    /// <summary>
    /// Verifies that the page shows the stored roles of each AI function in its dropdown, and "all" for a function
    /// without roles.
    /// </summary>
    [Test]
    public async Task SettingsAi_ShowsStoredRolesOfAiFunctions()
    {
        SimulatedGlobalConfig globalConfig = CreateGlobalConfig();
        globalConfig.AiChatRoles = "[\"modeller\"]";
        await using BunitContext context = CreateContext(Roles.Admin, CreateApiConnection(), globalConfig);
        IRenderedComponent<CascadingAuthenticationState> wrapper = RenderComponent(context);

        wrapper.WaitForState(() => GetInputText(wrapper, kChatRolesInputSelector) != null);

        Assert.Multiple(() =>
        {
            Assert.That(GetInputText(wrapper, kChatRolesInputSelector), Is.EqualTo(Roles.Modeller));
            Assert.That(GetInputText(wrapper, kReasonCheckRolesInputSelector), Is.EqualTo(CreateUserConfig(Roles.Admin).GetText("all")));
            Assert.That(GetAiSettings(wrapper).ChatRoles, Is.EqualTo(kModellerRoles));
            Assert.That(GetAiSettings(wrapper).ReasonCheckRoles, Is.Empty);
        });
    }

    /// <summary>
    /// Verifies that selecting a role in the dropdown of the chat adds it to the roles of the chat, and that removing
    /// the selection leaves the chat to every user again.
    /// </summary>
    [Test]
    public async Task SettingsAi_SelectingAndRemovingRoleSetsRolesOfChat()
    {
        await using BunitContext context = CreateContext(Roles.Admin, CreateApiConnection(), CreateGlobalConfig());
        IRenderedComponent<CascadingAuthenticationState> wrapper = RenderComponent(context);

        await OpenChatRolesDropdown(wrapper);
        List<string> choiceTexts = [.. wrapper.FindAll(kChatRoleChoiceSelector).Select(choice => choice.TextContent.Trim())];
        wrapper.FindAll(kChatRoleChoiceSelector).Single(choice => choice.TextContent.Trim() == Roles.Modeller).Click();
        wrapper.WaitForState(() => GetAiSettings(wrapper).ChatRoles.Count == 1);
        List<string> selectedRoles = [.. GetAiSettings(wrapper).ChatRoles];

        wrapper.Find(kSelectedChatRoleSelector).Click();
        wrapper.WaitForState(() => GetAiSettings(wrapper).ChatRoles.Count == 0);

        Assert.Multiple(() =>
        {
            Assert.That(choiceTexts, Is.EqualTo(kOfferedLdapRoles));
            Assert.That(selectedRoles, Is.EqualTo(kModellerRoles));
            Assert.That(GetAiSettings(wrapper).ChatRoles, Is.Empty);
            Assert.That(GetAiSettings(wrapper).ReasonCheckRoles, Is.Empty);
        });
    }

    /// <summary>
    /// Verifies that role names are shown as text, so that markup in a stored role name is not executed.
    /// </summary>
    [Test]
    public async Task SettingsAi_RendersRoleNamesAsText()
    {
        SimulatedGlobalConfig globalConfig = CreateGlobalConfig();
        globalConfig.AiChatRoles = AiFunctionRoles.Serialize(kMarkupPayloadRoles);
        await using BunitContext context = CreateContext(Roles.Admin, CreateApiConnection(), globalConfig);
        IRenderedComponent<CascadingAuthenticationState> wrapper = RenderComponent(context);

        await OpenChatRolesDropdown(wrapper);

        Assert.Multiple(() =>
        {
            Assert.That(wrapper.Find(kSelectedChatRoleSelector).TextContent.Trim(), Is.EqualTo(kMarkupPayload));
            Assert.That(wrapper.FindAll("img"), Is.Empty);
        });
    }

    /// <summary>
    /// Verifies that the roles of the internal LDAP are offered without technical and anonymous roles, together with
    /// stored roles the LDAP does not know, sorted by name.
    /// </summary>
    [Test]
    public async Task OnInitializedAsync_OffersUserRolesOfLdapAndStoredRoles()
    {
        List<(Exception? Exception, string Title, string Message, bool IsError)> messages = [];
        SimulatedGlobalConfig globalConfig = CreateGlobalConfig();
        globalConfig.AiReasonCheckRoles = $"[\"{kStoredOnlyRole}\",\"{Roles.Admin}\"]";
        SettingsAi component = CreateComponent(CreateApiConnection(), globalConfig, messages);

        await InvokePrivateTask(component, "OnInitializedAsync");

        Assert.Multiple(() =>
        {
            Assert.That(GetMember<List<string>>(component, "offeredRoles"), Is.EqualTo(kOfferedRolesWithStoredRole));
            Assert.That(GetMember<bool>(component, "rolesLoaded"), Is.True);
            Assert.That(messages, Is.Empty);
        });
    }

    /// <summary>
    /// Verifies that an LDAP without roles is reported, and that the stored roles are still offered.
    /// </summary>
    [Test]
    public async Task OnInitializedAsync_WithoutLdapRoles_ShowsMessageAndOffersStoredRoles()
    {
        List<(Exception? Exception, string Title, string Message, bool IsError)> messages = [];
        SimulatedGlobalConfig globalConfig = CreateGlobalConfig();
        globalConfig.AiChatRoles = "[\"modeller\"]";
        SettingsAi component = CreateComponent(CreateApiConnection(), globalConfig, messages, new MockMiddlewareClient());

        await InvokePrivateTask(component, "OnInitializedAsync");

        Assert.Multiple(() =>
        {
            Assert.That(messages.Single().IsError, Is.True);
            Assert.That(messages.Single().Message, Is.EqualTo(GetMember<UserConfig>(component, "userConfig").GetText("E5251")));
            Assert.That(GetMember<List<string>>(component, "offeredRoles"), Is.EqualTo(kModellerRoles));
            Assert.That(GetMember<bool>(component, "rolesLoaded"), Is.True);
        });
    }

    /// <summary>
    /// Verifies that a failure to read the roles is shown, and that the role dropdowns are rendered nevertheless.
    /// </summary>
    [Test]
    public async Task OnInitializedAsync_ShowsErrorWhenRolesCannotBeRead()
    {
        List<(Exception? Exception, string Title, string Message, bool IsError)> messages = [];
        MockMiddlewareClient middlewareClient = new() { GetAllRolesException = new HttpRequestException("middleware not reachable") };
        SettingsAi component = CreateComponent(CreateApiConnection(), CreateGlobalConfig(), messages, middlewareClient);

        await InvokePrivateTask(component, "OnInitializedAsync");

        Assert.Multiple(() =>
        {
            Assert.That(messages.Single().Exception, Is.TypeOf<HttpRequestException>());
            Assert.That(messages.Single().IsError, Is.True);
            Assert.That(GetMember<List<string>>(component, "offeredRoles"), Is.Empty);
            Assert.That(GetMember<bool>(component, "rolesLoaded"), Is.True);
        });
    }

    /// <summary>
    /// Verifies that the roles of the AI functions are stored as normalized JSON arrays, which the harness reads back.
    /// </summary>
    [Test]
    public async Task Save_StoresRolesOfAiFunctions()
    {
        List<(Exception? Exception, string Title, string Message, bool IsError)> messages = [];
        RecordingSettingsApiConn apiConnection = CreateApiConnection();
        SettingsAi component = CreateComponent(apiConnection, CreateGlobalConfig(), messages);
        await InvokePrivateTask(component, "OnInitializedAsync");
        AiSettings aiSettings = GetMember<AiSettings>(component, "aiSettings");
        aiSettings.ChatEnabled = true;
        InvokePrivate<object?>(component, "OnChatRolesChanged", kModellerAndAdminRoles);
        InvokePrivate<object?>(component, "OnReasonCheckRolesChanged", kModellerRoles);

        await InvokePrivateTask(component, "Save");

        Dictionary<string, string?> storedValues = apiConnection.LastUpsertConfigItems.ToDictionary(item => item.Key, item => (string?)item.Value);
        AiSettings readBack = AiSettings.FromConfigValues(storedValues);
        Assert.Multiple(() =>
        {
            Assert.That(messages.Single().IsError, Is.False);
            Assert.That(storedValues[AiSettings.kChatRolesKey], Is.EqualTo("[\"admin\",\"modeller\"]"));
            Assert.That(storedValues[AiSettings.kReasonCheckRolesKey], Is.EqualTo("[\"modeller\"]"));
            Assert.That(readBack.ChatRoles, Is.EqualTo(kAdminAndModellerRoles));
            Assert.That(readBack.ReasonCheckRoles, Is.EqualTo(kModellerRoles));
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

    /// <summary>
    /// Returns the CSS selector of an element of the row of an additional header.
    /// </summary>
    private static string GetCustomHeaderSelector(string idPrefix, int rowIndex)
    {
        return $"#{idPrefix}{rowIndex}";
    }

    /// <summary>
    /// Creates valid settings of Anthropic with the API key credential of the tests.
    /// </summary>
    private static AiSettings CreateValidAnthropicSettings()
    {
        return new AiSettings
        {
            Provider = AiProvider.Anthropic,
            Model = kModel,
            CredentialId = kApiKeyCredentialId,
            ContextWindowTokens = 200000
        };
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
    /// Returns the text the input of a dropdown shows, null while it is not rendered.
    /// </summary>
    private static string? GetInputText(IRenderedComponent<CascadingAuthenticationState> wrapper, string inputSelector)
    {
        return wrapper.FindAll(inputSelector).SingleOrDefault()?.GetAttribute("value");
    }

    /// <summary>
    /// Tells whether a checkbox is checked, null while it is not rendered; for waits, which must not assert or throw.
    /// </summary>
    private static bool? IsChecked(IRenderedComponent<CascadingAuthenticationState> wrapper, string checkboxSelector)
    {
        return wrapper.FindAll(checkboxSelector).SingleOrDefault()?.HasAttribute("checked");
    }

    /// <summary>
    /// Tells whether an input is disabled, null while it is not rendered; for waits, which must not assert or throw.
    /// </summary>
    private static bool? IsDisabled(IRenderedComponent<CascadingAuthenticationState> wrapper, string inputSelector)
    {
        return wrapper.FindAll(inputSelector).SingleOrDefault()?.Matches(":disabled");
    }

    /// <summary>
    /// Opens the menu of the role dropdown of the chat by focusing its input and waits until it shows roles.
    /// </summary>
    private static async Task OpenChatRolesDropdown(IRenderedComponent<CascadingAuthenticationState> wrapper)
    {
        wrapper.WaitForState(() => wrapper.FindAll(kChatRolesInputSelector).Count == 1);
        wrapper.Find("#aiChat").Change(true);
        await wrapper.InvokeAsync(() => wrapper.Find(kChatRolesInputSelector).Focus());
        wrapper.WaitForState(() => wrapper.FindAll(kChatRoleChoiceSelector).Count + wrapper.FindAll(kSelectedChatRoleSelector).Count > 0);
    }

    /// <summary>
    /// Creates the middleware client answering the role query with the roles of the test LDAP, technical and
    /// anonymous roles among them.
    /// </summary>
    private static MockMiddlewareClient CreateMiddlewareClient()
    {
        return new MockMiddlewareClient { AllRoles = [.. kLdapRoleNames.Select(MockMiddlewareClient.CreateLdapRole)] };
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
    private static Task OpenCredentialDropdown(IRenderedComponent<CascadingAuthenticationState> wrapper)
    {
        return OpenDropdown(wrapper, kCredentialInputSelector, kCredentialChoiceSelector);
    }

    /// <summary>
    /// Opens the menu of a single-select dropdown by focusing its input and waits until it offers choices.
    /// </summary>
    private static async Task OpenDropdown(IRenderedComponent<CascadingAuthenticationState> wrapper, string inputSelector, string choiceSelector)
    {
        wrapper.WaitForState(() => wrapper.FindAll(inputSelector).Count == 1);
        await wrapper.InvokeAsync(() => wrapper.Find(inputSelector).Focus());
        wrapper.WaitForState(() => wrapper.FindAll(choiceSelector).Count > 0);
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
        context.Services.AddSingleton<MiddlewareClient>(CreateMiddlewareClient());
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
        List<(Exception? Exception, string Title, string Message, bool IsError)>? messages = null, MiddlewareClient? middlewareClient = null)
    {
        SettingsAi component = new();
        SetMember(component, "apiConnection", apiConnection);
        SetMember(component, "globalConfig", globalConfig);
        SetMember(component, "userConfig", CreateUserConfig(Roles.Admin));
        SetMember(component, "middlewareClient", middlewareClient ?? CreateMiddlewareClient());
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
