using FWO.Data.Modelling;
using FWO.Data.Workflow;
using NUnit.Framework;
using System.Text.Json;

namespace FWO.Test
{
    /// <summary>
    /// Tests that the extra configurations of a modelled connection are saved with its request task as requested and read
    /// back from it, and that a request task without them can be told from one whose connection had none.
    /// </summary>
    [TestFixture]
    [Parallelizable]
    internal class ModellingRequestExtraConfigsTest
    {
        private static readonly List<string> kRequested = ["IDA-User: a_testUser", "NAT: To 1.2.3.4"];
        private static readonly List<string> kRequestedFromInterface = ["Updatable Objects: Exchange Online China Services"];

        private static WfReqTask TaskWith(Dictionary<string, string> addInfo) => new() { AdditionalInfo = JsonSerializer.Serialize(addInfo) };

        [Test]
        public void AdditionalInfo_SavesTheConnectionIdAndItsExtraConfigurations()
        {
            ModellingConnection connection = new()
            {
                Id = 42,
                ExtraConfigs =
                [
                    new ModellingExtraConfig { Id = 7, ExtraConfigType = "IDA-User", ExtraConfigText = "a_testUser" },
                    new ModellingExtraConfig { ExtraConfigType = "NAT", ExtraConfigText = "To 1.2.3.4" }
                ],
                ExtraConfigsFromInterface = [new ModellingExtraConfig { ExtraConfigType = "Updatable Objects", ExtraConfigText = "Exchange Online China Services" }]
            };

            WfReqTask reqTask = TaskWith(ModellingRequestExtraConfigs.AdditionalInfo(connection));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(reqTask.GetAddInfoIntValue(AdditionalInfoKeys.ConnId), Is.EqualTo(42));
                Assert.That(ModellingRequestExtraConfigs.Read(reqTask, AdditionalInfoKeys.ExtraConfigs)?.Select(config => config.Display()), Is.EqualTo(kRequested));
                Assert.That(ModellingRequestExtraConfigs.Read(reqTask, AdditionalInfoKeys.ExtraConfigs)?.Select(config => config.Id), Is.All.EqualTo(0), "only type and text are saved");
                Assert.That(ModellingRequestExtraConfigs.Read(reqTask, AdditionalInfoKeys.InterfaceExtraConfigs)?.Select(config => config.Display()),
                    Is.EqualTo(kRequestedFromInterface));
            }
        }

        [Test]
        public void AdditionalInfo_ConnectionWithoutExtraConfigurations_SavesEmptyLists()
        {
            WfReqTask reqTask = TaskWith(ModellingRequestExtraConfigs.AdditionalInfo(new ModellingConnection { Id = 1 }));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(ModellingRequestExtraConfigs.Read(reqTask, AdditionalInfoKeys.ExtraConfigs), Is.Not.Null.And.Empty);
                Assert.That(ModellingRequestExtraConfigs.Read(reqTask, AdditionalInfoKeys.InterfaceExtraConfigs), Is.Not.Null.And.Empty);
            }
        }

        [Test]
        public void Read_RequestTaskWithoutSavedExtraConfigurations_IsNull()
        {
            WfReqTask reqTask = TaskWith(new Dictionary<string, string> { { AdditionalInfoKeys.ConnId, "1" } });

            Assert.That(ModellingRequestExtraConfigs.Read(reqTask, AdditionalInfoKeys.ExtraConfigs), Is.Null);
        }

        [Test]
        public void Read_ValueThatIsNoList_Throws()
        {
            WfReqTask reqTask = TaskWith(new Dictionary<string, string> { { AdditionalInfoKeys.ExtraConfigs, "{\"no\":\"list\"}" } });

            Assert.That(() => ModellingRequestExtraConfigs.Read(reqTask, AdditionalInfoKeys.ExtraConfigs), Throws.InstanceOf<JsonException>());
        }
    }
}
