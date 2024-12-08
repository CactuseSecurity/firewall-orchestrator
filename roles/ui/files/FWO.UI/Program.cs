using BlazorTable;
using FWO.Api.Client;
using FWO.Config.Api;
using FWO.Config.File;
using FWO.Logging;
using FWO.Middleware.Client;
using FWO.Ui.Auth;
using FWO.Ui.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.Circuits;
using RestSharp;
using System.Diagnostics;


// Implicitly call static constructor so background lock process is started
// (static constructor is only called after class is used in any way)
Log.WriteInfo("Startup", "Starting FWO UI Server...");

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseWebRoot("wwwroot").UseStaticWebAssets();

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

string ApiUri = ConfigFile.ApiServerUri;
string MiddlewareUri = ConfigFile.MiddlewareServerUri;
string ProductVersion = ConfigFile.ProductVersion;

builder.Services.AddScoped<ApiConnection>(_ => new GraphQlApiConnection(ApiUri));
builder.Services.AddScoped<MiddlewareClient>(_ => new MiddlewareClient(MiddlewareUri));

// Create "anonymous" (empty) jwt
MiddlewareClient middlewareClient = new MiddlewareClient(MiddlewareUri);
ApiConnection apiConn = new GraphQlApiConnection(ApiUri);

RestResponse<string> createJWTResponse = middlewareClient.CreateInitialJWT().Result;
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

string jwt = createJWTResponse.Data ?? throw new NullReferenceException("Received empty jwt.");
apiConn.SetAuthHeader(jwt);

// Get all non-confidential configuration settings and add to a global service (for all users)
GlobalConfig globalConfig = Task.Run(async () => await GlobalConfig.ConstructAsync(jwt, true, true)).Result;
builder.Services.AddSingleton<GlobalConfig>(_ => globalConfig);

// the user's personal config
builder.Services.AddScoped<UserConfig>(_ => new UserConfig(globalConfig));

builder.Services.AddScoped(_ => new NetworkZoneService());
builder.Services.AddScoped(_ => new DomEventService());

// For processing files and uploading them to the database
builder.Services.AddScoped(sp => new FileUploadService(sp));

builder.Services.AddBlazorTable();

#endregion

var app = builder.Build();

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

app.UseAuthentication();
app.UseAuthorization();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

#endregion

app.Run();
