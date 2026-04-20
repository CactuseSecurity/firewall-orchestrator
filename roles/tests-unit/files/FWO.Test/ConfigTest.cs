using System.Reflection;
using FWO.Config.Api;
using FWO.Config.Api.Data;
using FWO.Data.Workflow;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class ConfigTest
    {
        [Test]
        public void Update_KeepsDefaultEnum_WhenConfigContainsUnknownAutoCreateImplTaskOption()
        {
            SimulatedUserConfig userConfig = new();
            userConfig.ReqAutoCreateImplTasks = AutoCreateImplTaskOptions.never;

            InvokeUpdate(userConfig,
            [
                new() { Key = "reqAutoCreateImplTasks", Value = "999", User = 0 }
            ]);

            Assert.That(userConfig.ReqAutoCreateImplTasks, Is.EqualTo(AutoCreateImplTaskOptions.never));
        }

        private static void InvokeUpdate(FWO.Config.Api.Config config, ConfigItem[] configItems)
        {
            MethodInfo updateMethod = typeof(FWO.Config.Api.Config).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new MissingMethodException(typeof(FWO.Config.Api.Config).FullName, "Update");

            updateMethod.Invoke(config, [configItems]);
        }
    }
}
