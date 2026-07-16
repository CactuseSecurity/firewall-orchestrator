using Microsoft.Playwright;
using NUnit.Framework;

namespace FWO.Ui.AmbientRoleSmoke;

/// <summary>
/// Browser smoke tests for UI pages that need an explicit or ambient API role.
/// </summary>
[TestFixture]
public class AmbientRoleSmokeTest
{
    private static readonly string[] DefaultRoutes =
    [
        "/settings/user",
        "/settings/language",
        "/settings/report",
        "/settings/modellingpersonal",
        "/settings/recertificationpersonal",
        "/report/generation",
        "/report/schedule",
        "/report/archive",
        "/networkmodelling",
        "/certification",
        "/request/tickets",
        "/request/ticketsoverview",
        "/request/approvals",
        "/request/plannings",
        "/request/implementations",
        "/request/reviews",
        "/monitoring",
        "/monitoring/import_status",
        "/monitoring/alerts",
        "/monitoring/modelling_requests",
        "/monitoring/requested_interfaces",
        "/compliance/checks",
        "/compliance/matrix",
        "/compliance/policies",
        "/network_analysis",
    ];

    private static readonly string[] RoleErrorPatterns =
    [
        "requires an explicit role",
        "User has none of the required roles",
        "x-hasura-role",
        "GraphQL API call requires",
    ];

    private static readonly string[] IgnoredConsoleFragments =
    [
        "favicon",
        "DevTools failed to load source map",
    ];

    private const int kSpinnerTimeoutMs = 10000;
    private const int kLoginTimeoutMs = 30000;

