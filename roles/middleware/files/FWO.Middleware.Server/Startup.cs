using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FWO.Middleware.Server;
using FWO.ApiClient.Queries;
using FWO.Logging;
using FWO.Config.File;
using FWO.ApiClient;

namespace FWO.Middleware
{
    public class Startup
    {
        private readonly ConfigFile config;

        private ApiSubscription<List<Ldap>> connectedLdapsSubscription;
        private List<Ldap> connectedLdaps;

        private readonly object changesLock = new object(); // LOCK

        private readonly RsaSecurityKey privateJWTKey;
        private readonly int jwtMinutesValid = 240;  // TODO: MOVE TO API/Config    

        private readonly string apiUri;

        private ReportScheduler reportScheduler;

        public Startup(IConfiguration configuration, ConfigFile config)
        {
            Configuration = configuration;
            this.config = config;
            apiUri = config.ApiServerUri;
            privateJWTKey = config.JwtPrivateKey;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers()
            .AddJsonOptions(jsonOptions =>
            {
                //jsonOptions.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
                jsonOptions.JsonSerializerOptions.PropertyNamingPolicy = null;
            });

            // Create Token Generator
            JwtWriter jwtWriter = new JwtWriter(privateJWTKey, jwtMinutesValid);

            // Create JWT for middleware-server API calls (relevant part is the role middleware-server) and add it to the Api connection header. 
            APIConnection apiConnection = new APIConnection(apiUri, jwtWriter.CreateJWTMiddlewareServer());

            // Fetch all connectedLdaps via API (blocking).
            connectedLdaps = apiConnection.SendQueryAsync<List<Ldap>>(AuthQueries.getLdapConnections).Result;
            connectedLdapsSubscription = apiConnection.GetSubscription<List<Ldap>>(HandleSubscriptionException, AuthQueries.getLdapConnectionsSubscription);
            connectedLdapsSubscription.OnUpdate += ConnectedLdapsSubscriptionUpdate;
            Log.WriteInfo("Found ldap connection to server", string.Join("\n", connectedLdaps.ConvertAll(ldap => $"{ldap.Address}:{ldap.Port}")));

            // Create and start report scheduler
            Task.Factory.StartNew(() =>
            {
                reportScheduler = new ReportScheduler(apiConnection, jwtWriter, connectedLdapsSubscription);
            }, TaskCreationOptions.LongRunning);

            services.AddSingleton<string>(apiUri);
            services.AddSingleton<JwtWriter>(jwtWriter);
            services.AddSingleton<List<Ldap>>(connectedLdaps);
            services.AddScoped<APIConnection>(_ => new APIConnection(apiUri, jwtWriter.CreateJWTMiddlewareServer()));

            services.AddAuthentication(confOptions =>
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
                    IssuerSigningKey = config.JwtPublicKey
                };
            });
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "FWO.Middleware", Version = "v1" });
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c => { c.SwaggerEndpoint("/swagger/v1/swagger.json", "FWO.Middleware v1"); });
            }

            //app.UseHttpsRedirection();

            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }

        private void HandleSubscriptionException(Exception exception)
        {
            Log.WriteError("Subscription", "Subscription lead to exception.", exception);
        }

        private void ConnectedLdapsSubscriptionUpdate(List<Ldap> ldapsChanges)
        {
            lock (changesLock)
            {
                connectedLdaps = ldapsChanges;
            }
        }
    }
}
