using Bunit;
using Bunit.TestDoubles;
using FWO.Ui.Shared;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class UiSectionNavLinkTest
    {
        [TestCase("/request/reviews/42", "/request", true)]
        [TestCase("/compliance/checks", "/compliance", true)]
        [TestCase("/requesting", "/request", false)]
        [TestCase("/report/generation?device=1", "/report", true)]
        [TestCase("/report/generation#details", "/report", true)]
        public void SectionNavLink_HighlightsOnlyMatchingSection(string currentPath, string activePath, bool expectedActive)
        {
            using BunitContext context = new();
            BunitNavigationManager navigationManager = context.Services.GetRequiredService<BunitNavigationManager>();
            navigationManager.NavigateTo(currentPath);

            IRenderedComponent<SectionNavLink> link = context.Render<SectionNavLink>(parameters => parameters
                .Add(component => component.ActivePath, activePath)
                .AddUnmatched("href", "/destination")
                .AddChildContent("Section"));

            Assert.That(link.Find("a").ClassList.Contains("active"), Is.EqualTo(expectedActive));
        }

        [Test]
        public void SectionNavLink_DoesNotHighlightExcludedSubsection()
        {
            using BunitContext context = new();
            BunitNavigationManager navigationManager = context.Services.GetRequiredService<BunitNavigationManager>();
            navigationManager.NavigateTo("/settings/user");

            IRenderedComponent<SectionNavLink> link = context.Render<SectionNavLink>(parameters => parameters
                .Add(component => component.ActivePath, "/settings")
                .Add(component => component.ExcludedPath, "/settings/user")
                .AddUnmatched("href", "/settings")
                .AddChildContent("Settings"));

            Assert.That(link.Find("a").ClassList.Contains("active"), Is.False);
        }

        [Test]
        public void SectionNavLink_HighlightsNonExcludedSubsection()
        {
            using BunitContext context = new();
            BunitNavigationManager navigationManager = context.Services.GetRequiredService<BunitNavigationManager>();
            navigationManager.NavigateTo("/settings/users");

            IRenderedComponent<SectionNavLink> link = context.Render<SectionNavLink>(parameters => parameters
                .Add(component => component.ActivePath, "/settings")
                .Add(component => component.ExcludedPath, "/settings/user")
                .AddUnmatched("href", "/settings")
                .AddChildContent("Settings"));

            Assert.That(link.Find("a").ClassList.Contains("active"), Is.True);
        }
    }
}