    /// <summary>
    /// Logs in and visits every configured route with a multi-role user.
    /// </summary>
    [Test]
    public async Task MultiRoleUserCanVisitMainPages()
    {
        SmokeTestConfig config = SmokeTestConfig.FromEnvironment();
        using IPlaywright playwright = await Playwright.CreateAsync();
        await using IBrowser browser = await LaunchBrowser(playwright, config);
        await using IBrowserContext context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = config.IgnoreHttpsErrors,
        });
        context.SetDefaultTimeout(config.TimeoutMs);
        IPage page = await context.NewPageAsync();

        BrowserErrors errors = BrowserErrors.Empty();
        AttachErrorHandlers(page, errors);

        await Login(page, config);
        foreach (string route in config.Routes)
        {
            errors.Clear();
            await GoToRoute(page, config.BaseUrl, route, config.TimeoutMs);
            await RunSafeRouteAction(page, route);
            await WaitForUiToSettle(page);
            await errors.AssertClean(route, page);
        }
    }

    /// <summary>
    /// Starts the browser selected by the smoke test configuration.
    /// </summary>
    private static async Task<IBrowser> LaunchBrowser(IPlaywright playwright, SmokeTestConfig config)
    {
        BrowserTypeLaunchOptions options = new()
        {
            Args = ["--no-sandbox"],
            ExecutablePath = string.IsNullOrWhiteSpace(config.BrowserExecutable) ? null : config.BrowserExecutable,
            Headless = config.Headless,
            Timeout = config.TimeoutMs,
        };

        return await playwright.Chromium.LaunchAsync(options);
    }

    /// <summary>
    /// Signs in to the UI with the configured credentials.
    /// </summary>
    private static async Task Login(IPage page, SmokeTestConfig config)
    {
        string loginUrl = BuildUrl(config.BaseUrl, "login");
        IResponse? loginResponse = await page.GotoAsync(loginUrl, new PageGotoOptions
        {
            Timeout = config.TimeoutMs,
            WaitUntil = WaitUntilState.DOMContentLoaded,
        });

        await page.FillAsync("#UsernameInput", config.Username);
        await page.FillAsync("#PasswordInput", config.Password);
        await page.ClickAsync("button[type='submit']");

        try
        {
            await page.Locator("#UsernameInput").WaitForAsync(new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Hidden,
                Timeout = kLoginTimeoutMs,
            });
        }
        catch (TimeoutException exception)
        {
            await FailWithLoginDiagnostics(page, config.Username, loginUrl, loginResponse, exception);
        }

        if (loginResponse is not null && loginResponse.Status >= 400)
        {
            Assert.Fail($"Login page returned HTTP {loginResponse.Status}: {loginUrl}");
            return;
        }

        await WaitForUiToSettle(page);
    }

    /// <summary>
    /// Emits visible page diagnostics when login did not complete.
    /// </summary>
    private static async Task FailWithLoginDiagnostics(
        IPage page,
        string username,
        string loginUrl,
        IResponse? loginResponse,
        Exception exception)
    {
        string loginStatus = loginResponse is null ? "no response" : loginResponse.Status.ToString();
        string bodyText = await GetBodyText(page);
        string visibleText = string.Join(
            Environment.NewLine,
            bodyText.Split(Environment.NewLine).Select(line => line.Trim()).Where(line => line.Length > 0));

        Assert.Fail(
            "Login form was still visible after submitting credentials for "
            + $"{username} at {loginUrl}. Current URL: {page.Url}. "
            + $"Login page status: {loginStatus}.{Environment.NewLine}"
            + $"Visible page text:{Environment.NewLine}{visibleText}{Environment.NewLine}"
            + $"Original assertion: {exception.Message}");
    }

    /// <summary>
    /// Navigates to a route and fails on HTTP errors for the main route document.
    /// </summary>
    private static async Task GoToRoute(IPage page, string baseUrl, string route, int timeoutMs)
    {
        string targetUrl = BuildUrl(baseUrl, route.TrimStart('/'));
        IResponse? response = await page.GotoAsync(targetUrl, new PageGotoOptions
        {
            Timeout = timeoutMs,
            WaitUntil = WaitUntilState.DOMContentLoaded,
        });

        if (response is not null && response.Status >= 400)
        {
            Assert.Fail($"Route returned HTTP {response.Status}: {targetUrl}");
            return;
        }

        await WaitForUiToSettle(page);
    }

    /// <summary>
    /// Waits until the Blazor page no longer shows common loading indicators.
    /// </summary>
    private static async Task WaitForUiToSettle(IPage page)
    {
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await page.Locator(".spinner-border").WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Detached,
            Timeout = kSpinnerTimeoutMs,
        });
        await page.Locator("text=Loading").WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Detached,
            Timeout = kSpinnerTimeoutMs,
        });
    }

    /// <summary>
    /// Runs harmless route-specific actions that trigger role-dependent API calls.
    /// </summary>
    private static async Task RunSafeRouteAction(IPage page, string route)
    {
        string normalizedRoute = NormalizeRoute(route);
        string[] actionRoutes =
        [
            "/settings/language",
            "/settings/report",
            "/settings/modellingpersonal",
            "/settings/recertificationpersonal",
        ];

        if (!actionRoutes.Contains(normalizedRoute, StringComparer.Ordinal))
        {
            return;
        }

        await ClickFirstPrimaryButton(page);
    }

    /// <summary>
    /// Clicks the first enabled primary action button on the active page.
    /// </summary>
    private static async Task ClickFirstPrimaryButton(IPage page)
    {
        ILocator button = page.Locator("button.btn-primary:not([disabled])").First;
        if (await button.CountAsync() == 0)
        {
            Assert.Fail("Expected an enabled primary action button.");
            return;
        }

        await button.ClickAsync();
        await WaitForUiToSettle(page);
    }

    /// <summary>
    /// Normalizes a route string for action lookup.
    /// </summary>
    private static string NormalizeRoute(string route)
    {
        string routeWithoutQuery = route.Split('?', 2)[0];
        string routeWithoutFragment = routeWithoutQuery.Split('#', 2)[0];
        return "/" + routeWithoutFragment.Trim('/');
    }

    /// <summary>
    /// Attaches browser error collectors to a page.
    /// </summary>
    private static void AttachErrorHandlers(IPage page, BrowserErrors errors)
    {
        page.Console += (_, message) => RecordConsoleError(errors, message.Type, message.Text);
        page.PageError += (_, error) => errors.PageErrors.Add(error);
        page.Response += (_, response) => RecordFailedResponse(errors, response);
    }

    /// <summary>
    /// Records only relevant browser console errors.
    /// </summary>
    private static void RecordConsoleError(BrowserErrors errors, string messageType, string text)
    {
        if (messageType != "error")
        {
            return;
        }

        bool ignored = IgnoredConsoleFragments.Any(fragment => text.Contains(fragment, StringComparison.Ordinal));
        if (!ignored)
        {
            errors.ConsoleErrors.Add($"console error: {text}");
        }
    }

    /// <summary>
    /// Records failed API or GraphQL responses caused by missing roles.
    /// </summary>
    private static void RecordFailedResponse(BrowserErrors errors, IResponse response)
    {
        if (response.Status is not (401 or 403 or 500))
        {
            return;
        }

        if (!response.Url.Contains("/api/", StringComparison.Ordinal)
            && !response.Url.Contains("/v1/graphql", StringComparison.Ordinal))
        {
            return;
        }

        errors.FailedResponses.Add($"{response.Status} {response.Url}");
    }

    /// <summary>
    /// Returns the visible body text.
    /// </summary>
    private static async Task<string> GetBodyText(IPage page)
    {
        return await page.Locator("body").InnerTextAsync(new LocatorInnerTextOptions { Timeout = 5000 });
    }

    /// <summary>
    /// Combines base URL and route without losing the base path.
    /// </summary>
    private static string BuildUrl(string baseUrl, string route)
    {
        return new Uri(new Uri(baseUrl), route).ToString();
    }

    private sealed class BrowserErrors
    {
        public List<string> ConsoleErrors { get; } = [];
        public List<string> PageErrors { get; } = [];
        public List<string> FailedResponses { get; } = [];

        /// <summary>
        /// Creates an empty error collector.
        /// </summary>
        public static BrowserErrors Empty()
        {
            return new BrowserErrors();
        }

        /// <summary>
        /// Clears all collected errors for the next route.
        /// </summary>
        public void Clear()
        {
            ConsoleErrors.Clear();
            PageErrors.Clear();
            FailedResponses.Clear();
        }

        /// <summary>
        /// Fails when the page or browser collected route/API failures.
        /// </summary>
        public async Task AssertClean(string route, IPage page)
        {
            string bodyText = await GetBodyText(page);
            List<string> roleErrors = RoleErrorPatterns
                .Where(pattern => bodyText.Contains(pattern, StringComparison.Ordinal))
                .Select(pattern => $"body contains '{pattern}'")
                .ToList();
            List<string> failures = [.. ConsoleErrors, .. PageErrors, .. FailedResponses, .. roleErrors];
            Assert.That(failures, Is.Empty, $"{route} had browser/API failures:{Environment.NewLine}{string.Join(Environment.NewLine, failures)}");
        }
    }

    private sealed record SmokeTestConfig(
        string BaseUrl,
        string Username,
        string Password,
        IReadOnlyList<string> Routes,
        bool Headless,
        bool IgnoreHttpsErrors,
        int TimeoutMs,
        string BrowserExecutable)
    {
        /// <summary>
        /// Builds smoke-test configuration from environment variables.
        /// </summary>
        public static SmokeTestConfig FromEnvironment()
        {
            string baseUrl = RequireEnvironment("FWO_UI_BASE_URL").Trim();
            string username = RequireEnvironment("FWO_UI_USERNAME").Trim();
            string password = RequireEnvironment("FWO_UI_PASSWORD");
            string normalizedBaseUrl = baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/";

            return new SmokeTestConfig(
                normalizedBaseUrl,
                username,
                password,
                ReadRoutes(),
                ReadBoolean("FWO_UI_HEADLESS", true),
                ReadBoolean("FWO_UI_IGNORE_HTTPS_ERRORS", true),
                ReadInteger("FWO_UI_TIMEOUT_MS", 30000),
                Environment.GetEnvironmentVariable("FWO_UI_BROWSER_EXECUTABLE")?.Trim() ?? string.Empty);
        }

        /// <summary>
        /// Reads a required environment variable or skips the test.
        /// </summary>
        private static string RequireEnvironment(string name)
        {
            string? value = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrWhiteSpace(value))
            {
                Assert.Ignore($"{name} is required for UI ambient role smoke tests.");
            }

            return value;
        }

        /// <summary>
        /// Reads the configured route set or falls back to defaults.
        /// </summary>
        private static IReadOnlyList<string> ReadRoutes()
        {
            string? configuredRoutes = Environment.GetEnvironmentVariable("FWO_UI_ROUTES")?.Trim();
            if (string.IsNullOrEmpty(configuredRoutes))
            {
                return DefaultRoutes;
            }

            return configuredRoutes
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .ToList();
        }

        /// <summary>
        /// Reads an environment boolean with a default value.
        /// </summary>
        private static bool ReadBoolean(string name, bool defaultValue)
        {
            string? value = Environment.GetEnvironmentVariable(name);
            return string.IsNullOrWhiteSpace(value) ? defaultValue : !value.Equals("false", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Reads an environment integer with a default value.
        /// </summary>
        private static int ReadInteger(string name, int defaultValue)
        {
            string? value = Environment.GetEnvironmentVariable(name);
            return int.TryParse(value, out int parsedValue) ? parsedValue : defaultValue;
        }
    }
}
