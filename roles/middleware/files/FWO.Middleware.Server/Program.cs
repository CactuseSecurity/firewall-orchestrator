using FWO.ApiClient;
using FWO.ApiClient.Queries;
using FWO.Config.File;
using FWO.Logging;
using FWO.Middleware.Server;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;


object changesLock = new object(); // LOCK
int jwtMinutesValid = 240;  // TODO: MOVE TO API/Config    

ReportScheduler reportScheduler;
AutoDiscoverScheduler autoDiscoverScheduler;
DailyCheckScheduler dailyCheckScheduler;

// Create new config file
ConfigFile configFile = new ConfigFile();

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls(configFile.MiddlewareServerNativeUri ?? throw new Exception("Missing middleware server url on startup."));

// Create Token Generator
JwtWriter jwtWriter = new JwtWriter(configFile.JwtPrivateKey, jwtMinutesValid);

// Create JWT for middleware-server API calls (relevant part is the role middleware-server) and add it to the Api connection header. 
APIConnection apiConnection = new APIConnection(configFile.ApiServerUri ?? throw new Exception("Missing api server url on startup."), jwtWriter.CreateJWTMiddlewareServer());

// Fetch all connectedLdaps via API (blocking).
List<Ldap> connectedLdaps = new List<Ldap>();
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

Action<Exception> handleSubscriptionException = (Exception exception) => Log.WriteError("Subscription", "Subscription lead to exception.", exception);
ApiSubscription<List<Ldap>>.SubscriptionUpdate connectedLdapsSubscriptionUpdate = (List<Ldap> ldapsChanges) => { lock (changesLock) { connectedLdaps = ldapsChanges; } };
ApiSubscription<List<Ldap>> connectedLdapsSubscription = apiConnection.GetSubscription<List<Ldap>>(handleSubscriptionException, connectedLdapsSubscriptionUpdate, AuthQueries.getLdapConnectionsSubscription);
Log.WriteInfo("Found ldap connection to server", string.Join("\n", connectedLdaps.ConvertAll(ldap => $"{ldap.Address}:{ldap.Port}")));

// Create and start report scheduler
Task.Factory.StartNew(() =>
{
    reportScheduler = new ReportScheduler(apiConnection, jwtWriter, connectedLdapsSubscription);
}, TaskCreationOptions.LongRunning);

// Create and start auto disovery scheduler
Task.Factory.StartNew(async() =>
{
    autoDiscoverScheduler = await AutoDiscoverScheduler.CreateAsync(apiConnection);
}, TaskCreationOptions.LongRunning);

// Create and start daily check scheduler
Task.Factory.StartNew(async() =>
{
    dailyCheckScheduler = await DailyCheckScheduler.CreateAsync(apiConnection);
}, TaskCreationOptions.LongRunning);

// Add services to the container.
builder.Services.AddControllers()
  .AddJsonOptions(jsonOptions =>
  {
        //jsonOptions.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        jsonOptions.JsonSerializerOptions.PropertyNamingPolicy = null;
  });

builder.Services.AddSingleton<string>(configFile.ApiServerUri);
builder.Services.AddSingleton<JwtWriter>(jwtWriter);
builder.Services.AddSingleton<List<Ldap>>(connectedLdaps);
builder.Services.AddScoped<APIConnection>(_ => new APIConnection(configFile.ApiServerUri, jwtWriter.CreateJWTMiddlewareServer()));

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
        ValidateAudience = false,
        ValidateIssuer = false,
        ValidateLifetime = true,
        RoleClaimType = "role",
        IssuerSigningKey = configFile.JwtPublicKey
    };
});
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "FWO.Middleware", Version = "v1" });
});

var app = builder.Build();

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

app.UseEndpoints(endpoints =>
{
    endpoints.MapControllers();
});

app.Run();
