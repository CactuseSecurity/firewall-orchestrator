using System.Threading.Channels;
using Bunit;
using FWO.Config.Api;
using FWO.Ui.Services;
using FWO.Ui.Shared;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Tests of the navigation bar button of the AI chat: opening and minimizing the panel, and the badges of a running
    /// answer, a new answer and tool calls waiting for approval.
    /// </summary>
    [TestFixture]
    internal class UiAiChatNavButtonTest
    {
        private const string kButton = "button.ai-chat-toggle";
        private const string kBadge = ".ai-chat-badge";
        private const string kRunningBadge = ".ai-chat-badge-running";
        private const string kAnswerBadge = ".ai-chat-badge-answer";
        private const string kApprovalBadge = ".ai-chat-badge-approval";
        private const string kQuestion = "Which rules allow ssh?";
        private const string kAnswer = "Rule 7.";

        private BunitContext context = null!;
        private AiChatTestSession chat = null!;

        private AiChatSession Session => chat.Session;

        [SetUp]
        public void SetUp()
        {
            context = new BunitContext();
            chat = new AiChatTestSession(ScriptedChatClient.Returning(ScriptedChatClient.Text(kAnswer)));
            context.Services.AddSingleton<UserConfig>(new AiChatUserConfig());
            context.Services.AddSingleton(Session);
        }

        [TearDown]
        public void TearDown()
        {
            context.Dispose();
            chat.Dispose();
        }

        [Test]
        public void Click_OpensAndMinimizesThePanel()
        {
            IRenderedComponent<AiChatNavButton> button = context.Render<AiChatNavButton>();
            Assert.Multiple(() =>
            {
                Assert.That(button.Find(kButton).TextContent, Does.Contain(AiChatDisplay.kChatTextKey));
                Assert.That(button.FindAll(kBadge), Is.Empty);
            });

            button.Find(kButton).Click();
            Assert.Multiple(() =>
            {
                Assert.That(Session.IsOpen, Is.True);
                Assert.That(button.Find(kButton).ClassList, Does.Contain("active"));
                Assert.That(button.Find(kButton).GetAttribute("aria-pressed"), Is.EqualTo("true"));
            });

            button.Find(kButton).Click();
            Assert.Multiple(() =>
            {
                Assert.That(Session.IsOpen, Is.False);
                Assert.That(button.Find(kButton).ClassList, Does.Not.Contain("active"));
            });
        }

        [Test]
        public async Task Badge_ShowsARunningAnswerAndThenTheNewAnswer()
        {
            Channel<ChatResponseUpdate> updates = Channel.CreateUnbounded<ChatResponseUpdate>();
            chat.ChatClient = ScriptedChatClient.Streaming(updates);
            IRenderedComponent<AiChatNavButton> button = context.Render<AiChatNavButton>();

            Task run = Session.SendAsync(kQuestion);
            button.WaitForAssertion(() => Assert.That(button.FindAll(kRunningBadge), Has.Count.EqualTo(1)));
            updates.Writer.TryWrite(ScriptedChatClient.Text(kAnswer));
            updates.Writer.TryComplete();
            await run;

            button.WaitForAssertion(() =>
            {
                Assert.That(button.FindAll(kAnswerBadge), Has.Count.EqualTo(1));
                Assert.That(button.FindAll(kRunningBadge), Is.Empty);
                Assert.That(button.Find(kAnswerBadge).GetAttribute("title"), Is.EqualTo(AiChatDisplay.kNewAnswerTextKey));
            });

            button.Find(kButton).Click();
            Assert.That(button.FindAll(kBadge), Is.Empty);
        }

        [Test]
        public async Task Badge_ShowsToolCallsWaitingForApproval()
        {
            chat.ChatClient = ScriptedChatClient.Returning(AiChatTestUpdates.ApprovalRequest("request-1", "store_value"),
                AiChatTestUpdates.Snapshot(AiChatTestUpdates.Conversation(kQuestion)));
            IRenderedComponent<AiChatNavButton> button = context.Render<AiChatNavButton>();

            await Session.SendAsync(kQuestion);

            button.WaitForAssertion(() =>
            {
                Assert.That(button.FindAll(kApprovalBadge), Has.Count.EqualTo(1));
                Assert.That(button.Find(kApprovalBadge).GetAttribute("title"), Is.EqualTo(AiChatDisplay.kApprovalPendingTextKey));
            });
        }

        [Test]
        public void Dispose_StopsFollowingTheSession()
        {
            IRenderedComponent<AiChatNavButton> button = context.Render<AiChatNavButton>();

            button.Instance.Dispose();

            Assert.DoesNotThrow(Session.Open);
        }
    }
}
