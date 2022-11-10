using FWO.Api.Client;
using FWO.Ui.Auth;
using FWO.Middleware.Client;
using Microsoft.AspNetCore.Components.Authorization;
using FWO.Config.File;
using FWO.Config.Api;
using FWO.Logging;
using FWO.Ui.Services;
using BlazorTable;
using RestSharp;

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
            services.AddScoped<CircuitHandlerService, CircuitHandlerService>();

            string ApiUri = ConfigFile.ApiServerUri;
            string MiddlewareUri = ConfigFile.MiddlewareServerUri;
            string ProductVersion = ConfigFile.ProductVersion;

            services.AddScoped<ApiConnection>(_ => new GraphQlApiConnection(ApiUri));
            services.AddScoped<MiddlewareClient>(_ => new MiddlewareClient(MiddlewareUri));
            
            // create "anonymous" (empty) jwt
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

            string jwt = createJWTResponse.Data ?? throw new Exception("Received empty jwt.");
            apiConn.SetAuthHeader(jwt);

            // get all non-confidential configuration settings and add to a global service (for all users)
            GlobalConfig globalConfig = Task.Run(async() => await GlobalConfig.ConstructAsync(jwt)).Result;
            services.AddSingleton<GlobalConfig>(_ => globalConfig);    
            services.AddScoped<UserConfig>(_ => new UserConfig(globalConfig));

            services.AddScoped(_ => new DomEventService());

            services.AddBlazorTable();
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
