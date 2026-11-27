using AngleSharp.Dom;
using Bunit;
using FWO.Config.Api;
using FWO.Ui.Shared;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    internal class UiInProgressTest
    {
        /// <summary>
        /// The spinner rotates, and the corners of its rotated box reach below the spinner. Its row needs
        /// a bottom margin, otherwise the body of the extra small popup gets a vertical scrollbar.
        /// </summary>
        [Test]
        public async Task Render_LeavesRoomBelowTheRotatingSpinner()
        {
            await using BunitContext context = CreateContext();

            IRenderedComponent<InProgress> inProgress = context.Render<InProgress>(parameters => parameters
                .Add(p => p.Display, true));
            IElement spinnerRow = inProgress.Find(".modal-body > .row");

            Assert.Multiple(() =>
            {
                Assert.That(spinnerRow.QuerySelector(".spinner-border"), Is.Not.Null);
                Assert.That(spinnerRow.ClassList, Does.Contain("my-3"));
            });
        }

        /// <summary>
        /// Without Display the popup is not rendered at all.
        /// </summary>
        [Test]
        public async Task Render_ShowsNothingWithoutDisplay()
        {
            await using BunitContext context = CreateContext();

            IRenderedComponent<InProgress> inProgress = context.Render<InProgress>(parameters => parameters
                .Add(p => p.Display, false));

            Assert.That(inProgress.FindAll(".spinner-border"), Is.Empty);
        }

        private static BunitContext CreateContext()
        {
            BunitContext context = new();
            context.Services.AddSingleton<UserConfig>(new SimulatedUserConfig());
            return context;
        }
    }
}
