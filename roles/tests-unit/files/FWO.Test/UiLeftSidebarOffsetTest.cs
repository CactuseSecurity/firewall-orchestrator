using Bunit;
using FWO.Ui.Services;
using FWO.Ui.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    internal class UiLeftSidebarOffsetTest
    {
        private const string kWarning = "<p id=\"warning\">warning</p>";
        private const int kNavbarHeight = 50;

        [Test]
        public void LeftSidebarState_UsesWidestSidebarAndForgetsRemovedOnes()
        {
            LeftSidebarState state = new();
            object firstSidebar = new();
            object secondSidebar = new();
            int changeCount = 0;
            state.OnChange += () => changeCount++;

            state.SetWidth(firstSidebar, 250);
            state.SetWidth(secondSidebar, 300);
            state.SetWidth(secondSidebar, 300);
            int widthWithBoth = state.Width;
            state.Remove(secondSidebar);
            int widthWithFirst = state.Width;
            state.Remove(firstSidebar);
            state.Remove(firstSidebar);

            Assert.Multiple(() =>
            {
                Assert.That(widthWithBoth, Is.EqualTo(300));
                Assert.That(widthWithFirst, Is.EqualTo(250));
                Assert.That(state.HasSidebar, Is.False);
                Assert.That(state.Width, Is.EqualTo(0));
                Assert.That(changeCount, Is.EqualTo(4));
            });
        }

        [Test]
        public void LeftSidebarOffset_StartsContentAfterLeftSidebar()
        {
            using BunitContext context = new();
            LeftSidebarState state = new();
            state.SetWidth(new object(), 240);

            IRenderedComponent<LeftSidebarOffset> cut = RenderOffset(context, state);

            Assert.That(cut.Find("#warning").ParentElement!.GetAttribute("style"), Does.Contain("margin-left: 260px"));
        }

        [Test]
        public void LeftSidebarOffset_FollowsSidebarChanges()
        {
            using BunitContext context = new();
            LeftSidebarState state = new();
            object sidebar = new();
            IRenderedComponent<LeftSidebarOffset> cut = RenderOffset(context, state);
            Assert.That(cut.Find("#warning").ParentElement?.GetAttribute("style"), Is.Null);

            state.SetWidth(sidebar, 100);
            cut.WaitForAssertion(() => Assert.That(cut.Find("#warning").ParentElement!.GetAttribute("style"), Does.Contain("margin-left: 120px")));

            state.Remove(sidebar);
            cut.WaitForAssertion(() => Assert.That(cut.Find("#warning").ParentElement?.GetAttribute("style"), Is.Null));
        }

        [Test]
        public void LeftSidebarOffset_KeepsContentComponentsWhenSidebarComesAndGoes()
        {
            using BunitContext context = new();
            LeftSidebarState state = new();
            object sidebar = new();
            IRenderedComponent<LeftSidebarOffset> cut = context.Render<LeftSidebarOffset>(parameters => parameters
                .Add(component => component.State, state)
                .AddChildContent<InitializationCounter>());
            InitializationCounter content = cut.FindComponent<InitializationCounter>().Instance;

            state.SetWidth(sidebar, 100);
            cut.WaitForAssertion(() => Assert.That(cut.Find("div").GetAttribute("style"), Does.Contain("margin-left: 120px")));
            state.Remove(sidebar);
            cut.WaitForAssertion(() => Assert.That(cut.Find("div").GetAttribute("style"), Is.Null));
            InitializationCounter contentAfterChanges = cut.FindComponent<InitializationCounter>().Instance;

            Assert.Multiple(() =>
            {
                Assert.That(contentAfterChanges, Is.SameAs(content));
                Assert.That(content.Initializations, Is.EqualTo(1));
                Assert.That(content.Disposed, Is.False);
            });
        }

        [Test]
        public async Task Sidebar_ReportsCollapsedAndRestoredWidth()
        {
            using BunitContext context = CreateSidebarContext();
            LeftSidebarState state = new();
            IRenderedComponent<Sidebar> cut = RenderSidebar(context, state, positionLeft: true);

            await cut.InvokeAsync(() => cut.Instance.ToggleCollapse(true));
            bool shownWhenCollapsed = state.HasSidebar;
            int collapsedWidth = state.Width;
            await cut.InvokeAsync(() => cut.Instance.ToggleCollapse(false));

            Assert.Multiple(() =>
            {
                Assert.That(shownWhenCollapsed, Is.True);
                Assert.That(collapsedWidth, Is.EqualTo(0));
                Assert.That(state.Width, Is.EqualTo(240));
            });
        }

        [Test]
        public void Sidebar_ReportsResizedWidth()
        {
            using BunitContext context = CreateSidebarContext();
            // the sizer is only rendered once the navbar height is known
            context.Services.GetRequiredService<DomEventService>().InvokeNavbarHeightChanged(kNavbarHeight);
            LeftSidebarState state = new();
            IRenderedComponent<Sidebar> cut = RenderSidebar(context, state, positionLeft: true);

            cut.WaitForElement(".sidebar-sizer").DragStart(new DragEventArgs { ScreenX = 100, DataTransfer = new DataTransfer() });
            cut.Find(".sidebar-sizer").DragEnd(new DragEventArgs { ScreenX = 160, DataTransfer = new DataTransfer() });

            Assert.That(state.Width, Is.EqualTo(300));
        }

        [Test]
        public void Sidebar_ReportsLeftWidthUntilDisposed()
        {
            using BunitContext context = CreateSidebarContext();
            LeftSidebarState state = new();

            IRenderedComponent<Sidebar> cut = RenderSidebar(context, state, positionLeft: true);
            int reportedWidth = state.Width;
            cut.Instance.Dispose();

            Assert.Multiple(() =>
            {
                Assert.That(reportedWidth, Is.EqualTo(240));
                Assert.That(state.HasSidebar, Is.False);
            });
        }

        [Test]
        public void Sidebar_DoesNotReportRightSidebar()
        {
            using BunitContext context = CreateSidebarContext();
            LeftSidebarState state = new();

            RenderSidebar(context, state, positionLeft: false);

            Assert.That(state.HasSidebar, Is.False);
        }

        private static IRenderedComponent<LeftSidebarOffset> RenderOffset(BunitContext context, LeftSidebarState state)
        {
            return context.Render<LeftSidebarOffset>(parameters => parameters
                .Add(component => component.State, state)
                .AddChildContent(kWarning));
        }

        private static BunitContext CreateSidebarContext()
        {
            BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddSingleton(new DomEventService());
            context.Services.AddScoped(_ => context.JSInterop.JSRuntime);
            return context;
        }

        private static IRenderedComponent<Sidebar> RenderSidebar(BunitContext context, LeftSidebarState state, bool positionLeft)
        {
            IRenderedComponent<CascadingValue<LeftSidebarState>> wrapper = context.Render<CascadingValue<LeftSidebarState>>(parameters => parameters
                .Add(component => component.Value, state)
                .Add(component => component.IsFixed, true)
                .Add(component => component.ChildContent, (RenderFragment)(builder =>
                {
                    builder.OpenComponent<Sidebar>(0);
                    builder.AddAttribute(1, nameof(Sidebar.Width), 240);
                    builder.AddAttribute(2, nameof(Sidebar.PositionLeft), positionLeft);
                    builder.CloseComponent();
                })));
            return wrapper.FindComponent<Sidebar>();
        }

        /// <summary>
        /// Content component that tells whether it was created more than once or disposed.
        /// </summary>
        private sealed class InitializationCounter : ComponentBase, IDisposable
        {
            public int Initializations { get; private set; }

            public bool Disposed { get; private set; }

            /// <summary>
            /// Counts the initializations of this instance.
            /// </summary>
            protected override void OnInitialized()
            {
                Initializations++;
            }

            /// <summary>
            /// Records that the instance was disposed.
            /// </summary>
            public void Dispose()
            {
                Disposed = true;
            }
        }
    }
}
