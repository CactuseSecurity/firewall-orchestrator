using FWO.Api.Client;
using FWO.Api.Client.ExceptionHandling;
using FWO.Api.Client.Queries;
using FWO.Config.Api;
using FWO.Config.File;
using FWO.Logging;
using FWO.Middleware.Server;
using FWO.Middleware.Server.OpenApi;
using FWO.Middleware.Server.Services;
using FWO.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Quartz;
using Scalar.AspNetCore;

object changesLock = new(); // LOCK
const string kApiDocsPageRoute = "/api-docs";
const string kApiDocsRoute = "/api-docs/{documentName}.json";

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls(ConfigFile.MiddlewareServerNativeUri ?? throw new ArgumentException("Missing middleware server url on startup."));

// Create Token Generator
TokenLifetimeProvider tokenLifetimeProvider = new();
JwtWriter jwtWriter = new(ConfigFile.JwtPrivateKey);
InternalApiTokenService internalApiTokenService = new(jwtWriter, tokenLifetimeProvider);

// Create JWT for middleware-server API calls (relevant part is the role middleware-server) and add it to the Api connection header. 
ApiConnection apiConnection = new GraphQlApiConnection(ConfigFile.ApiServerUri ?? throw new ArgumentException("Missing api server url on startup."), internalApiTokenService.CreateInitialMiddlewareToken());

List<Ldap> connectedLdaps = [];
int connectionAttemptsCount = 1;
while (true)
{
    // Repeat first api call in case graphql api is not started yet
    try
    {
        connectedLdaps = await apiConnection.SendQueryAsync<List<Ldap>>(AuthQueries.getAllLdapConnections);
        break;
    }
    catch (Exception ex)
    {
        Log.WriteError("Graphql api", "Graphql api unreachable.", ex);
        Thread.Sleep(500 * connectionAttemptsCount++);
    }
}

GraphQlApiSubscription<List<Ldap>>.SubscriptionUpdate connectedLdapsSubscriptionUpdate = (List<Ldap> ldapsChanges) => { lock (changesLock) { connectedLdaps = ldapsChanges; } };

GraphQlApiSubscription<List<Ldap>> connectedLdapsSubscription = apiConnection.GetSubscription<List<Ldap>>(GraphqlExceptionHandler.Handle, connectedLdapsSubscriptionUpdate, AuthQueries.getLdapConnectionsSubscription);
Log.WriteInfo("Found ldap connection to server", string.Join("\n", connectedLdaps.ConvertAll(ldap => $"{ldap.Address}:{ldap.Port}")));

// GlobalConfig for Quartz DI
GlobalConfig globalConfig = await GlobalConfig.ConstructAsync(apiConnection, true);

builder.Services.AddQuartz();
builder.Services.AddQuartzHostedService(options =>
{
    options.WaitForJobsToComplete = true;
});

// Register singletons for DI
builder.Services.AddSingleton(apiConnection);
builder.Services.AddSingleton(globalConfig);
builder.Services.AddSingleton<FlowSync>();
builder.Services.AddSingleton<JobExecutionTracker>();
builder.Services.AddSingleton<ComplianceCheckStatusTracker>();
builder.Services.AddSingleton(tokenLifetimeProvider);
builder.Services.AddSingleton(internalApiTokenService);
builder.Services.AddHostedService<InternalApiTokenRefreshService>();

// Register config listeners as singletons (activated at startup)
builder.Services.AddSingleton<ExternalRequestSchedulerService>();
builder.Services.AddSingleton<AutoDiscoverSchedulerService>();
builder.Services.AddSingleton<DailyCheckSchedulerService>();
builder.Services.AddSingleton<ImportAppDataSchedulerService>();
builder.Services.AddSingleton<ImportIpDataSchedulerService>();
builder.Services.AddSingleton<ImportChangeNotifySchedulerService>();
builder.Services.AddSingleton<VarianceAnalysisSchedulerService>();
builder.Services.AddSingleton<ReportSchedulerService>();
builder.Services.AddSingleton<ComplianceSchedulerService>();
builder.Services.AddSingleton<UpdateRuleOwnerMappingSchedulerService>();
builder.Services.AddSingleton<UpdateFlowsSchedulerService>();

// Add services to the container.
builder.Services.AddControllers()
  .AddJsonOptions(jsonOptions =>
  {
      ApiDocumentationJsonOptions.Configure(jsonOptions);
  });

builder.Services.AddSingleton<JwtWriter>(jwtWriter);
builder.Services.AddSingleton<List<Ldap>>(connectedLdaps);
builder.Services.AddSingleton<FlowCatalogService>();
builder.Services.AddSingleton<FlowComplianceService>();
builder.Services.AddSingleton<FlowRequestService>();
builder.Services.AddApiExamples();

