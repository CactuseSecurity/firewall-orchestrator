using Bunit;
using FWO.Api.Client;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Ui.Shared;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class UiNavigationMenuTest
    {
        [Test]
        public void NavigationMenu_IgnoresDisposedServicesDuringInitialization()
        {
            using BunitContext context = new();
            UserConfig userConfig = new();
            userConfig.User.Roles = [Roles.Requester];
            userConfig.Dispose();

            context.Services.AddSingleton<GlobalConfig>(new SimulatedGlobalConfig());
            context.Services.AddSingleton<UserConfig>(userConfig);
            context.Services.AddSingleton<ApiConnection>(new DisposedNavigationApiConnection());

            Assert.DoesNotThrow(() => context.Render<NavigationMenu>());
        }

        private sealed class DisposedNavigationApiConnection : SimulatedApiConnection
        {
            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                throw new ObjectDisposedException(nameof(DisposedNavigationApiConnection));
            }
        }
    }
}
