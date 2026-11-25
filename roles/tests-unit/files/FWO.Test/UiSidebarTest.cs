using Bunit;
using FWO.Ui.Services;
using FWO.Ui.Shared;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Reflection;

namespace FWO.Test
{
    [TestFixture]
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    internal class UiSidebarTest
    {
        private const string kContent = "sidebar content";
        private const int kNavbarHeight = 56;

        private static readonly FieldInfo NavbarHeightSubscribersField = typeof(DomEventService).GetField("_navbarHeightSubscribers", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(typeof(DomEventService).FullName, "_navbarHeightSubscribers");

        [Test]
        public void Sidebar_Dispose_UnsubscribesNavbarHeightHandler_WhenInitializeFailed()
        {
            using BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Strict;

            DomEventService eventService = new();
            context.Services.AddSingleton(eventService);
            context.Services.AddScoped(_ => context.JSInterop.JSRuntime);

            IRenderedComponent<Sidebar> cut = context.Render<Sidebar>(parameters => parameters
                .Add(component => component.Width, 240)
                .AddChildContent("sidebar"));

            cut.WaitForAssertion(() =>
            {
                Assert.That(eventService.Initialized, Is.False);
                Assert.That(GetNavbarHeightSubscriberCount(eventService), Is.EqualTo(1));
            });

            cut.Instance.Dispose();

            Assert.That(GetNavbarHeightSubscriberCount(eventService), Is.EqualTo(0));
        }

        [Test]
        public async Task Sidebar_ContentShown_IsInvokedOnceWhenTheNavbarHeightIsKnown()
        {
            using BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            DomEventService eventService = new();
            context.Services.AddSingleton(eventService);
            int shownCount = 0;

            IRenderedComponent<Sidebar> cut = context.Render<Sidebar>(parameters => parameters
                .Add(component => component.Width, 240)
                .Add(component => component.ContentShown, () => shownCount++)
                .AddChildContent(kContent));

            Assert.Multiple(() =>
            {
                Assert.That(cut.Markup, Does.Not.Contain(kContent));
                Assert.That(shownCount, Is.EqualTo(0));
            });

            await cut.InvokeAsync(() => eventService.InvokeNavbarHeightChanged(kNavbarHeight));
            await cut.InvokeAsync(() => eventService.InvokeNavbarHeightChanged(kNavbarHeight + 1));

            cut.WaitForAssertion(() =>
            {
                Assert.That(cut.Markup, Does.Contain(kContent));
                Assert.That(shownCount, Is.EqualTo(1));
            });
        }

        private static int GetNavbarHeightSubscriberCount(DomEventService eventService)
        {
            MulticastDelegate? subscribers = NavbarHeightSubscribersField.GetValue(eventService) as MulticastDelegate;
            return subscribers?.GetInvocationList().Length ?? 0;
        }
    }
}
