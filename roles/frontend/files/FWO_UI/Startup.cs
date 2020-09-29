using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using Blazored.SessionStorage;
using FWO.Api;
using FWO.UI.Auth;
using FWO.Auth.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FWO
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

            // Todo: Get ApiUri + AuthModuleUri from config file

            /*
            for local API testing (in visual studio without running full ansible installer), either 
            - create a local ssh tunneling to the http server on the virtual machine on an arbitrary port (here 8443) to connect to api like this:
            const string APIPort = "9443";
            - or use the demo system as api host like this: 
            const string APIHost = "demo.itsecorg.de";
            */

            string ApiUri = "https://localhost:9443/api/v1/graphql"; // todo: read from config
#if DEBUG
            //ApiUri = "https://demo.itsecorg.de:443/api/v1/graphql";
#endif
            string AuthUri = "http://localhost:8888/"; // todo: read from config

            services.AddScoped<APIConnection>(api => new APIConnection(ApiUri));
            services.AddScoped<AuthClient>(auth => new AuthClient(AuthUri));
            services.AddBlazoredSessionStorage();
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
