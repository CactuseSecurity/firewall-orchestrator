using Bunit;
using FWO.Config.Api;
using FWO.Config.Api.Data;
using FWO.Data.Modelling;
using FWO.Ui.Pages.Settings;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Tests the area list of the modelling settings, which can hide the source/destination usage flags.
    /// </summary>
    [TestFixture]
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    internal class UiCommonAreaSelectionTest : BunitContext
    {
        private const string kUseInSrcSelector = "input#cbx_use_in_src";
        private const string kUseInDstSelector = "input#cbx_use_in_dst";

        [SetUp]
        public void SetUpServices()
        {
            Services.AddSingleton<UserConfig>(new SimulatedUserConfig());
        }

        [Test]
        public void Render_ShowsUsageFlagsByDefault()
        {
            IRenderedComponent<CommonAreaSelection> rendered = Render<CommonAreaSelection>(parameters => parameters
                .Add(p => p.CommonAreas, BuildAreas()));

            Assert.Multiple(() =>
            {
                Assert.That(rendered.FindAll(kUseInSrcSelector), Has.Count.EqualTo(1));
                Assert.That(rendered.FindAll(kUseInDstSelector), Has.Count.EqualTo(1));
            });
        }

        [Test]
        public void Render_HidesUsageFlagsButKeepsAreaWhenDisabled()
        {
            IRenderedComponent<CommonAreaSelection> rendered = Render<CommonAreaSelection>(parameters => parameters
                .Add(p => p.CommonAreas, BuildAreas())
                .Add(p => p.ShowUsageFlags, false));

            Assert.Multiple(() =>
            {
                Assert.That(rendered.FindAll(kUseInSrcSelector), Is.Empty);
                Assert.That(rendered.FindAll(kUseInDstSelector), Is.Empty);
                Assert.That(rendered.FindAll("li"), Has.Count.EqualTo(1));
                Assert.That(rendered.Markup, Does.Contain("AreaExcluded"));
            });
        }

        [Test]
        public void ClickAdd_TriggersAreaSearch()
        {
            int searchCalls = 0;
            IRenderedComponent<CommonAreaSelection> rendered = Render<CommonAreaSelection>(parameters => parameters
                .Add(p => p.ShowUsageFlags, false)
                .Add(p => p.SearchCommonArea, () =>
                {
                    searchCalls++;
                    return true;
                }));

            rendered.Find("button").Click();

            Assert.That(searchCalls, Is.EqualTo(1));
        }

        private static List<CommonArea> BuildAreas()
        {
            return [new CommonArea { Area = new ModellingNwGroupWrapper { Content = new ModellingNwGroup { Id = 5, Name = "AreaExcluded", IdString = "NA5" } } }];
        }
    }
}
