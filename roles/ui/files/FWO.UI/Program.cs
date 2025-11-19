using BlazorTable;
using FWO.Api.Client;
using FWO.Config.Api;
using FWO.Config.File;
using FWO.Data.Middleware;
using FWO.Logging;
using FWO.Middleware.Client;
using FWO.Services;
using FWO.Services.EventMediator;
using FWO.Services.EventMediator.Interfaces;
using FWO.Services.RuleTreeBuilder;
using FWO.Ui.Auth;
using FWO.Ui.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.Circuits;
using RestSharp;


// Implicitly call static constructor so background lock process is started
// (static constructor is only called after class is used in any way)
Log.WriteInfo("Startup", "Starting FWO UI Server...");

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseWebRoot("wwwroot").UseStaticWebAssets();

// explicitly set the port to listen on
// this can be change for debugging purposes (to allow for a second instance of the UI to run)
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.ListenLocalhost(5000); // Listen on port 5000
});

/// Add services to the container.
#region Services

// CORS configuration (allows acccess from a client to an address which is not the own address - proxies etc.)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowRemoteOrigins", builder =>
    {
        builder.WithOrigins(ConfigFile.RemoteAddresses);
    });
});

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

builder.Services.AddScoped<AuthenticationStateProvider, AuthStateProvider>();
builder.Services.AddScoped<CircuitHandler, CircuitHandlerService>();
builder.Services.AddScoped<KeyboardInputService, KeyboardInputService>();
builder.Services.AddScoped<IEventMediator, EventMediator>();

builder.Services.AddTransient<IRuleTreeBuilder, RuleTreeBuilder>();

string ApiUri = ConfigFile.ApiServerUri;
string MiddlewareUri = ConfigFile.MiddlewareServerUri;
string ProductVersion = ConfigFile.ProductVersion;

builder.Services.AddScoped<ApiConnection>(_ => new GraphQlApiConnection(ApiUri));
builder.Services.AddScoped<MiddlewareClient>(_ => new MiddlewareClient(MiddlewareUri));
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<ITokenRefreshService>(_ => _.GetRequiredService<TokenService>());

// Create "anonymous" (empty) jwt
MiddlewareClient middlewareClient = new MiddlewareClient(MiddlewareUri);
ApiConnection apiConn = new GraphQlApiConnection(ApiUri);

RestResponse<TokenPair> createJWTResponse = middlewareClient.CreateInitialJWT().Result;
bool connectionEstablished = createJWTResponse.IsSuccessful;
int connectionAttemptsCount = 1;
while (!connectionEstablished)
{
    Log.WriteError("Middleware Server Connection",
    $"Error while authenticating as anonymous user from UI (Attempt {connectionAttemptsCount}), "
    + $"Uri: {createJWTResponse.ResponseUri?.AbsoluteUri}, "
    + $"HttpStatus: {createJWTResponse.StatusDescription}, "
    + $"Error: {createJWTResponse.ErrorMessage}");
    Thread.Sleep(500 * connectionAttemptsCount++);
    createJWTResponse = middlewareClient.CreateInitialJWT().Result;
    connectionEstablished = createJWTResponse.IsSuccessful;
}

TokenPair tokenPair = System.Text.Json.JsonSerializer.Deserialize<TokenPair>(createJWTResponse.Content) ?? throw new ArgumentException("failed to deserialize token pair");

string jwt = tokenPair.AccessToken ?? throw new ArgumentException("Received empty jwt.");
apiConn.SetAuthHeader(jwt);

// Get all non-confidential configuration settings and add to a global service (for all users)
GlobalConfig globalConfig = Task.Run(async () => await GlobalConfig.ConstructAsync(jwt, true, true)).Result;
builder.Services.AddSingleton<GlobalConfig>(_ => globalConfig);
builder.Services.AddSingleton<IUrlSanitizer, UrlSanitizer>();

// the user's personal config
builder.Services.AddScoped<UserConfig>(_ => new UserConfig(globalConfig));

builder.Services.AddScoped(_ => new NetworkZoneService());
builder.Services.AddScoped(_ => new DomEventService());

builder.Services.AddBlazorTable();

#endregion

var app = builder.Build();

// Make ServiceProvider accessible via static reference.

FWO.Services.ServiceProvider.UiServices = app.Services;

//// Configure the HTTP request pipeline.
#region HTTP Request Pipeline

Log.WriteInfo("Environment", $"{app.Environment.ApplicationName} runs in {app.Environment.EnvironmentName} Mode.");

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    // app.UseHsts();
}

// app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseWhen(
    ctx => !ctx.Request.Path.StartsWithSegments("/_blazor") &&
           !ctx.Request.Path.StartsWithSegments("/_framework") &&
           !ctx.Request.Path.StartsWithSegments("/css") &&
           !ctx.Request.Path.StartsWithSegments("/js") &&
           !ctx.Request.Path.StartsWithSegments("/images"),
    branch =>
    {
        branch.UseMiddleware<UrlSanitizerMiddleware>();
    });

app.UseAuthentication();
app.UseAuthorization();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

#endregion

app.Run();
