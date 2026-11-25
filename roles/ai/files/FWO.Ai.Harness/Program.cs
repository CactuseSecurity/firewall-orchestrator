using FWO.Ai.Harness.Hosting;
using FWO.Basics;
using FWO.Config.File;
using FWO.Logging;

const string kAspNetCoreLogCategory = "Microsoft.AspNetCore";
const string kListenerStartupLogCategory = "Harness startup";
const string kListenerPurpose = "AI harness listener (ASPNETCORE_URLS)";
// EX_CONFIG from sysexits(3): the service was configured to receive access tokens in clear text from the network.
const int kInsecureListenerExitCode = 78;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// The JWT of the user arrives on this listener, so it must not take it in clear text from the network. The installer
// binds it to loopback behind the Apache reverse proxy of the middleware, which terminates https.
try
{
    EndpointGuard.RequireHttpsOrLoopbackListener(builder.Configuration, kListenerPurpose);
}
catch (ArgumentException exception)
{
    Log.WriteError(kListenerStartupLogCategory, exception.Message);
    return kInsecureListenerExitCode;
}

HarnessSettings settings = HarnessSettings.FromConfiguration(builder.Configuration);

builder.Logging.AddFilter(kAspNetCoreLogCategory, LogLevel.Warning);

// JWT auth: the JWTs of the middleware server, as accepted by the MCP server
builder.Services.AddFwoJwtAuthentication(ConfigFile.JwtPublicKey);

// AG-UI services: the agent of the FWO users
builder.Services.AddFwoAgentServer(settings, new AgentFactoryHolder());
builder.Services.AddFwoAiSettingsRefresh(settings);

WebApplication app = builder.Build();
app.MapFwoAgent();

app.Run();
return 0;
