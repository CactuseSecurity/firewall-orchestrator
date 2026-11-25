using System.Net;
using AngleSharp.Dom;
using Bunit;
using FWO.Ai.Client;
using FWO.Ai.Client.ReasonChecks;
using FWO.Config.Api;
using FWO.Ui.Services;
using FWO.Ui.Shared;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NUnit.Framework;
using System.Threading.Channels;

namespace FWO.Test
{
    /// <summary>
    /// Tests of the AI reason check component: streaming, verdicts, output encoding, failures and cancelling.
    /// </summary>
    [TestFixture]
    internal class UiAiReasonCheckTest
    {
        private const string kAccessToken = "user.access.token";
        private const string kCheckButton = "button.ai-check-button";
        private const string kCancelButton = "button.ai-check-cancel";
        private const string kCloseButton = "button.ai-check-close";
        private const string kSpinner = ".ai-check-spinner";
        private const string kMessage = ".ai-check-message";
        private const string kVerdictBadge = ".ai-check-verdict";
        private const string kAnswer = ".ai-answer";
        private const string kGenuineAnswer = "Verdict: GENUINE\nCriteria: all met";
        private const string kRetryText = "retry in @@SECONDS@@ s";
        private const string kInvalidReasonText = "at most @@MAX_LENGTH@@ characters";
        private const string kDisabledAttribute = "disabled";
        private const string kWarningAlert = "alert-warning";
        private const string kErrorAlert = "alert-danger";

        private static readonly Dictionary<string, string> kPromptArguments = new() { { "reason", "nightly backup of the database" } };
        private static readonly AgentPromptRequest kPromptRequest = new(ConnectionReasonCheck.kPromptName, kPromptArguments);
        private static readonly List<AIContent> kApprovalRequests =
        [
            new ToolApprovalRequestContent("interrupt-1", new FunctionCallContent("call-1", "delete_owner"))
        ];
        private static readonly List<AIContent> kRunError = [new ErrorContent("no provider configured") { ErrorCode = "provider" }];
        private static readonly List<string> kInjectedAttributes = ["onerror", "onmouseover", "onclick"];

        private static readonly List<string> kInjectionPayloads =
        [
            "<img src=x onerror=alert(1)>",
            "\"><script>alert(1)</script>",
            "' onmouseover='x"
        ];

        private static readonly List<TestCaseData> kVerdictCases =
        [
            new TestCaseData("Verdict: GENUINE\nCriteria: met", "bg-success", "ai_verdict_genuine").SetName("Verdict_Genuine"),
            new TestCaseData("Verdict: INSUFFICIENT\nQuestions: which data?", "bg-warning", "ai_verdict_insufficient").SetName("Verdict_Insufficient"),
            new TestCaseData("Verdict: NOT_GENUINE\nNo purpose", "bg-danger", "ai_verdict_not_genuine").SetName("Verdict_NotGenuine"),
            new TestCaseData("The answer names no verdict.", "bg-secondary", "ai_verdict_unknown").SetName("Verdict_Unknown")
        ];

        private static readonly List<TestCaseData> kFailureCases =
        [
            new TestCaseData(AgentClientFailure.Unauthorized, "E9035", kWarningAlert),
            new TestCaseData(AgentClientFailure.Forbidden, "E9036", kWarningAlert),
            new TestCaseData(AgentClientFailure.RateLimited, "E9037", kWarningAlert),
            new TestCaseData(AgentClientFailure.RequestTooLarge, "E9040", kWarningAlert),
            new TestCaseData(AgentClientFailure.Unavailable, "E9041", kErrorAlert),
            new TestCaseData(AgentClientFailure.ServerError, "E9042", kErrorAlert),
            new TestCaseData(AgentClientFailure.Timeout, "E9043", kErrorAlert),
            new TestCaseData(AgentClientFailure.ProtocolError, "E9044", kErrorAlert)
        ];

        private IFwoAgentChatClientFactory chatClientFactory = null!;
        private ScriptedChatClient chatClient = null!;
        private Func<CancellationToken, Task<string?>>? tokenProvider;
        private IAccessTokenSource accessTokenSource = null!;
        private BunitContext context = null!;

        /// <summary>
        /// User config whose texts are their keys, except for the texts with placeholders.
        /// </summary>
        private sealed class AiCheckUserConfig : SimulatedUserConfig
        {
            public override string GetText(string key)
            {
                return key switch
                {
                    AiReasonCheckDisplay.kRateLimitedRetryTextKey => kRetryText,
                    AiReasonCheckDisplay.kInvalidReasonTextKey => kInvalidReasonText,
                    _ => base.GetText(key)
                };
            }
        }

