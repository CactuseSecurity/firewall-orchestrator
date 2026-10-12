using Bunit;
using FWO.Ui.Services;
using FWO.Ui.Shared;
using Microsoft.AspNetCore.Components;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    internal class UiPopUpComponentTest
    {
        private const string kCenterClass = "custom-modal-center";

        private static readonly PopupSize[] kCenteredSizes =
        [
            PopupSize.Auto,
            PopupSize.XLarge,
            PopupSize.Large,
            PopupSize.Medium,
            PopupSize.Small,
            PopupSize.XSmall
        ];

        /// <summary>
        /// The centering class must be set on every popup that is not full screen, because it is the
        /// only place where the modal is positioned. Full screen popups fill the viewport on their own.
        /// </summary>
        [Test]
        public async Task SizeClass_CentersEveryNonFullScreenPopup()
        {
            await using BunitContext context = new();

            foreach (PopupSize size in kCenteredSizes)
            {
                string dialogClass = RenderDialogClass(context, size);
                Assert.That(dialogClass.Split(' '), Contains.Item(kCenterClass), $"Popup size {size} is not centered.");
            }

            Assert.That(RenderDialogClass(context, PopupSize.FullScreen).Split(' '), Has.No.Member(kCenterClass));
        }

        /// <summary>
        /// Every size keeps its own class next to the centering class, so the size specific
        /// dimensions are still applied.
        /// </summary>
        [Test]
        public async Task SizeClass_KeepsSizeSpecificClass()
        {
            await using BunitContext context = new();
            Dictionary<PopupSize, string> expectedClasses = new()
            {
                { PopupSize.Auto, "custom-modal-auto" },
                { PopupSize.FullScreen, "custom-modal-fs" },
                { PopupSize.XLarge, "custom-modal-xl" },
                { PopupSize.Large, "custom-modal-lg" },
                { PopupSize.Medium, "custom-modal-md" },
                { PopupSize.Small, "custom-modal-sm" },
                { PopupSize.XSmall, "custom-modal-xs" }
            };

            foreach (KeyValuePair<PopupSize, string> expectedClass in expectedClasses)
            {
                Assert.That(RenderDialogClass(context, expectedClass.Key).Split(' '), Contains.Item(expectedClass.Value));
            }
        }

        /// <summary>
        /// Every size marks its content box with its own class. The auto size needs it as well: without
        /// that class its content grows past the viewport and pushes the footer out of reach.
        /// </summary>
        [Test]
        public async Task SizeClassContent_KeepsSizeSpecificContentClass()
        {
            await using BunitContext context = new();
            Dictionary<PopupSize, string> expectedClasses = new()
            {
                { PopupSize.Auto, "custom-modal-content-auto" },
                { PopupSize.FullScreen, "custom-modal-content-fs" },
                { PopupSize.XLarge, "custom-modal-content-xl" },
                { PopupSize.Large, "custom-modal-content-lg" },
                { PopupSize.Medium, "custom-modal-content-md" },
                { PopupSize.Small, "custom-modal-content-sm" },
                { PopupSize.XSmall, "custom-modal-content-xs" }
            };

            foreach (KeyValuePair<PopupSize, string> expectedClass in expectedClasses)
            {
                Assert.That(RenderContentClass(context, expectedClass.Key).Split(' '), Contains.Item(expectedClass.Value),
                    $"The content box of popup size {expectedClass.Key} lost its size specific class.");
            }
        }

        /// <summary>
        /// The footer is rendered as a sibling of the scrollable content for all sizes but the smallest one.
        /// </summary>
        [Test]
        public async Task Footer_IsSiblingOfContentAndOmittedForXSmall()
        {
            await using BunitContext context = new();

            IRenderedComponent<PopUp> popup = RenderPopUp(context, PopupSize.Small);
            Assert.That(popup.FindAll(".modal-content .custom-modal-footer"), Is.Empty);
            Assert.That(popup.FindAll(".custom-modal-footer"), Has.Count.EqualTo(1));

            popup = RenderPopUp(context, PopupSize.XSmall);
            Assert.That(popup.FindAll(".custom-modal-footer"), Is.Empty);
        }

        private static string RenderDialogClass(BunitContext context, PopupSize size)
        {
            return RenderPopUp(context, size).Find(".modal-open > div").GetAttribute("class") ?? "";
        }

        private static string RenderContentClass(BunitContext context, PopupSize size)
        {
            return RenderPopUp(context, size).Find(".modal-content").GetAttribute("class") ?? "";
        }

        private static IRenderedComponent<PopUp> RenderPopUp(BunitContext context, PopupSize size)
        {
            return context.Render<PopUp>(parameters => parameters
                .Add(p => p.Show, true)
                .Add(p => p.Size, size)
                .Add(p => p.Title, "TestPopup")
                .Add(p => p.Body, (RenderFragment)(builder => builder.AddMarkupContent(0, "<span>body</span>")))
                .Add(p => p.Footer, (RenderFragment)(builder => builder.AddMarkupContent(0, "<span>footer</span>"))));
        }
    }
}
