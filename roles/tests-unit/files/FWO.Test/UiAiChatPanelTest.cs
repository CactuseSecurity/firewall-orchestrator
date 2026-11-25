using AngleSharp.Dom;
using Bunit;
using FWO.Ai.Client;
using FWO.Config.Api;
using FWO.Ui.Services;
using FWO.Ui.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Tests of the AI chat panel: visibility, sending, streaming, cancelling, tool approvals, failures, output encoding,
    /// new chat, minimizing and resizing.
    /// </summary>
    [TestFixture]
    internal class UiAiChatPanelTest
    {
        private const string kThreadId = "thread-1";
        private const string kQuestion = "Which rules allow ssh?";
        private const string kAnswer = "Rule 7 allows ssh.\nRule 9 too.";
        private const string kStoreTool = "store_value";
        private const int kNavbarHeight = 56;
        private const string kPanel = ".ai-chat-content";
        private const string kInput = "#ai-chat-input";
        private const string kSendButton = "button.ai-chat-send";
        private const string kCancelButton = "button.ai-chat-cancel";
        private const string kNewChatButton = "button.ai-chat-new";
        private const string kMinimizeButton = "button.ai-chat-minimize";
        private const string kApproveButton = "button.ai-chat-approve";
        private const string kRejectButton = "button.ai-chat-reject";
        private const string kUserEntry = ".ai-chat-user";
        private const string kAnswerText = ".ai-chat-answer .ai-chat-text";
        private const string kStateMessage = ".ai-chat-state";
        private const string kIntro = ".ai-chat-intro";
        private const string kHelpLink = "a[href^='/help/']";
        private const string kChatHelpUrl = "/help/ai/chat?";
        private const string kScrollFunction = "scrollToEndIfNear";
        private const string kFocusFunction = "Blazor._internal.domWrapper.focus";
        private const string kDisabledAttribute = "disabled";
        private const string kEnterKey = "Enter";

        private static readonly Dictionary<string, object?> kStoreArguments = new() { { "value", "42" } };
        private static readonly List<string> kInjectedAttributes = ["onerror", "onmouseover", "onclick"];
        private static readonly List<string> kInjectedElements = ["img", "script"];
        private static readonly List<string> kApprovalAnswerTextKeys = [AiChatDisplay.kApprovedTextKey, AiChatDisplay.kRejectedTextKey];

        private static readonly List<string> kInjectionPayloads =
        [
            "<img src=x onerror=alert(1)>",
            "\"><script>alert(1)</script>",
            "' onmouseover='x"
        ];

        private BunitContext context = null!;
        private AiChatUserConfig userConfig = null!;
        private AiChatTestSession chat = null!;

        private AiChatSession Session => chat.Session;

        [SetUp]
        public void SetUp()
        {
            context = new BunitContext();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            userConfig = new AiChatUserConfig();
            chat = new AiChatTestSession(CreateAnsweringClient(kAnswer));
            DomEventService eventService = new();
            eventService.InvokeNavbarHeightChanged(kNavbarHeight);
            context.Services.AddSingleton(eventService);
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
        public void Panel_IsNotRenderedWhileMinimized()
        {
            IRenderedComponent<AiChatPanel> panel = context.Render<AiChatPanel>();

            Assert.That(panel.FindAll(kPanel), Is.Empty);
        }

        [Test]
        public void Panel_IsNotRenderedAndMinimizedWhenTheChatIsNotOffered()
        {
            userConfig.AiChatEnabled = false;
            Session.Open();

            IRenderedComponent<AiChatPanel> panel = context.Render<AiChatPanel>();

            Assert.Multiple(() =>
            {
                Assert.That(panel.FindAll(kPanel), Is.Empty);
                Assert.That(Session.IsOpen, Is.False);
            });
        }

        [Test]
        public void Panel_OpensWithTheIntroWhenTheSessionIsOpened()
        {
            IRenderedComponent<AiChatPanel> panel = context.Render<AiChatPanel>();

            Session.Open();

            WaitUntil(panel, () => panel.FindAll(kPanel).Count == 1 && panel.FindAll(kIntro).Count == 1);
            Assert.Multiple(() =>
            {
                Assert.That(panel.Find(kIntro).TextContent, Is.EqualTo(AiChatDisplay.kIntroTextKey));
                Assert.That(panel.Markup, Does.Contain(AiChatDisplay.kDisclaimerTextKey));
                Assert.That(panel.Find(kSendButton).HasAttribute(kDisabledAttribute), Is.True);
            });
        }

        [Test]
        public void Panel_FocusesTheInputOnceTheSidebarShowsIt()
        {
            IRenderedComponent<AiChatPanel> panel = context.Render<AiChatPanel>();

            Session.Open();

            WaitUntil(panel, () => panel.FindAll(kInput).Count == 1 && context.JSInterop.Invocations[kFocusFunction].Count > 0);
            JSRuntimeInvocation focus = context.JSInterop.VerifyFocusAsyncInvoke();
            ElementReference? focused = focus.Arguments[0] as ElementReference?;
            Assert.That(focused?.Id, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void HelpLink_OpensTheChatPageOfTheAiHelp()
        {
            IRenderedComponent<AiChatPanel> panel = RenderOpenPanel();

            string? target = panel.Find(kHelpLink).GetAttribute("href");

            Assert.That(target, Does.StartWith(kChatHelpUrl));
        }

        [Test]
        public void SendButton_SendsTheDraftAndShowsQuestionAndAnswer()
        {
            IRenderedComponent<AiChatPanel> panel = RenderOpenPanel();

            panel.Find(kInput).Input(kQuestion);
            panel.Find(kSendButton).Click();

            WaitUntil(panel, () => panel.FindAll(kAnswerText).Count == 1);
            Assert.Multiple(() =>
            {
                Assert.That(panel.Find(kUserEntry).TextContent, Is.EqualTo(kQuestion));
                Assert.That(panel.Find(kAnswerText).TextContent, Is.EqualTo(kAnswer));
                Assert.That(panel.FindAll(kIntro), Is.Empty);
                Assert.That(Session.Draft, Is.Empty);
                Assert.That(chat.ChatClient.Calls.Single().Messages.Single().Text, Is.EqualTo(kQuestion));
                Assert.That(context.JSInterop.Invocations[kScrollFunction], Is.Not.Empty);
            });
        }

        [Test]
        public void Input_CtrlEnterSendsAndEnterAloneDoesNot()
        {
            IRenderedComponent<AiChatPanel> panel = RenderOpenPanel();
            panel.Find(kInput).Input(kQuestion);

            panel.Find(kInput).KeyDown(new KeyboardEventArgs { Key = kEnterKey });
            Assert.That(chat.ChatClient.Calls, Is.Empty);

            panel.Find(kInput).KeyDown(new KeyboardEventArgs { Key = kEnterKey, CtrlKey = true });

            WaitUntil(panel, () => panel.FindAll(kAnswerText).Count == 1);
            Assert.That(chat.ChatClient.Calls, Has.Count.EqualTo(1));
        }

        [Test]
        public void SendButton_IsDisabledForABlankDraft()
        {
            IRenderedComponent<AiChatPanel> panel = RenderOpenPanel();

            panel.Find(kInput).Input("   ");

            Assert.That(panel.Find(kSendButton).HasAttribute(kDisabledAttribute), Is.True);
        }

        [Test]
        public void CancelButton_IsShownWhileRunningAndCancelsTheRun()
        {
            chat.ChatClient = ScriptedChatClient.WaitingForCancellation();
            IRenderedComponent<AiChatPanel> panel = RenderOpenPanel();
            panel.Find(kInput).Input(kQuestion);

            panel.Find(kSendButton).Click();
            WaitUntil(panel, () => panel.FindAll(kCancelButton).Count == 1 && panel.Markup.Contains(AiChatDisplay.kAnsweringTextKey));
            panel.Find(kCancelButton).Click();

            // the answer is marked cancelled shortly before the run ends, so wait until the send button is back as well
            WaitUntil(panel, () => panel.FindAll(kStateMessage).Count == 1 && panel.FindAll(kSendButton).Count == 1);
            IElement stateMessage = panel.Find(kStateMessage);
            Assert.Multiple(() =>
            {
                Assert.That(stateMessage.TextContent, Is.EqualTo(AiChatDisplay.kCancelledTextKey));
                Assert.That(stateMessage.ClassList, Does.Contain("alert-warning"));
                Assert.That(panel.FindAll(kSendButton), Has.Count.EqualTo(1));
            });
        }

        [Test]
        public void ApprovalButtons_AnswerTheToolCallsAndTheAiGoesOn()
        {
            chat.ChatClient = CreateApprovalClient(kStoreTool);
            IRenderedComponent<AiChatPanel> panel = RenderOpenPanel();
            panel.Find(kInput).Input(kQuestion);
            panel.Find(kSendButton).Click();
            // the approvals are shown shortly before the run ends; answers given before its end would be ignored
            WaitUntil(panel, () => panel.FindAll(kApproveButton).Count == 2 && panel.FindAll(kSendButton).Count == 1 && !Session.IsRunning);
            Assert.Multiple(() =>
            {
                Assert.That(panel.Find(".ai-chat-tool").TextContent, Is.EqualTo(kStoreTool));
                Assert.That(panel.Find(".ai-chat-arguments").TextContent, Does.Contain("\"value\""));
                Assert.That(panel.Markup, Does.Contain(AiChatDisplay.kApprovalRequestTextKey));
                Assert.That(panel.Find(kSendButton).HasAttribute(kDisabledAttribute), Is.True);
            });

            panel.FindAll(kApproveButton)[0].Click();
            WaitUntil(panel, () => panel.FindAll(kRejectButton).Count == 1);
            chat.ChatClient = CreateAnsweringClient(kAnswer);
            panel.Find(kRejectButton).Click();

            // the answer asking for the approvals has no text, so the only answer text is the one of the resumed run
            WaitUntil(panel, () => panel.FindAll(kAnswerText).Count == 1);
            Assert.Multiple(() =>
            {
                Assert.That(panel.FindAll(".ai-chat-approval-answer").Select(badge => badge.TextContent),
                    Is.EqualTo(kApprovalAnswerTextKeys.Select(userConfig.GetText)));
                Assert.That(panel.FindAll(kApproveButton), Is.Empty);
                Assert.That(panel.Find(kAnswerText).TextContent, Is.EqualTo(kAnswer));
                Assert.That(chat.ChatClient.Calls.Single().Messages.Last().Contents.OfType<ToolApprovalResponseContent>().Count(), Is.EqualTo(2));
            });
        }

        [Test]
        public void Failure_ShowsTheMessageOfTheFailure()
        {
            chat.ChatClient = ScriptedChatClient.Throwing(new AgentClientException(AgentClientFailure.Unavailable, "not reachable"));
            IRenderedComponent<AiChatPanel> panel = RenderOpenPanel();
            panel.Find(kInput).Input(kQuestion);

            panel.Find(kSendButton).Click();

            WaitUntil(panel, () => panel.FindAll(kStateMessage).Count == 1);
            Assert.Multiple(() =>
            {
                Assert.That(panel.Find(kStateMessage).TextContent, Is.EqualTo("E9041"));
                Assert.That(panel.Find(kStateMessage).ClassList, Does.Contain("alert-danger"));
            });
        }

        [TestCaseSource(nameof(kInjectionPayloads))]
        public void Panel_EncodesQuestionAnswerToolNameAndArguments(string payload)
        {
            Dictionary<string, object?> arguments = new() { { "value", payload } };
            chat.ChatClient = ScriptedChatClient.Returning(ScriptedChatClient.Text(payload),
                AiChatTestUpdates.ApprovalRequest("request-1", payload, arguments),
                AiChatTestUpdates.Snapshot(AiChatTestUpdates.Conversation(payload, payload)));
            IRenderedComponent<AiChatPanel> panel = RenderOpenPanel();
            panel.Find(kInput).Input(payload);

            panel.Find(kSendButton).Click();

            WaitUntil(panel, () => panel.FindAll(kApproveButton).Count == 1);
            IElement content = panel.Find(kPanel);
            Assert.Multiple(() =>
            {
                Assert.That(panel.Find(kUserEntry).TextContent, Is.EqualTo(payload));
                Assert.That(panel.Find(kAnswerText).TextContent, Is.EqualTo(payload));
                Assert.That(panel.Find(".ai-chat-tool").TextContent, Is.EqualTo(payload));
                foreach (string element in kInjectedElements)
                {
                    Assert.That(content.QuerySelectorAll(element), Is.Empty, $"injected element {element}");
                }
                foreach (string attribute in kInjectedAttributes)
                {
                    Assert.That(content.QuerySelectorAll($"[{attribute}]"), Is.Empty, $"injected attribute {attribute}");
                }
            });
        }

        [Test]
        public void NewChatButton_DropsTheConversation()
        {
            IRenderedComponent<AiChatPanel> panel = RenderOpenPanel();
            panel.Find(kInput).Input(kQuestion);
            panel.Find(kSendButton).Click();
            WaitUntil(panel, () => panel.FindAll(kAnswerText).Count == 1);

            panel.Find(kNewChatButton).Click();

            WaitUntil(panel, () => panel.FindAll(kIntro).Count == 1);
            Assert.Multiple(() =>
            {
                Assert.That(panel.FindAll(kUserEntry), Is.Empty);
                Assert.That(Session.GetEntries(), Is.Empty);
            });
        }

        [Test]
        public void MinimizeButton_MinimizesThePanel()
        {
            IRenderedComponent<AiChatPanel> panel = RenderOpenPanel();

            panel.Find(kMinimizeButton).Click();

            WaitUntil(panel, () => panel.FindAll(kPanel).Count == 0);
            Assert.That(Session.IsOpen, Is.False);
        }

        [Test]
        public void ConfigChange_SwitchingTheChatOff_MinimizesThePanel()
        {
            IRenderedComponent<AiChatPanel> panel = RenderOpenPanel();

            userConfig.AiChatEnabled = false;
            userConfig.RaiseChange();

            WaitUntil(panel, () => panel.FindAll(kPanel).Count == 0);
            Assert.That(Session.IsOpen, Is.False);
        }

        [Test]
        public async Task Resize_KeepsTheWidthInTheSession()
        {
            IRenderedComponent<AiChatPanel> panel = RenderOpenPanel();
            Sidebar sidebar = panel.FindComponent<Sidebar>().Instance;

            await panel.InvokeAsync(() => sidebar.WidthChanged.InvokeAsync(600));

            Assert.That(Session.PanelWidth, Is.EqualTo(600));
        }

        [Test]
        public void Dispose_StopsFollowingTheSession()
        {
            IRenderedComponent<AiChatPanel> panel = RenderOpenPanel();

            panel.Instance.Dispose();

            Assert.DoesNotThrow(Session.Minimize);
        }

        /// <summary>
        /// Opens the session and renders the panel with its content.
        /// </summary>
        private IRenderedComponent<AiChatPanel> RenderOpenPanel()
        {
            Session.Open();
            IRenderedComponent<AiChatPanel> panel = context.Render<AiChatPanel>();
            WaitUntil(panel, () => panel.FindAll(kInput).Count == 1);
            return panel;
        }

        /// <summary>
        /// Waits until the condition holds after a render, failing the test with a timeout otherwise. Used instead of
        /// WaitForAssertion: NUnit keeps every failed Assert.That in the test result, also one that WaitForAssertion
        /// catches and retries on a later render, e.g. of a run that ends asynchronously, so a wait must not assert.
        /// The condition must not throw either, as WaitForState stops waiting then.
        /// </summary>
        private static void WaitUntil(IRenderedComponent<AiChatPanel> panel, Func<bool> condition)
        {
            panel.WaitForState(condition);
        }

        /// <summary>
        /// Returns a client answering with the thread, the given answer and the conversation.
        /// </summary>
        private static ScriptedChatClient CreateAnsweringClient(string answer)
        {
            return ScriptedChatClient.Returning(AiChatTestUpdates.RunStarted(kThreadId), ScriptedChatClient.Text(answer),
                AiChatTestUpdates.Snapshot(AiChatTestUpdates.Conversation(kQuestion, answer)));
        }

        /// <summary>
        /// Returns a client asking to approve two calls of the given tool.
        /// </summary>
        private static ScriptedChatClient CreateApprovalClient(string toolName)
        {
            return ScriptedChatClient.Returning(AiChatTestUpdates.RunStarted(kThreadId),
                AiChatTestUpdates.ApprovalRequest("request-1", toolName, kStoreArguments),
                AiChatTestUpdates.ApprovalRequest("request-2", toolName),
                AiChatTestUpdates.Snapshot(AiChatTestUpdates.Conversation(kQuestion)));
        }
    }
}
