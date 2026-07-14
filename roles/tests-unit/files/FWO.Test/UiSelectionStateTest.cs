using Bunit;
using FWO.Config.Api;
using FWO.Data.Report;
using FWO.Ui.Shared;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    internal class UiSelectionStateTest : BunitContext
    {
        [SetUp]
        public void SetUpContext()
        {
            JSInterop.Mode = JSRuntimeMode.Loose;
            Services.AddSingleton<UserConfig>(new SimulatedUserConfig());
            Services.AddLocalization();
            SimulatedUserConfig.DummyTranslate.TryAdd("select_all", "Select All");
            SimulatedUserConfig.DummyTranslate.TryAdd("clear_all", "Clear All");
            SimulatedUserConfig.DummyTranslate.TryAdd("collapse_all", "Collapse All");
            SimulatedUserConfig.DummyTranslate.TryAdd("expand_all", "Expand All");
            SimulatedUserConfig.DummyTranslate.TryAdd("select_device", "Select device");
            SimulatedUserConfig.DummyTranslate.TryAdd("select_management", "Select management");
        }

        [Test]
        public void ManagementSelection_RecomputesButtonStateFromSelectedFilter()
        {
            DeviceFilter deviceFilter = CreateFullySelectedFilter();

            IRenderedComponent<ManagementSelection> cut = Render<ManagementSelection>(parameters => parameters
                .Add(p => p.DeviceFilter, deviceFilter)
                .Add(p => p.SelectAll, true)
                .Add(p => p.ShowTitle, false)
                .Add(p => p.UseLightText, false));

            Assert.That(cut.Markup, Does.Contain("Clear All"));
            Assert.That(cut.Markup, Does.Not.Contain("Select All"));
        }

        [Test]
        public void DeviceSelection_RecomputesButtonStateFromSelectedFilter()
        {
            DeviceFilter deviceFilter = CreateFullySelectedFilter();

            IRenderedComponent<DeviceSelection> cut = Render<DeviceSelection>(parameters => parameters
                .Add(p => p.DeviceFilter, deviceFilter)
                .Add(p => p.SelectAll, true)
                .Add(p => p.CollapseAll, false)
                .Add(p => p.ShowTitle, false));

            Assert.That(cut.Markup, Does.Contain("Clear All"));
            Assert.That(cut.Markup, Does.Not.Contain("Select All"));
        }

        private static DeviceFilter CreateFullySelectedFilter()
        {
            return new DeviceFilter
            {
                Managements =
                [
                    new ManagementSelect
                    {
                        Id = 1,
                        Name = "Management A",
                        Visible = true,
                        Selected = true,
                        Devices =
                        [
                            new DeviceSelect { Id = 11, Name = "Device A1", Visible = true, Selected = true },
                            new DeviceSelect { Id = 12, Name = "Device A2", Visible = true, Selected = true }
                        ]
                    },
                    new ManagementSelect
                    {
                        Id = 2,
                        Name = "Management B",
                        Visible = true,
                        Selected = true,
                        Devices =
                        [
                            new DeviceSelect { Id = 21, Name = "Device B1", Visible = true, Selected = true }
                        ]
                    }
                ]
            };
        }
    }
}
