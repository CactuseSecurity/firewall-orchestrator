using Bunit;
using FWO.Api.Client;
using FWO.Config.Api;
using FWO.Data.Middleware;
using FWO.Middleware.Client;
using FWO.Services.EventMediator;
using FWO.Test.Mocks;
using FWO.Ui.Auth;
using FWO.Ui.Pages;
using FWO.Ui.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using NUnit.Framework;
using RestSharp;
using System.Net;
using System.Reflection;
using System.Security.Authentication;

namespace FWO.Test;

[TestFixture]
[NonParallelizable]
public sealed class UiLoginLogoutTest
{
    [Test]
    public void Login_FirstRender_ShowsLoginFormAndSanitizesWelcomeMessage()
    {
        GlobalConfig globalConfig = new()
        {
            WelcomeMessage = "Welcome <script>alert('xss')</script><strong>user</strong>"
        };
        UserConfig userConfig = UserConfig.ForTextOnly(globalConfig, registerOnChangeHandler: false);

        using BunitContext context = CreateContext(globalConfig, userConfig, out _, out _);

        IRenderedComponent<Login> rendered = context.Render<Login>();

        Assert.That(rendered.Find("#UsernameInput"), Is.Not.Null);
        Assert.That(rendered.Find("#PasswordInput"), Is.Not.Null);
        Assert.That(rendered.Find(".alert-primary").InnerHtml, Does.Contain("<strong>user</strong>"));
        Assert.That(rendered.Find(".alert-primary").InnerHtml, Does.Not.Contain("<script>"));
    }

    [Test]
    public void Login_CustomLogo_ShowsConfiguredLogo()
    {
        GlobalConfig globalConfig = new() { UseCustomLogo = true, CustomLogoData = "custom-logo" };
        UserConfig userConfig = UserConfig.ForTextOnly(globalConfig, registerOnChangeHandler: false);
        using BunitContext context = CreateContext(globalConfig, userConfig, out _, out _);

        IRenderedComponent<Login> rendered = context.Render<Login>();

        Assert.That(rendered.Find("img").GetAttribute("src"), Is.EqualTo("data:image/png;base64, custom-logo"));
    }

    [Test]
    public async Task Login_InputHandlers_UpdateValuesAndClearInvalidState()
    {
        GlobalConfig globalConfig = new();
        UserConfig userConfig = UserConfig.ForTextOnly(globalConfig, registerOnChangeHandler: false);
        using BunitContext context = CreateContext(globalConfig, userConfig, out _, out _);
        Login component = context.Render<Login>().Instance;
        SetPrivateField(component, "InputClass", "is-invalid");

        await InvokePrivateTask(component, "OnUsernameInput", new ChangeEventArgs { Value = "alice" });
        await InvokePrivateTask(component, "OnPasswordInput", new ChangeEventArgs { Value = "secret" });
        await InvokePrivateTask(component, "OnOldPasswordInput", new ChangeEventArgs { Value = "old" });
        await InvokePrivateTask(component, "OnNewPassword1Input", new ChangeEventArgs { Value = "new1" });
        await InvokePrivateTask(component, "OnNewPassword2Input", new ChangeEventArgs { Value = "new2" });

        Assert.Multiple(() =>
        {
            Assert.That(GetPrivateField<string>(component, "Username"), Is.EqualTo("alice"));
            Assert.That(GetPrivateField<string>(component, "Password"), Is.EqualTo("secret"));
            Assert.That(GetPrivateField<string>(component, "oldPassword"), Is.EqualTo("old"));
            Assert.That(GetPrivateField<string>(component, "newPassword1"), Is.EqualTo("new1"));
            Assert.That(GetPrivateField<string>(component, "newPassword2"), Is.EqualTo("new2"));
            Assert.That(GetPrivateField<string>(component, "InputClass"), Is.Empty);
        });
    }

    [Test]
    public async Task Login_SubmitWithContentError_ShowsInvalidInputs()
    {
        GlobalConfig globalConfig = new();
        UserConfig userConfig = UserConfig.ForTextOnly(globalConfig, registerOnChangeHandler: false);
        using BunitContext context = CreateContext(globalConfig, userConfig, out MockMiddlewareClient middlewareClient, out _);
        middlewareClient.AuthenticateUserResponse = CreateAuthenticationResponse(HttpStatusCode.BadRequest, "invalid_credentials");
        Login component = context.Render<Login>().Instance;

        await InvokePrivateTask(component, "LoginSubmit");

        Assert.Multiple(() =>
        {
            Assert.That(middlewareClient.AuthenticateUserCallCount, Is.EqualTo(1));
            Assert.That(GetPrivateField<string>(component, "errorMessage"), Is.Not.Empty);
            Assert.That(GetPrivateField<string>(component, "InputClass"), Is.EqualTo("is-invalid"));
            Assert.That(GetPrivateField<bool>(component, "loginInProgress"), Is.False);
        });
    }

