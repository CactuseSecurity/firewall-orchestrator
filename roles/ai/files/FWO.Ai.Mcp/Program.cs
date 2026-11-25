using FWO.Ai.Mcp;
using FWO.Basics;
using FWO.Config.File;
using FWO.Logging;

const string kListenerStartupLogCategory = "MCP server startup";
const string kListenerPurpose = "AI MCP server listener (ASPNETCORE_URLS)";
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

// JWT auth
builder.Services.AddFwoJwtAuthentication(ConfigFile.JwtPublicKey);

// MCP services: the transport to use (http), the tools and prompts to register and the rate limits per user.
builder.Services.AddFwoMcpServer(McpRateLimitSettings.FromConfigFile());

WebApplication app = builder.Build();
// no https redirection: Kestrel listens on loopback http only, behind the Apache reverse proxy that terminates TLS
app.MapFwoMcp();

app.Run();
return 0;