        [SetUp]
        public void SetUp()
        {
            chatClient = ScriptedChatClient.Returning(ScriptedChatClient.Text(kGenuineAnswer));
            chatClientFactory = Substitute.For<IFwoAgentChatClientFactory>();
            chatClientFactory.CreateChatClient(Arg.Any<Func<CancellationToken, Task<string?>>>()).Returns(callInfo =>
            {
                tokenProvider = callInfo.Arg<Func<CancellationToken, Task<string?>>>();
                return chatClient;
            });
            accessTokenSource = Substitute.For<IAccessTokenSource>();
            accessTokenSource.GetAccessToken().Returns(kAccessToken);
            context = new BunitContext();
            context.Services.AddSingleton(chatClientFactory);
            context.Services.AddSingleton(accessTokenSource);
            context.Services.AddSingleton<UserConfig>(new AiCheckUserConfig());
        }

        [TearDown]
        public void TearDown()
        {
            context.Dispose();
        }

        [Test]
        public void RunCheck_StreamsTheAnswerAndShowsTheVerdictWhenCompleted()
        {
            Channel<ChatResponseUpdate> updates = Channel.CreateUnbounded<ChatResponseUpdate>();
            chatClient = ScriptedChatClient.Streaming(updates);
            IRenderedComponent<AiReasonCheck> check = RenderCheck();

            check.Find(kCheckButton).Click();
            updates.Writer.TryWrite(ScriptedChatClient.Text("Verdict: GEN"));

            WaitUntil(check, () => HasAnswer(check, "Verdict: GEN"));
            Assert.Multiple(() =>
            {
                Assert.That(check.FindAll(kSpinner), Has.Count.EqualTo(1));
                Assert.That(check.Markup, Does.Contain(AiReasonCheckDisplay.kRunningTextKey));
                Assert.That(check.Find(kCheckButton).HasAttribute(kDisabledAttribute), Is.True);
                Assert.That(check.FindAll(kCancelButton), Has.Count.EqualTo(1));
                Assert.That(check.FindAll(kVerdictBadge), Is.Empty);
            });

            updates.Writer.TryWrite(ScriptedChatClient.Text("UINE\nCriteria: all met"));
            updates.Writer.TryComplete();

            WaitUntil(check, () => check.FindAll(kVerdictBadge).Count == 1);
            Assert.Multiple(() =>
            {
                Assert.That(check.Find(kAnswer).TextContent, Is.EqualTo(kGenuineAnswer));
                Assert.That(check.Find(kVerdictBadge).ClassList, Does.Contain("bg-success"));
                Assert.That(check.Markup, Does.Contain(AiReasonCheckDisplay.kDisclaimerTextKey));
                Assert.That(check.FindAll(kSpinner), Is.Empty);
                Assert.That(check.Find(kCheckButton).HasAttribute(kDisabledAttribute), Is.False);
            });
        }

        [Test]
        public void RunCheck_SendsThePromptAsNewConversationWithTheSessionToken()
        {
            IRenderedComponent<AiReasonCheck> check = RenderCheck();

            check.Find(kCheckButton).Click();

            WaitUntil(check, () => check.FindAll(kVerdictBadge).Count == 1);
            ScriptedChatCall call = chatClient.Calls.Single();
            Assert.Multiple(() =>
            {
                Assert.That(call.Messages, Is.Empty);
                Assert.That(call.Options?.ConversationId, Is.Null);
                Assert.That(ScriptedChatClient.GetPromptName(call.Options), Is.EqualTo(ConnectionReasonCheck.kPromptName));
                Assert.That(ScriptedChatClient.GetPromptArguments(call.Options), Is.EqualTo(kPromptArguments));
                Assert.That(tokenProvider?.Invoke(CancellationToken.None).Result, Is.EqualTo(kAccessToken));
            });
        }

        [Test]
        public void RunCheck_SeparatesTheTextsOfSeveralMessages()
        {
            chatClient = ScriptedChatClient.Returning(ScriptedChatClient.Text("Verdict: GENUINE", "message-1"),
                ScriptedChatClient.Text("Criteria: all met", "message-2"));
            IRenderedComponent<AiReasonCheck> check = RenderCheck();

            check.Find(kCheckButton).Click();

            WaitUntil(check, () => check.FindAll(kVerdictBadge).Count == 1);
            Assert.That(check.Find(kAnswer).TextContent, Is.EqualTo("Verdict: GENUINE" + AiReasonCheckRun.kMessageSeparator + "Criteria: all met"));
        }

