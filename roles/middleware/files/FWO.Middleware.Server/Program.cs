using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api;
using FWO.Config.File;
using FWO.Logging;
using FWO.Middleware.Server;
using FWO.Middleware.Server.Jobs;
using FWO.Middleware.Server.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Quartz;
using System.Reflection;

// Implicitly call static constructor so background lock process is started
// (static constructor is only called after class is used in any way)
Log.WriteInfo("Startup", "Starting FWO Middleware Server...");

object changesLock = new(); // LOCK

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls(ConfigFile.MiddlewareServerNativeUri ?? throw new ArgumentException("Missing middleware server url on startup."));

// Create Token Generator
JwtWriter jwtWriter = new(ConfigFile.JwtPrivateKey);

// Create JWT for middleware-server API calls (relevant part is the role middleware-server) and add it to the Api connection header. 
ApiConnection apiConnection = new GraphQlApiConnection(ConfigFile.ApiServerUri ?? throw new ArgumentException("Missing api server url on startup."), jwtWriter.CreateJWTMiddlewareServer());

List<Ldap> connectedLdaps = [];
int connectionAttemptsCount = 1;
while (true)
{
    // Repeat first api call in case graphql api is not started yet
    try
    {
        connectedLdaps = apiConnection.SendQueryAsync<List<Ldap>>(AuthQueries.getAllLdapConnections).Result;
        break;
    }
    catch (Exception ex)
    {
        Log.WriteError("Graphql api", "Graphql api unreachable.", ex);
        Thread.Sleep(500 * connectionAttemptsCount++);
    }
}

Action<Exception> handleSubscriptionException = exception => Log.WriteError("Subscription", "Subscription lead to exception.", exception);
GraphQlApiSubscription<List<Ldap>>.SubscriptionUpdate connectedLdapsSubscriptionUpdate = (List<Ldap> ldapsChanges) => { lock (changesLock) { connectedLdaps = ldapsChanges; } };
GraphQlApiSubscription<List<Ldap>> connectedLdapsSubscription = apiConnection.GetSubscription<List<Ldap>>(handleSubscriptionException, connectedLdapsSubscriptionUpdate, AuthQueries.getLdapConnectionsSubscription);
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
builder.Services.AddSingleton<ReportSchedulerState>();
builder.Services.AddSingleton<JobExecutionTracker>();

// Register config listeners as singletons (activated at startup)
builder.Services.AddSingleton<ExternalRequestSchedulerService>();
builder.Services.AddSingleton<AutoDiscoverSchedulerService>();
builder.Services.AddSingleton<DailyCheckSchedulerService>();
builder.Services.AddSingleton<ImportAppDataSchedulerService>();
builder.Services.AddSingleton<ImportIpDataSchedulerService>();
builder.Services.AddSingleton<ImportChangeNotifySchedulerService>();
builder.Services.AddSingleton<VarianceAnalysisSchedulerService>();
builder.Services.AddSingleton<ReportSchedulerService>();

// Add services to the container.
builder.Services.AddControllers()
  .AddJsonOptions(jsonOptions =>
  {
      //jsonOptions.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
      jsonOptions.JsonSerializerOptions.PropertyNamingPolicy = null;
  });

builder.Services.AddSingleton<JwtWriter>(jwtWriter);
builder.Services.AddSingleton<List<Ldap>>(connectedLdaps);

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
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "FWO Middleware API Documentation",
        Description = "A documentation of the REST API interface for the FWO Middleware.",
        Version = "v1"
    });
    string documentationPath = Path.Combine(AppContext.BaseDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.xml");
    c.IncludeXmlComments(documentationPath);
});

WebApplication app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseSwagger();
app.UseSwaggerUI(c => { c.SwaggerEndpoint("/swagger/v1/swagger.json", "FWO.Middleware v1"); });

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

await app.RunAsync();