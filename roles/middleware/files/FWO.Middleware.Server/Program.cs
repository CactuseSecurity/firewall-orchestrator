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

// GlobalConfig for Quartz DI
GlobalConfig globalConfig = await GlobalConfig.ConstructAsync(apiConnection, true);

// Configure Quartz.NET with jobs and triggers
builder.Services.AddQuartz(q =>
{
    q.SchedulerId = "FwoScheduler";

    // DailyCheck
    JobKey dailyJob = new("DailyCheckJob");
    q.AddJob<DailyCheckJob>(opts => opts.WithIdentity(dailyJob));
    DateTimeOffset dailyStart = CalculateForward(globalConfig.DailyCheckStartAt, TimeSpan.FromDays(1));
    q.AddTrigger(opts => opts
        .WithIdentity("DailyCheckTrigger")
        .ForJob(dailyJob)
        .StartAt(dailyStart)
        .WithSimpleSchedule(x => x.WithInterval(TimeSpan.FromDays(1)).RepeatForever()));

    // AutoDiscover
    JobKey autoDiscoverJob = new("AutoDiscoverJob");
    q.AddJob<AutoDiscoverJob>(opts => opts.WithIdentity(autoDiscoverJob).StoreDurably());
    if (globalConfig.AutoDiscoverSleepTime > 0)
    {
        DateTimeOffset adStart = CalculateForward(globalConfig.AutoDiscoverStartAt, TimeSpan.FromHours(globalConfig.AutoDiscoverSleepTime));
        q.AddTrigger(opts => opts
            .WithIdentity("AutoDiscoverTrigger")
            .ForJob(autoDiscoverJob)
            .StartAt(adStart)
            .WithSimpleSchedule(x => x.WithInterval(TimeSpan.FromHours(globalConfig.AutoDiscoverSleepTime)).RepeatForever()));
    }

    // ImportAppData 
    JobKey importAppDataJob = new("ImportAppDataJob");
    q.AddJob<ImportAppDataJob>(opts => opts.WithIdentity(importAppDataJob).StoreDurably());
    if (globalConfig.ImportAppDataSleepTime > 0)
    {
        DateTimeOffset iaStart = CalculateForward(globalConfig.ImportAppDataStartAt, TimeSpan.FromHours(globalConfig.ImportAppDataSleepTime));
        q.AddTrigger(opts => opts
            .WithIdentity("ImportAppDataTrigger")
            .ForJob(importAppDataJob)
            .StartAt(iaStart)
            .WithSimpleSchedule(x => x.WithInterval(TimeSpan.FromHours(globalConfig.ImportAppDataSleepTime)).RepeatForever()));
    }

    // ImportIpData
    JobKey importIpDataJob = new("ImportIpDataJob");
    q.AddJob<ImportIpDataJob>(opts => opts.WithIdentity(importIpDataJob).StoreDurably());
    if (globalConfig.ImportSubnetDataSleepTime > 0)
    {
        DateTimeOffset iidStart = CalculateForward(globalConfig.ImportSubnetDataStartAt, TimeSpan.FromHours(globalConfig.ImportSubnetDataSleepTime));
        q.AddTrigger(opts => opts
            .WithIdentity("ImportIpDataTrigger")
            .ForJob(importIpDataJob)
            .StartAt(iidStart)
            .WithSimpleSchedule(x => x.WithInterval(TimeSpan.FromHours(globalConfig.ImportSubnetDataSleepTime)).RepeatForever()));
    }

    // ImportChangeNotify
    JobKey importChangeNotifyJob = new("ImportChangeNotifyJob");
    q.AddJob<ImportChangeNotifyJob>(opts => opts.WithIdentity(importChangeNotifyJob).StoreDurably());

    if (globalConfig.ImpChangeNotifyActive && globalConfig.ImpChangeNotifySleepTime > 0)
    {
        DateTimeOffset icnStart = CalculateForward(globalConfig.ImpChangeNotifyStartAt, TimeSpan.FromSeconds(globalConfig.ImpChangeNotifySleepTime));
        q.AddTrigger(opts => opts
            .WithIdentity("ImportChangeNotifyTrigger")
            .ForJob(importChangeNotifyJob)
            .StartAt(icnStart)
            .WithSimpleSchedule(x => x.WithInterval(TimeSpan.FromSeconds(globalConfig.ImpChangeNotifySleepTime)).RepeatForever()));
    }

    // ExternalRequest
    JobKey externalRequestJob = new("ExternalRequestJob");
    q.AddJob<ExternalRequestJob>(opts => opts.WithIdentity(externalRequestJob).StoreDurably());

    if (globalConfig.ExternalRequestSleepTime > 0)
    {
        DateTimeOffset erStart = CalculateForward(globalConfig.ExternalRequestStartAt, TimeSpan.FromSeconds(globalConfig.ExternalRequestSleepTime));
        q.AddTrigger(opts => opts
            .WithIdentity("ExternalRequestTrigger")
            .ForJob(externalRequestJob)
            .StartAt(erStart)
            .WithSimpleSchedule(x => x.WithInterval(TimeSpan.FromSeconds(globalConfig.ExternalRequestSleepTime)).RepeatForever()));
    }

    // VarianceAnalysis 
    JobKey varianceAnalysisJob = new("VarianceAnalysisJob");
    q.AddJob<VarianceAnalysisJob>(opts => opts.WithIdentity(varianceAnalysisJob).StoreDurably());
    if (globalConfig.VarianceAnalysisSleepTime > 0)
    {
        DateTimeOffset vaStart = CalculateForward(globalConfig.VarianceAnalysisStartAt, TimeSpan.FromMinutes(globalConfig.VarianceAnalysisSleepTime));
        q.AddTrigger(opts => opts
            .WithIdentity("VarianceAnalysisTrigger")
            .ForJob(varianceAnalysisJob)
            .StartAt(vaStart)
            .WithSimpleSchedule(x => x.WithInterval(TimeSpan.FromMinutes(globalConfig.VarianceAnalysisSleepTime)).RepeatForever()));
    }

    // Report (every minute)
    JobKey reportJob = new("ReportJob");
    q.AddJob<ReportJob>(opts => opts.WithIdentity(reportJob));
    q.AddTrigger(opts => opts
        .WithIdentity("ReportTrigger")
        .ForJob(reportJob)
        .StartNow()
        .WithSimpleSchedule(x => x.WithInterval(TimeSpan.FromMinutes(1)).RepeatForever()));
});

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

// Register JobExecutionTracker with scheduler
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

app.Run();

// Helper to compute next occurrence in the future
static DateTimeOffset CalculateForward(DateTime configuredStartTime, TimeSpan interval)
{
    DateTime startTime = configuredStartTime;
    DateTime now = DateTime.Now;
    while (startTime < now)
    {
        startTime = startTime.Add(interval);
    }
    return new DateTimeOffset(startTime);
}
