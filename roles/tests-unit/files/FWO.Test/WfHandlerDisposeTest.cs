using FWO.Config.Api;
using FWO.Services.Workflow;
using NUnit.Framework;
using System.Reflection;

namespace FWO.Test
{
    [TestFixture]
    internal class WfHandlerDisposeTest
    {
        private static readonly FieldInfo ConfigDisposedField = typeof(Config.Api.Config).GetField("_isDisposed", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new MissingFieldException(typeof(Config.Api.Config).FullName, "_isDisposed");

        [Test]
        public void Dispose_DisposesTheUserConfigCreatedByTheDefaultConstructor()
        {
            WfHandler wfHandler = new();
            UserConfig ownedUserConfig = wfHandler.userConfig;

            wfHandler.Dispose();

            Assert.That(ConfigDisposedField.GetValue(ownedUserConfig), Is.True);
        }

        [Test]
        public void Dispose_KeepsAUserConfigThatWasPassedIn()
        {
            using SimulatedUserConfig userConfig = new();
            WfHandler wfHandler = new(userConfig, new SimulatedApiConnection(), Data.Workflow.WorkflowPhases.request, null);

            wfHandler.Dispose();

            Assert.That(ConfigDisposedField.GetValue(userConfig), Is.False);
        }

        [Test]
        public void Dispose_WhenCalledTwice_DoesNotThrow()
        {
            WfHandler wfHandler = new();

            wfHandler.Dispose();

            Assert.DoesNotThrow(wfHandler.Dispose);
        }
    }
}
