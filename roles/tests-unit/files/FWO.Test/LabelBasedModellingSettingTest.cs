using FWO.Config.Api.Data;
using NUnit.Framework;
using System.Reflection;

namespace FWO.Test
{
    /// <summary>
    /// The global switch for label-based modelling (#5553).
    /// </summary>
    [TestFixture]
    [Parallelizable]
    internal class LabelBasedModellingSettingTest
    {
        private const string kConfigKey = "labelBasedModelling";

        [Test]
        public void Default_IsOff()
        {
            Assert.That(new ConfigData().LabelBasedModelling, Is.False);
        }

        [Test]
        public void Setting_IsGlobalAndStoredUnderItsKey()
        {
            PropertyInfo property = typeof(ConfigData).GetProperty(nameof(ConfigData.LabelBasedModelling))
                ?? throw new AssertionException("missing property");
            ConfigData configData = new() { LabelBasedModelling = true };

            string newtonsoftJson = Newtonsoft.Json.JsonConvert.SerializeObject(configData);
            string systemTextJson = System.Text.Json.JsonSerializer.Serialize(configData);

            Assert.Multiple(() =>
            {
                Assert.That(property.GetCustomAttribute<UserConfigDataAttribute>(), Is.Null);
                Assert.That(newtonsoftJson, Does.Contain($"\"{kConfigKey}\":true"));
                Assert.That(systemTextJson, Does.Contain($"\"{kConfigKey}\":true"));
                Assert.That(Newtonsoft.Json.JsonConvert.DeserializeObject<ConfigData>(newtonsoftJson)?.LabelBasedModelling, Is.True);
            });
        }
    }
}