builder.Services.AddAuthentication(confOptions =>
{
    confOptions.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    confOptions.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(confOptions =>
{
    confOptions.TokenValidationParameters = new TokenValidationParameters
    {
        RequireExpirationTime = true,
        RequireSignedTokens = true,
        ValidateAudience = true,
        ValidAudiences = [FWO.Basics.JwtConstants.Audience],
        ValidateIssuer = true,
        ValidIssuers = [FWO.Basics.JwtConstants.Issuer],
        ValidateLifetime = true,
        RoleClaimType = "role",
        IssuerSigningKey = ConfigFile.JwtPublicKey
    };
});
builder.Services.AddOpenApi("v1", options =>
{
    options.AddOperationTransformer<OpenApiOperationNameTransformer>();
    options.AddOperationTransformer<OpenApiAuthorizationOperationTransformer>();
    options.AddOperationTransformer<OpenApiApiExampleOperationTransformer>();
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Info = new OpenApiInfo
        {
            Title = "FWO Middleware API Documentation",
            // Top-level Markdown headings ("# ...") in the description are rendered by Scalar as
            // their own sidebar sections at the same level as the auto-generated "Introduction".
            // The "Authentication" section documents the single, top-level bearer scheme.
            Description =
                "This is the REST API interface for the Firewall Orchestrator (FWO) Middleware Server. " +
                "The middleware server brokers communication between the FWO UI, the data layer and the " +
                "automation routines. It exposes endpoints for authentication and JWT issuance, " +
                "authorization (roles, groups and tenants), user and credential management, scheduling, " +
                "rule compliance checks, reporting and change-request workflows.\n\n" +
                "Use this interactive documentation to explore the available endpoints, inspect their " +
                "request and response schemas, and send live test requests directly from your browser. " +
                "Every request requires a valid JWT — see the **Authentication** section below on how to " +
                "obtain and apply one.\n\n" +
                "## Authentication\n\n" +
                "All endpoints are protected by a single JWT bearer scheme. In this API documentation, there is no need to " +
                "add an individual authorization header to each endpoint. Instead, set the token " +
                "once in the top-level **Authentication** field at the top of this page and it is " +
                "automatically applied to every request you send.\n\n" +
                "To obtain a token, call `POST /api/AuthenticationToken/GetTokenPair` and paste only " +
                "the returned `accessToken` value into the Authentication field (without the " +
                "`Bearer` prefix and without the `refreshToken`).",
            Version = "v1"
        };

        OpenApiComponents components = document.Components ??= new OpenApiComponents();
        components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        components.SecuritySchemes["bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "JWT Authorization header using the Bearer scheme."
        };

        return Task.CompletedTask;
    });
});

WebApplication app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseForwardedHeaders(ReverseProxyForwardingOptions.Create());

app.MapOpenApi(kApiDocsRoute);
app.MapScalarApiReference(kApiDocsPageRoute, options =>
{
    options.WithTitle("FWO Middleware API Documentation")
        .WithOpenApiRoutePattern(kApiDocsRoute)
        .AddPreferredSecuritySchemes(["bearer"])
        .AddHttpAuthentication("bearer", scheme =>
        {
            scheme.WithDescription("Paste only the accessToken value returned by /api/AuthenticationToken/GetTokenPair. Do not paste the refreshToken or add the Bearer prefix.");
        })
        .EnablePersistentAuthentication();
});
app.UseSwaggerRedirect(kApiDocsPageRoute);

//app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

//Register JobExecutionTracker with scheduler
ISchedulerFactory schedulerFactory = app.Services.GetRequiredService<ISchedulerFactory>();
JobExecutionTracker executionTracker = app.Services.GetRequiredService<JobExecutionTracker>();
IScheduler scheduler = await schedulerFactory.GetScheduler();
scheduler.ListenerManager.AddJobListener(executionTracker);

// Activate config listeners so they attach subscriptions after startup
app.Services.GetRequiredService<ExternalRequestSchedulerService>();
app.Services.GetRequiredService<AutoDiscoverSchedulerService>();
app.Services.GetRequiredService<DailyCheckSchedulerService>();
app.Services.GetRequiredService<ImportAppDataSchedulerService>();
app.Services.GetRequiredService<ImportIpDataSchedulerService>();
app.Services.GetRequiredService<ImportChangeNotifySchedulerService>();
app.Services.GetRequiredService<VarianceAnalysisSchedulerService>();
app.Services.GetRequiredService<ReportSchedulerService>();
app.Services.GetRequiredService<ComplianceSchedulerService>();
app.Services.GetRequiredService<UpdateRuleOwnerMappingSchedulerService>();
app.Services.GetRequiredService<UpdateFlowsSchedulerService>();

await app.RunAsync();

namespace FWO.Middleware.ServerTest
{
    /// <summary>
    /// Entry point for the FWO Middleware Server application to make it accessible for testing
    /// </summary>
    public partial class Program
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Program"/> class.
        /// Protected constructor to allow partial class for testing.
        /// </summary>
        protected Program()
        {

        }
    }
}
