using System;
using System.Net;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using FWO.ApiClient;
using FWO.Ui.Auth;
using FWO.Auth.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using FWO.Config;
using FWO.ApiConfig;
using FWO.Logging;
using FWO.Ui.Services;

namespace FWO.Ui
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddRazorPages();
            services.AddServerSideBlazor();

            services.AddScoped<AuthenticationStateProvider, AuthStateProvider>();

            ConfigConnection configConnection = new ConfigConnection();

            string ApiUri = configConnection.ApiServerUri;
            string AuthUri = configConnection.AuthServerUri;
            string ProductVersion = configConnection.ProductVersion;

            services.AddScoped<APIConnection>(_ => new APIConnection(ApiUri));
            services.AddScoped<AuthClient>(_ => new AuthClient(AuthUri));
            // use anonymous login

            AuthClient authClient = new AuthClient(AuthUri);
            APIConnection apiConn = new APIConnection(ApiUri);
            AuthServerResponse authResponse = authClient.AuthenticateUser("","").Result;
            if (authResponse.Status == HttpStatusCode.BadRequest) 
            {
                Log.WriteError("Auth Server Connection", $"Error while authenticating as anonymous user from UI.");
                Environment.Exit(1);
            }
            string jwt = authResponse.GetResult<string>("jwt");
            apiConn.SetAuthHeader(jwt);
            //((AuthStateProvider)AuthService).AuthenticateUser(jwt);
            
            // get all non-confidential configuration settings and add to a global service (for all users)
            ConfigCollection configCollection = new ConfigCollection(jwt);
            services.AddSingleton<ConfigCollection>(_ => configCollection);  
            
            services.AddScoped<UserConfigCollection>(_ => new UserConfigCollection(configCollection));

            services.AddScoped<DownloadManagerService>(_ => new DownloadManagerService());
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
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

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapBlazorHub();
                endpoints.MapFallbackToPage("/_Host");
            });
        }
    }
}