    [Test]
    public async Task Login_SubmitWithoutContentError_ShowsGenericError()
    {
        GlobalConfig globalConfig = new();
        UserConfig userConfig = UserConfig.ForTextOnly(globalConfig, registerOnChangeHandler: false);
        using BunitContext context = CreateContext(globalConfig, userConfig, out MockMiddlewareClient middlewareClient, out _);
        middlewareClient.AuthenticateUserResponse = CreateAuthenticationResponse(HttpStatusCode.BadRequest, null);
        Login component = context.Render<Login>().Instance;

        await InvokePrivateTask(component, "LoginSubmit");

        Assert.That(GetPrivateField<string>(component, "errorMessage"), Is.Not.Empty);
    }

    [Test]
    public async Task Login_SubmitAuthenticationException_ShowsMappedError()
    {
        GlobalConfig globalConfig = new();
        UserConfig userConfig = UserConfig.ForTextOnly(globalConfig, registerOnChangeHandler: false);
        using BunitContext context = CreateContext(globalConfig, userConfig, out MockMiddlewareClient middlewareClient, out _);
        middlewareClient.AuthenticateUserException = new AuthenticationException("invalid_credentials");
        Login component = context.Render<Login>().Instance;

        await InvokePrivateTask(component, "LoginSubmit");

        Assert.That(GetPrivateField<string>(component, "errorMessage"), Is.Not.Empty);
    }

    [Test]
    public async Task Login_SubmitUnexpectedException_ShowsGenericError()
    {
        GlobalConfig globalConfig = new();
        UserConfig userConfig = UserConfig.ForTextOnly(globalConfig, registerOnChangeHandler: false);
        using BunitContext context = CreateContext(globalConfig, userConfig, out MockMiddlewareClient middlewareClient, out _);
        middlewareClient.AuthenticateUserException = new InvalidOperationException("unexpected failure");
        Login component = context.Render<Login>().Instance;

        await InvokePrivateTask(component, "LoginSubmit");

        Assert.Multiple(() =>
        {
            Assert.That(GetPrivateField<string>(component, "errorMessage"), Is.Not.Empty);
            Assert.That(GetPrivateField<bool>(component, "loginInProgress"), Is.False);
        });
    }

    [Test]
    public async Task Login_ChangePasswordSuccess_HidesChangeForm()
    {
        GlobalConfig globalConfig = new();
        UserConfig userConfig = UserConfig.ForTextOnly(globalConfig, registerOnChangeHandler: false);
        using BunitContext context = CreateContext(globalConfig, userConfig, out MockMiddlewareClient middlewareClient, out _);
        Login component = context.Render<Login>().Instance;
        SetPrivateField(component, "showPasswordChangeForm", true);
        SetPrivateField(component, "oldPassword", "OldPassword123!");
        SetPrivateField(component, "newPassword1", "NewPassword123!");
        SetPrivateField(component, "newPassword2", "NewPassword123!");

        await InvokePrivateTask(component, "ChangePassword");

        Assert.Multiple(() =>
        {
            Assert.That(middlewareClient.ChangePasswordCallCount, Is.EqualTo(1));
            Assert.That(GetPrivateField<bool>(component, "showPasswordChangeForm"), Is.False);
            Assert.That(GetPrivateField<bool>(component, "passwordChangeInProgress"), Is.False);
        });
    }

    [Test]
    public async Task Login_ChangePasswordException_ShowsExceptionMessage()
    {
        GlobalConfig globalConfig = new();
        UserConfig userConfig = UserConfig.ForTextOnly(globalConfig, registerOnChangeHandler: false);
        using BunitContext context = CreateContext(globalConfig, userConfig, out MockMiddlewareClient middlewareClient, out _);
        middlewareClient.ChangePasswordException = new InvalidOperationException("password service unavailable");
        Login component = context.Render<Login>().Instance;
        SetPrivateField(component, "showPasswordChangeForm", true);
        SetPrivateField(component, "oldPassword", "OldPassword123!");
        SetPrivateField(component, "newPassword1", "NewPassword123!");
        SetPrivateField(component, "newPassword2", "NewPassword123!");

        await InvokePrivateTask(component, "ChangePassword");

        Assert.That(GetPrivateField<string>(component, "chgPwErrorMessage"), Is.EqualTo("password service unavailable"));
    }