        [TestCaseSource(nameof(kVerdictCases))]
        public void RunCheck_ShowsTheBadgeOfTheVerdict(string answer, string expectedClass, string expectedTextKey)
        {
            chatClient = ScriptedChatClient.Returning(ScriptedChatClient.Text(answer));
            IRenderedComponent<AiReasonCheck> check = RenderCheck();

            check.Find(kCheckButton).Click();

            WaitUntil(check, () => check.FindAll(kVerdictBadge).Count == 1);
            IElement badge = check.Find(kVerdictBadge);
            Assert.Multiple(() =>
            {
                Assert.That(badge.ClassList, Does.Contain(expectedClass));
                Assert.That(badge.TextContent, Is.EqualTo(expectedTextKey));
                Assert.That(check.Find(kAnswer).TextContent, Is.EqualTo(answer));
            });
        }

        [TestCaseSource(nameof(kInjectionPayloads))]
        public void RunCheck_RendersStreamedAndFinalAnswerAsText(string payload)
        {
            string answer = $"Verdict: INSUFFICIENT\n{payload}";
            Channel<ChatResponseUpdate> updates = Channel.CreateUnbounded<ChatResponseUpdate>();
            chatClient = ScriptedChatClient.Streaming(updates);
            IRenderedComponent<AiReasonCheck> check = RenderCheck();

            check.Find(kCheckButton).Click();
            updates.Writer.TryWrite(ScriptedChatClient.Text(answer));
            WaitUntil(check, () => HasAnswer(check, answer));
            AssertNoInjectedMarkup(check);

            updates.Writer.TryComplete();
            WaitUntil(check, () => check.FindAll(kVerdictBadge).Count == 1);
            Assert.That(check.Find(kAnswer).TextContent, Is.EqualTo(answer));
            AssertNoInjectedMarkup(check);
        }

        [TestCaseSource(nameof(kFailureCases))]
        public void RunCheck_ShowsTheMessageOfTheFailure(AgentClientFailure failure, string expectedTextKey, string expectedAlertClass)
        {
            chatClient = ScriptedChatClient.Throwing(new AgentClientException(failure, "test failure", HttpStatusCode.BadRequest));
            IRenderedComponent<AiReasonCheck> check = RenderCheck();

            check.Find(kCheckButton).Click();

            WaitUntil(check, () => check.FindAll(kMessage).Count == 1);
            IElement message = check.Find(kMessage);
            Assert.Multiple(() =>
            {
                Assert.That(message.TextContent, Is.EqualTo(expectedTextKey));
                Assert.That(message.ClassList, Does.Contain(expectedAlertClass));
                Assert.That(check.FindAll(kAnswer), Is.Empty);
                Assert.That(check.FindAll(kSpinner), Is.Empty);
            });
        }

        [Test]
        public void RunCheck_NamesTheSecondsToWaitWhenRateLimited()
        {
            AgentClientException rateLimited = new(AgentClientFailure.RateLimited, "too many runs", HttpStatusCode.TooManyRequests,
                TimeSpan.FromSeconds(29.2));
            chatClient = ScriptedChatClient.Throwing(rateLimited);
            IRenderedComponent<AiReasonCheck> check = RenderCheck();

            check.Find(kCheckButton).Click();

            WaitUntil(check, () => HasMessage(check, "retry in 30 s"));
        }

        [Test]
        public void RunCheck_ShowsInterruptedMessageAndDoesNotResume()
        {
            chatClient = ScriptedChatClient.Returning(ScriptedChatClient.Text("partial"), new ChatResponseUpdate(ChatRole.Assistant, kApprovalRequests));
            IRenderedComponent<AiReasonCheck> check = RenderCheck();

            check.Find(kCheckButton).Click();

            WaitUntil(check, () => HasMessage(check, AiReasonCheckDisplay.kInterruptedTextKey));
            Assert.Multiple(() =>
            {
                Assert.That(check.Find(kMessage).ClassList, Does.Contain(kWarningAlert));
                Assert.That(check.FindAll(kAnswer), Is.Empty);
            });
            Assert.That(chatClient.Calls, Has.Count.EqualTo(1));
        }

        [Test]
        public void RunCheck_ShowsFailedMessageWhenTheRunFailed()
        {
            chatClient = ScriptedChatClient.Returning(ScriptedChatClient.Text("partial"), new ChatResponseUpdate(ChatRole.Assistant, kRunError));
            IRenderedComponent<AiReasonCheck> check = RenderCheck();

            check.Find(kCheckButton).Click();

            WaitUntil(check, () => HasMessage(check, AiReasonCheckDisplay.kFailedTextKey));
            Assert.Multiple(() =>
            {
                Assert.That(check.Find(kMessage).ClassList, Does.Contain(kErrorAlert));
                Assert.That(check.FindAll(kAnswer), Is.Empty);
            });
        }

