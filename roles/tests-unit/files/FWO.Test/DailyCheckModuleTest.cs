using System.Text.Json;
using FWO.Basics;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class DailyCheckModuleTest
    {
        [Test]
        public void AllModulesNumListCanBeDeserialized()
        {
            List<DailyCheckModule>? modules = JsonSerializer.Deserialize<List<DailyCheckModule>>(DailyCheckModuleGroups.AllModulesNumList());

            ClassicAssert.IsNotNull(modules);
            ClassicAssert.AreEqual(Enum.GetValues<DailyCheckModule>().Length, modules!.Count);
            CollectionAssert.AreEqual(Enum.GetValues<DailyCheckModule>(), modules);
        }
    }
}