    [Test]
    public void Logout_FirstRender_RevokesStoredTokensAndNavigatesToLogin()
    {
        GlobalConfig globalConfig = new();
        UserConfig userConfig = UserConfig.ForTextOnly(globalConfig, registerOnChangeHandler: false);
        using BunitContext context = CreateContext(globalConfig, userConfig, out MockMiddlewareClient middlewareClient, out TokenService tokenService);

        tokenService.SetTokenPair(new TokenPair
        {
            AccessToken = "access-token",
            RefreshToken = "refresh-token",
            AccessTokenExpires = DateTime.UtcNow.AddMinutes(5),
            RefreshTokenExpires = DateTime.UtcNow.AddMinutes(5)
        }).GetAwaiter().GetResult();

        IRenderedComponent<Logout> rendered = context.Render<Logout>();

        Assert.That(rendered.Markup, Does.Contain("<h3>Logout</h3>"));
        Assert.That(middlewareClient.RevokeRefreshTokenCallCount, Is.EqualTo(1));
        Assert.That(context.Services.GetRequiredService<TestNavigationManager>().Uri, Is.EqualTo("http://localhost/"));
    }

    private static BunitContext CreateContext(GlobalConfig globalConfig, UserConfig userConfig,
        out MockMiddlewareClient middlewareClient, out TokenService tokenService)
    {
        BunitContext context = new();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddScoped(_ => context.JSInterop.JSRuntime);

        middlewareClient = new MockMiddlewareClient();
        MockProtectedSessionStorage sessionStorage = new();
        tokenService = new TokenService(middlewareClient, sessionStorage);
        TestNavigationManager navigationManager = new();
        AuthStateProvider authStateProvider = new(tokenService, new EventMediator(), navigationManager);

        context.Services.AddSingleton<MiddlewareClient>(middlewareClient);
        context.Services.AddSingleton<AuthenticationStateProvider>(authStateProvider);
        context.Services.AddSingleton<ApiConnection>(new MockApiConnection());
        context.Services.AddSingleton(globalConfig);
        context.Services.AddSingleton(userConfig);
        context.Services.AddSingleton(new UiClientAddress());
        context.Services.AddSingleton<NavigationManager>(navigationManager);
        context.Services.AddSingleton(navigationManager);
        return context;
    }

    private sealed class TestNavigationManager : NavigationManager
    {
        public TestNavigationManager()
        {
            Initialize("http://localhost/", "http://localhost/");
        }

        protected override void NavigateToCore(string uri, bool forceLoad)
        {
            Uri = ToAbsoluteUri(uri).ToString();
        }

        protected override void NavigateToCore(string uri, NavigationOptions options)
        {
            Uri = ToAbsoluteUri(uri).ToString();
        }
    }

    private static RestResponse<TokenPair> CreateAuthenticationResponse(HttpStatusCode statusCode, string? content)
    {
        return new RestResponse<TokenPair>(new RestRequest())
        {
            StatusCode = statusCode,
            Content = content,
            ResponseStatus = ResponseStatus.Completed,
            IsSuccessStatusCode = statusCode == HttpStatusCode.OK
        };
    }

    private static async Task InvokePrivateTask(object instance, string methodName, params object?[] parameters)
    {
        MethodInfo method = instance.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new MissingMethodException(instance.GetType().FullName, methodName);
        object? result = method.Invoke(instance, parameters);
        if (result is Task task)
        {
            await task;
        }
    }

    private static T GetPrivateField<T>(object instance, string fieldName)
    {
        FieldInfo field = instance.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new MissingFieldException(instance.GetType().FullName, fieldName);
        return (T)(field.GetValue(instance) ?? throw new InvalidOperationException($"Field {fieldName} is null."));
    }

    private static void SetPrivateField<T>(object instance, string fieldName, T value)
    {
        FieldInfo field = instance.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new MissingFieldException(instance.GetType().FullName, fieldName);
        field.SetValue(instance, value);
    }
}