        [Test]
        public void RunCheck_ShowsFailedMessageWhenTheAnswerIsEmpty()
        {
            chatClient = ScriptedChatClient.Returning(ScriptedChatClient.Text(" "));
            IRenderedComponent<AiReasonCheck> check = RenderCheck();

            check.Find(kCheckButton).Click();

            WaitUntil(check, () => HasMessage(check, AiReasonCheckDisplay.kFailedTextKey));
            Assert.That(check.FindAll(kAnswer), Is.Empty);
        }

        [Test]
        public void RunCheck_ShowsFailedMessageOnUnexpectedException()
        {
            chatClient = ScriptedChatClient.Throwing(new InvalidOperationException("unexpected"));
            IRenderedComponent<AiReasonCheck> check = RenderCheck();

            check.Find(kCheckButton).Click();

            WaitUntil(check, () => HasMessage(check, AiReasonCheckDisplay.kFailedTextKey));
            Assert.That(check.Find(kMessage).ClassList, Does.Contain(kErrorAlert));
        }

        [Test]
        public void RunCheck_WithInvalidReason_ShowsMessageAndDoesNotRun()
        {
            IRenderedComponent<AiReasonCheck> check = RenderCheck(() => throw new ArgumentException("The reason must not be blank."));

            check.Find(kCheckButton).Click();

            Assert.Multiple(() =>
            {
                Assert.That(check.Find(kMessage).TextContent, Is.EqualTo($"at most {ReasonCheckLimits.kMaxReasonLength} characters"));
                Assert.That(check.Find(kMessage).ClassList, Does.Contain(kWarningAlert));
            });
            Assert.That(chatClient.Calls, Is.Empty);
            accessTokenSource.DidNotReceive().GetAccessToken();
        }

        [Test]
        public void RunCheck_WhenDisabled_DisablesTheButtonAndDoesNotRun()
        {
            IRenderedComponent<AiReasonCheck> check = RenderCheck(disabled: true);

            Assert.That(check.Find(kCheckButton).HasAttribute(kDisabledAttribute), Is.True);
            check.Find(kCheckButton).Click();

            Assert.That(chatClient.Calls, Is.Empty);
        }

        [Test]
        public void RunCheck_RunsOnlyOneCheckAtATime()
        {
            Channel<ChatResponseUpdate> updates = Channel.CreateUnbounded<ChatResponseUpdate>();
            chatClient = ScriptedChatClient.Streaming(updates);
            IRenderedComponent<AiReasonCheck> check = RenderCheck();

            check.Find(kCheckButton).Click();
            check.Find(kCheckButton).Click();

            Assert.That(chatClient.Calls, Has.Count.EqualTo(1));
            updates.Writer.TryWrite(ScriptedChatClient.Text(kGenuineAnswer));
            updates.Writer.TryComplete();
            WaitUntil(check, () => check.FindAll(kVerdictBadge).Count == 1);
        }

        [Test]
        public void CancelRun_CancelsTheRunAndHidesTheAnswer()
        {
            chatClient = ScriptedChatClient.WaitingForCancellation();
            IRenderedComponent<AiReasonCheck> check = RenderCheck();

            check.Find(kCheckButton).Click();
            check.Find(kCancelButton).Click();

            WaitUntil(check, () => check.FindAll(kSpinner).Count == 0);
            Assert.Multiple(() =>
            {
                Assert.That(chatClient.Calls.Single().CancellationToken.IsCancellationRequested, Is.True);
                Assert.That(check.FindAll(kMessage), Is.Empty);
                Assert.That(check.FindAll(kAnswer), Is.Empty);
                Assert.That(check.Find(kCheckButton).HasAttribute(kDisabledAttribute), Is.False);
            });
        }

        [Test]
        public void Dispose_CancelsTheRunningCheck()
        {
            chatClient = ScriptedChatClient.WaitingForCancellation();
            IRenderedComponent<AiReasonCheck> check = RenderCheck();
            check.Find(kCheckButton).Click();

            check.Instance.Dispose();

            Assert.That(chatClient.Calls.Single().CancellationToken.IsCancellationRequested, Is.True);
        }

