using Bunit;
using FWO.Config.Api;
using FWO.Ui.Services;
using FWO.Ui.Shared;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Tests of the footer offset: the messages at the bottom end left of the AI chat panel only while it is open.
    /// </summary>
    [TestFixture]
    internal class UiAiChatFooterOffsetTest
    {
        private const string kFooter = "div.footer";
        private const string kMessage = "UI message";
        private const int kResizedPanelWidth = 600;

        private BunitContext context = null!;
        private AiChatUserConfig userConfig = null!;
        private AiChatTestSession chat = null!;

        private AiChatSession Session => chat.Session;

        [SetUp]
        public void SetUp()
        {
            context = new BunitContext();
            userConfig = new AiChatUserConfig();
            chat = new AiChatTestSession(ScriptedChatClient.Returning(ScriptedChatClient.Text("answer")));
            context.Services.AddSingleton<UserConfig>(userConfig);
            context.Services.AddSingleton(Session);
        }

        [TearDown]
        public void TearDown()
        {
            context.Dispose();
            chat.Dispose();
        }

        [Test]
        public void Footer_WhileThePanelIsMinimized_HasNoOffset()
        {
            IRenderedComponent<AiChatFooterOffset> footer = RenderFooter();

            Assert.Multiple(() =>
            {
                Assert.That(footer.Find(kFooter).HasAttribute("style"), Is.False);
                Assert.That(footer.Find(kFooter).TextContent, Does.Contain(kMessage));
            });
        }

        [Test]
        public void Footer_EndsLeftOfTheOpenPanelAndFollowsItsWidth()
        {
            IRenderedComponent<AiChatFooterOffset> footer = RenderFooter();

            string openStyle = $"right: {AiChatSession.kDefaultPanelWidth + AiChatFooterOffset.kSizerWidth}px; width: auto;";
            string resizedStyle = $"right: {kResizedPanelWidth + AiChatFooterOffset.kSizerWidth}px; width: auto;";

            Session.Open();
            footer.WaitForState(() => GetFooterStyle(footer) == openStyle);
            Assert.That(GetFooterStyle(footer), Is.EqualTo(openStyle));

            Session.PanelWidth = kResizedPanelWidth;
            footer.WaitForState(() => GetFooterStyle(footer) == resizedStyle);
            Assert.That(GetFooterStyle(footer), Is.EqualTo(resizedStyle));

            Session.Minimize();
            footer.WaitForState(() => GetFooterStyle(footer) == null);
            Assert.That(footer.Find(kFooter).HasAttribute("style"), Is.False);
        }

        [Test]
        public void Footer_WhenTheChatIsNotOffered_HasNoOffset()
        {
            userConfig.AiChatEnabled = false;
            Session.Open();

            IRenderedComponent<AiChatFooterOffset> footer = RenderFooter();

            Assert.That(footer.Find(kFooter).HasAttribute("style"), Is.False);
        }

        [Test]
        public void Dispose_StopsFollowingTheSession()
        {
            IRenderedComponent<AiChatFooterOffset> footer = RenderFooter();

            footer.Instance.Dispose();

            Assert.DoesNotThrow(Session.Open);
        }

        /// <summary>
        /// Returns the style of the footer; null if it has none.
        /// </summary>
        private static string? GetFooterStyle(IRenderedComponent<AiChatFooterOffset> footer)
        {
            return footer.Find(kFooter).GetAttribute("style");
        }

        private IRenderedComponent<AiChatFooterOffset> RenderFooter()
        {
            return context.Render<AiChatFooterOffset>(parameters => parameters.AddChildContent($"<div>{kMessage}</div>"));
        }
    }
}