        [Test]
        public void CloseResult_HidesTheAnswer()
        {
            IRenderedComponent<AiReasonCheck> check = RenderCheck();
            check.Find(kCheckButton).Click();
            WaitUntil(check, () => check.FindAll(kCloseButton).Count == 1);

            check.Find(kCloseButton).Click();

            Assert.Multiple(() =>
            {
                Assert.That(check.FindAll(kAnswer), Is.Empty);
                Assert.That(check.FindAll(kVerdictBadge), Is.Empty);
            });
        }

        [Test]
        public void RunCheck_AfterFailure_ClearsTheMessage()
        {
            chatClient = new ScriptedChatClient(ScriptedChatClient.Failing(new AgentClientException(AgentClientFailure.Timeout, "slow")),
                ScriptedChatClient.Updates(ScriptedChatClient.Text(kGenuineAnswer)));
            IRenderedComponent<AiReasonCheck> check = RenderCheck();
            check.Find(kCheckButton).Click();
            WaitUntil(check, () => check.FindAll(kMessage).Count == 1);

            check.Find(kCheckButton).Click();

            WaitUntil(check, () => check.FindAll(kVerdictBadge).Count == 1);
            Assert.That(check.FindAll(kMessage), Is.Empty);
        }

        [Test]
        public void AiReasonCheckDisplay_MapsEveryFailureToItsOwnText()
        {
            List<string> textKeys = Enum.GetValues<AgentClientFailure>().Select(AiReasonCheckDisplay.FailureTextKey).ToList();

            Assert.Multiple(() =>
            {
                Assert.That(textKeys, Is.Unique);
                Assert.That(textKeys, Does.Not.Contain(AiReasonCheckDisplay.kFailedTextKey));
            });
        }

        [Test]
        public void AiReasonCheckDisplay_RateLimitedWithoutRetryAfter_UsesTheGeneralText()
        {
            AgentClientException rateLimited = new(AgentClientFailure.RateLimited, "too many runs");

            string message = AiReasonCheckDisplay.FailureMessage(new AiCheckUserConfig(), rateLimited);

            Assert.That(message, Is.EqualTo("E9037"));
        }

        [Test]
        public void AiReasonCheckDisplay_UnknownValues_UseTheFallbacks()
        {
            Assert.Multiple(() =>
            {
                Assert.That(AiReasonCheckDisplay.VerdictTextKey((ReasonVerdict)99), Is.EqualTo("ai_verdict_unknown"));
                Assert.That(AiReasonCheckDisplay.VerdictCssClass((ReasonVerdict)99), Is.EqualTo("bg-secondary"));
                Assert.That(AiReasonCheckDisplay.FailureTextKey((AgentClientFailure)99), Is.EqualTo(AiReasonCheckDisplay.kFailedTextKey));
                Assert.That(AiReasonCheckDisplay.IsUserSolvable((AgentClientFailure)99), Is.False);
            });
        }

        private IRenderedComponent<AiReasonCheck> RenderCheck(Func<AgentPromptRequest>? createRequest = null, bool disabled = false)
        {
            return context.Render<AiReasonCheck>(parameters => parameters
                .Add(component => component.CreateRequest, createRequest ?? (() => kPromptRequest))
                .Add(component => component.Disabled, disabled));
        }

        /// <summary>
        /// Waits until the condition holds after a render, failing the test with a timeout otherwise.
        /// </summary>
        private static void WaitUntil(IRenderedComponent<AiReasonCheck> check, Func<bool> condition)
        {
            check.WaitForState(condition);
        }

        /// <summary>
        /// Tells whether the check shows the given answer.
        /// </summary>
        private static bool HasAnswer(IRenderedComponent<AiReasonCheck> check, string answer)
        {
            return check.FindAll(kAnswer).Count == 1 && check.Find(kAnswer).TextContent == answer;
        }

        /// <summary>
        /// Tells whether the check shows the given message.
        /// </summary>
        private static bool HasMessage(IRenderedComponent<AiReasonCheck> check, string message)
        {
            return check.FindAll(kMessage).Count == 1 && check.Find(kMessage).TextContent == message;
        }

        private static void AssertNoInjectedMarkup(IRenderedComponent<AiReasonCheck> check)
        {
            List<IElement> elements = check.FindAll("*").ToList();
            Assert.Multiple(() =>
            {
                Assert.That(check.FindAll("img"), Is.Empty);
                Assert.That(check.FindAll("script"), Is.Empty);
                Assert.That(elements.Where(element => kInjectedAttributes.Any(element.HasAttribute)), Is.Empty);
                Assert.That(check.Find(kAnswer).Children, Is.Empty);
            });
        }
    }
}
