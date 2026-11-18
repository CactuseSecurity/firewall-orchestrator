using System.Threading.Channels;
using FWO.Ai.Client;
using FWO.Ui.Services;
using Microsoft.Extensions.AI;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Tests of the AI chat session: conversation and thread across runs, tool approvals, failures, cancelling, new chat,
    /// change of the user, and the state of the panel.
    /// </summary>
    [TestFixture]
    internal class UiAiChatSessionTest
    {
        private const string kThreadId = "thread-1";
        private const string kQuestion = "Which rules allow ssh?";
        private const string kAnswer = "Rule 7 allows ssh.";
        private const string kNextQuestion = "And telnet?";
        private const string kNextAnswer = "No rule allows telnet.";
        private const string kOtherUserDn = "uid=other,ou=tenant0,ou=operator,ou=user,dc=fworch,dc=internal";
        private const string kStoreTool = "store_value";
        private const string kFirstRequest = "request-1";
        private const string kSecondRequest = "request-2";
        private const string kValueArgument = "value";
        private const string kValue = "42";

        private static readonly TimeSpan kWaitTimeout = TimeSpan.FromSeconds(5);
        private static readonly List<string> kQuestionOnly = new() { kQuestion };
        private static readonly List<string> kFirstTurnTexts = new() { kQuestion, kAnswer };
        private static readonly List<string> kNextTurnTexts = new() { kNextQuestion, kNextAnswer };
        private static readonly List<AiChatEntryKind> kUserThenAssistant = new() { AiChatEntryKind.User, AiChatEntryKind.Assistant };
        private static readonly List<string> kNextQuestionOnly = new() { kNextQuestion };
        private static readonly List<string> kSecondTurnTexts = new() { kQuestion, kAnswer, kNextQuestion };
        private static readonly List<bool> kApprovalAnswers = new() { true, false };
        private static readonly List<string> kApprovalRequestIds = new() { kFirstRequest, kSecondRequest };
        private static readonly Dictionary<string, object?> kStoreArguments = new() { { kValueArgument, kValue } };

        private static readonly List<TestCaseData> kIgnoredMessages = new()
        {
            new TestCaseData("").SetName("SendAsync_IgnoresEmptyMessage"),
            new TestCaseData("   ").SetName("SendAsync_IgnoresBlankMessage"),
            new TestCaseData(null).SetName("SendAsync_IgnoresMissingMessage"),
            new TestCaseData(new string('x', AiChatSession.kMaxMessageLength + 1)).SetName("SendAsync_IgnoresTooLongMessage")
        };

        private static readonly List<TestCaseData> kPanelWidths = new()
        {
            new TestCaseData(100, AiChatSession.kMinPanelWidth),
            new TestCaseData(500, 500),
            new TestCaseData(5000, AiChatSession.kMaxPanelWidth)
        };

        private static readonly List<TestCaseData> kSameUserCases = new()
        {
            new TestCaseData("", "", true),
            new TestCaseData("uid=a,dc=example", "uid=a,dc=example", true),
            new TestCaseData("uid=a,dc=example", "", false),
            new TestCaseData("", "uid=a,dc=example", false),
            new TestCaseData("uid=a,dc=example", "uid=b,dc=example", false)
        };

        private AiChatTestSession test = null!;

        private AiChatSession Session => test.Session;

        [SetUp]
        public void SetUp()
        {
            test = new AiChatTestSession(CreateAnsweringClient(kQuestion, kAnswer));
        }

        [TearDown]
        public void TearDown()
        {
            test.Dispose();
        }

        [Test]
        public async Task SendAsync_StreamsTheAnswerWithTheSessionToken()
        {
            Session.Draft = kQuestion;

            await Session.SendAsync(kQuestion);

            List<AiChatEntry> entries = Session.GetEntries();
            ScriptedChatCall call = test.ChatClient.Calls.Single();
            Assert.Multiple(() =>
            {
                Assert.That(entries.Select(entry => entry.Kind), Is.EqualTo(kUserThenAssistant));
                Assert.That(entries[0].Text, Is.EqualTo(kQuestion));
                Assert.That(entries[1].Text, Is.EqualTo(kAnswer));
                Assert.That(entries[1].State, Is.EqualTo(AiChatAnswerState.Completed));
                Assert.That(AiChatTestUpdates.GetTexts(call.Messages), Is.EqualTo(kQuestionOnly));
                Assert.That(call.Options?.ConversationId, Is.Null);
                Assert.That(test.TokenProvider?.Invoke(CancellationToken.None).Result, Is.EqualTo(AiChatTestSession.kAccessToken));
                Assert.That(Session.IsRunning, Is.False);
                Assert.That(Session.CanSend, Is.True);
                Assert.That(Session.Draft, Is.Empty);
            });
        }

        [Test]
        public async Task SendAsync_SendsTheReturnedConversationOnTheThreadOfTheRun()
        {
            await Session.SendAsync(kQuestion);
            test.ChatClient = CreateAnsweringClient(kQuestion, kAnswer, kNextQuestion, kNextAnswer);

            await Session.SendAsync(kNextQuestion);

            ScriptedChatCall call = test.ChatClient.Calls.Single();
            Assert.Multiple(() =>
            {
                Assert.That(AiChatTestUpdates.GetTexts(call.Messages), Is.EqualTo(kSecondTurnTexts));
                Assert.That(call.Options?.ConversationId, Is.EqualTo(kThreadId));
                Assert.That(Session.GetEntries().Select(entry => entry.Text).Last(), Is.EqualTo(kNextAnswer));
            });
        }

        [Test]
        public async Task SendAsync_WithoutReturnedConversation_KeepsTheAnswerInTheConversation()
        {
            test.ChatClient = ScriptedChatClient.Returning(ScriptedChatClient.Text(kAnswer));
            await Session.SendAsync(kQuestion);

            await Session.SendAsync(kNextQuestion);

            Assert.That(AiChatTestUpdates.GetTexts(test.ChatClient.Calls[1].Messages), Is.EqualTo(kSecondTurnTexts));
        }

        [TestCaseSource(nameof(kIgnoredMessages))]
        public async Task SendAsync_IgnoresMessagesThatAreBlankOrTooLong(string? message)
        {
            await Session.SendAsync(message);

            Assert.Multiple(() =>
            {
                Assert.That(test.ChatClient.Calls, Is.Empty);
                Assert.That(Session.GetEntries(), Is.Empty);
            });
        }

        [Test]
        public async Task SendAsync_TrimsTheMessage()
        {
            await Session.SendAsync($"  {kQuestion}\n");

            Assert.That(AiChatTestUpdates.GetTexts(test.ChatClient.Calls.Single().Messages), Is.EqualTo(kQuestionOnly));
        }

        [Test]
        public async Task SendAsync_WhileARunIsGoingOn_IsIgnored()
        {
            Channel<ChatResponseUpdate> updates = Channel.CreateUnbounded<ChatResponseUpdate>();
            test.ChatClient = ScriptedChatClient.Streaming(updates);
            Task firstRun = Session.SendAsync(kQuestion);
            WaitUntil(() => Session.IsRunning);

            await Session.SendAsync(kNextQuestion);
            updates.Writer.TryWrite(ScriptedChatClient.Text(kAnswer));
            updates.Writer.TryComplete();
            await firstRun;

            Assert.Multiple(() =>
            {
                Assert.That(test.ChatClient.Calls, Has.Count.EqualTo(1));
                Assert.That(Session.GetEntries().Select(entry => entry.Text), Is.EqualTo(kFirstTurnTexts));
            });
        }

        [Test]
        public async Task SendAsync_ShowsTheStreamedTextWhileTheRunIsGoingOn()
        {
            Channel<ChatResponseUpdate> updates = Channel.CreateUnbounded<ChatResponseUpdate>();
            test.ChatClient = ScriptedChatClient.Streaming(updates);
            Task run = Session.SendAsync(kQuestion);

            updates.Writer.TryWrite(ScriptedChatClient.Text("Rule 7"));
            WaitUntil(() => Session.GetEntries().Last().Text == "Rule 7");
            AiChatEntry answer = Session.GetEntries().Last();
            Assert.That(answer.State, Is.EqualTo(AiChatAnswerState.Running));

            updates.Writer.TryWrite(ScriptedChatClient.Text(" allows ssh."));
            updates.Writer.TryComplete();
            await run;

            Assert.Multiple(() =>
            {
                Assert.That(answer.Text, Is.EqualTo(kAnswer));
                Assert.That(answer.State, Is.EqualTo(AiChatAnswerState.Completed));
            });
        }

        [Test]
        public async Task SendAsync_RunError_FailsAndKeepsThePreviousConversation()
        {
            test.ChatClient = ScriptedChatClient.Returning(AiChatTestUpdates.RunStarted(kThreadId), AiChatTestUpdates.RunError("switched off"));

            await Session.SendAsync(kQuestion);
            await Session.SendAsync(kNextQuestion);

            AiChatEntry answer = Session.GetEntries()[1];
            Assert.Multiple(() =>
            {
                Assert.That(answer.State, Is.EqualTo(AiChatAnswerState.Failed));
                Assert.That(answer.ClientFailure, Is.Null);
                Assert.That(AiChatTestUpdates.GetTexts(test.ChatClient.Calls[1].Messages), Is.EqualTo(kNextQuestionOnly));
                Assert.That(test.ChatClient.Calls[1].Options?.ConversationId, Is.EqualTo(kThreadId));
            });
        }

        [Test]
        public async Task SendAsync_ClientFailure_KeepsTheFailureForTheMessage()
        {
            AgentClientException failure = new(AgentClientFailure.RateLimited, "too many runs", retryAfter: TimeSpan.FromSeconds(30));
            test.ChatClient = ScriptedChatClient.Throwing(failure);

            await Session.SendAsync(kQuestion);

            AiChatEntry answer = Session.GetEntries()[1];
            Assert.Multiple(() =>
            {
                Assert.That(answer.State, Is.EqualTo(AiChatAnswerState.Failed));
                Assert.That(answer.ClientFailure, Is.SameAs(failure));
                Assert.That(Session.IsRunning, Is.False);
            });
        }

        [Test]
        public async Task SendAsync_UnexpectedException_Fails()
        {
            test.ChatClient = ScriptedChatClient.Throwing(new InvalidOperationException("broken"));

            await Session.SendAsync(kQuestion);

            AiChatEntry answer = Session.GetEntries()[1];
            Assert.Multiple(() =>
            {
                Assert.That(answer.State, Is.EqualTo(AiChatAnswerState.Failed));
                Assert.That(answer.ClientFailure, Is.Null);
            });
        }

        [Test]
        public async Task SendAsync_RunWithoutAnswer_Fails()
        {
            test.ChatClient = ScriptedChatClient.Returning(AiChatTestUpdates.Snapshot(AiChatTestUpdates.Conversation(kQuestion)));

            await Session.SendAsync(kQuestion);

            Assert.That(Session.GetEntries()[1].State, Is.EqualTo(AiChatAnswerState.Failed));
        }

        [Test]
        public async Task Cancel_EndsTheRunAsCancelledAndKeepsThePreviousConversation()
        {
            test.ChatClient = ScriptedChatClient.WaitingForCancellation();
            Task run = Session.SendAsync(kQuestion);
            WaitUntil(() => Session.IsRunning && test.ChatClient.Calls.Count == 1);

            Session.Cancel();
            await run;
            test.ChatClient = CreateAnsweringClient(kNextQuestion, kNextAnswer);
            await Session.SendAsync(kNextQuestion);

            Assert.Multiple(() =>
            {
                Assert.That(Session.GetEntries()[1].State, Is.EqualTo(AiChatAnswerState.Cancelled));
                Assert.That(AiChatTestUpdates.GetTexts(test.ChatClient.Calls.Single().Messages), Is.EqualTo(kNextQuestionOnly));
            });
        }

        [Test]
        public void Cancel_WithoutRun_DoesNothing()
        {
            Assert.DoesNotThrow(Session.Cancel);
        }

        [Test]
        public async Task Approvals_WaitUntilAllAreAnsweredAndAreSentAfterTheConversation()
        {
            List<ChatMessage> interruptedConversation = AiChatTestUpdates.Conversation(kQuestion, "I will store the values.");
            test.ChatClient = ScriptedChatClient.Returning(AiChatTestUpdates.RunStarted(kThreadId),
                ScriptedChatClient.Text("I will store the values."),
                AiChatTestUpdates.ApprovalRequest(kFirstRequest, kStoreTool, kStoreArguments),
                AiChatTestUpdates.ApprovalRequest(kSecondRequest, kStoreTool),
                AiChatTestUpdates.Snapshot(interruptedConversation));

            await Session.SendAsync(kQuestion);

            AiChatEntry interruptedAnswer = Session.GetEntries()[1];
            Assert.Multiple(() =>
            {
                Assert.That(interruptedAnswer.State, Is.EqualTo(AiChatAnswerState.WaitingForApproval));
                Assert.That(interruptedAnswer.Approvals.Select(approval => approval.RequestId), Is.EqualTo(kApprovalRequestIds));
                Assert.That(interruptedAnswer.Approvals[0].ToolName, Is.EqualTo(kStoreTool));
                Assert.That(interruptedAnswer.Approvals[0].Arguments, Does.Contain($"\"{kValueArgument}\"").And.Contain(kValue));
                Assert.That(Session.WaitsForApproval, Is.True);
                Assert.That(Session.CanSend, Is.False);
            });

            test.ChatClient = CreateAnsweringClient(kQuestion, kAnswer);
            await Session.SendAsync(kNextQuestion);
            await Session.AnswerApprovalAsync(kFirstRequest, true);
            await Session.AnswerApprovalAsync(kFirstRequest, false);

            Assert.Multiple(() =>
            {
                Assert.That(test.ChatClient.Calls, Is.Empty);
                Assert.That(interruptedAnswer.Approvals[0].Approved, Is.True);
                Assert.That(Session.WaitsForApproval, Is.True);
            });

            await Session.AnswerApprovalAsync(kSecondRequest, false);

            ScriptedChatCall resumeCall = test.ChatClient.Calls.Single();
            List<ToolApprovalResponseContent> answers = resumeCall.Messages.Last().Contents.OfType<ToolApprovalResponseContent>().ToList();
            Assert.Multiple(() =>
            {
                Assert.That(AiChatTestUpdates.GetTexts(resumeCall.Messages.SkipLast(1)), Is.EqualTo(AiChatTestUpdates.GetTexts(interruptedConversation)));
                Assert.That(resumeCall.Messages.Last().Role, Is.EqualTo(ChatRole.User));
                Assert.That(answers.Select(answer => answer.RequestId), Is.EqualTo(kApprovalRequestIds));
                Assert.That(answers.Select(answer => answer.Approved), Is.EqualTo(kApprovalAnswers));
                Assert.That(resumeCall.Options?.ConversationId, Is.EqualTo(kThreadId));
                Assert.That(interruptedAnswer.State, Is.EqualTo(AiChatAnswerState.Completed));
                Assert.That(Session.GetEntries().Last().Text, Is.EqualTo(kAnswer));
                Assert.That(Session.WaitsForApproval, Is.False);
                Assert.That(Session.CanSend, Is.True);
            });
        }

        [Test]
        public async Task Approvals_WithoutReturnedConversation_Fail()
        {
            test.ChatClient = ScriptedChatClient.Returning(AiChatTestUpdates.ApprovalRequest(kFirstRequest, kStoreTool));

            await Session.SendAsync(kQuestion);

            Assert.Multiple(() =>
            {
                Assert.That(Session.GetEntries()[1].State, Is.EqualTo(AiChatAnswerState.Failed));
                Assert.That(Session.WaitsForApproval, Is.False);
            });
        }

        [Test]
        public async Task AnswerApprovalAsync_WithoutPendingApproval_DoesNothing()
        {
            await Session.SendAsync(kQuestion);

            await Session.AnswerApprovalAsync(kFirstRequest, true);

            Assert.That(test.ChatClient.Calls, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task StartNewChat_DropsTheEntriesTheConversationAndTheThread()
        {
            await Session.SendAsync(kQuestion);

            Session.StartNewChat();
            test.ChatClient = CreateAnsweringClient(kNextQuestion, kNextAnswer);
            await Session.SendAsync(kNextQuestion);

            ScriptedChatCall call = test.ChatClient.Calls.Single();
            Assert.Multiple(() =>
            {
                Assert.That(AiChatTestUpdates.GetTexts(call.Messages), Is.EqualTo(kNextQuestionOnly));
                Assert.That(call.Options?.ConversationId, Is.Null);
                Assert.That(Session.GetEntries().Select(entry => entry.Text), Is.EqualTo(kNextTurnTexts));
            });
        }

        [Test]
        public async Task StartNewChat_DuringARun_CancelsItAndIgnoresItsOutcome()
        {
            test.ChatClient = ScriptedChatClient.WaitingForCancellation();
            Task run = Session.SendAsync(kQuestion);
            WaitUntil(() => test.ChatClient.Calls.Count == 1);

            Session.StartNewChat();
            await run;

            Assert.Multiple(() =>
            {
                Assert.That(Session.GetEntries(), Is.Empty);
                Assert.That(Session.IsRunning, Is.False);
                Assert.That(Session.CanSend, Is.True);
            });
        }

        [Test]
        public async Task AuthenticationChange_ToOtherUser_DropsTheConversationAndMinimizes()
        {
            await Session.SendAsync(kQuestion);
            Session.Open();

            test.Authentication.ChangeUser(kOtherUserDn);

            WaitUntil(() => Session.GetEntries().Count == 0);
            Assert.That(Session.IsOpen, Is.False);
        }

        [Test]
        public async Task AuthenticationChange_ToNoUser_DropsTheConversation()
        {
            await Session.SendAsync(kQuestion);

            test.Authentication.ChangeUser("");

            WaitUntil(() => Session.GetEntries().Count == 0);
        }

        [Test]
        public async Task AuthenticationChange_OfTheSameUser_KeepsTheConversation()
        {
            await Session.SendAsync(kQuestion);
            Session.Open();

            test.Authentication.ChangeUser(AiChatTestSession.kUserDn);

            Assert.Multiple(() =>
            {
                Assert.That(Session.GetEntries(), Has.Count.EqualTo(2));
                Assert.That(Session.IsOpen, Is.True);
            });
        }

        [Test]
        public async Task SendAsync_ByAnotherUser_StartsANewConversation()
        {
            await Session.SendAsync(kQuestion);
            test.Authentication.SetUserSilently(kOtherUserDn);
            test.ChatClient = CreateAnsweringClient(kNextQuestion, kNextAnswer);

            await Session.SendAsync(kNextQuestion);

            Assert.Multiple(() =>
            {
                Assert.That(AiChatTestUpdates.GetTexts(test.ChatClient.Calls.Single().Messages), Is.EqualTo(kNextQuestionOnly));
                Assert.That(Session.GetEntries().Select(entry => entry.Text), Is.EqualTo(kNextTurnTexts));
            });
        }

        [Test]
        public async Task HasNewAnswer_IsSetForAnAnswerWhileMinimizedAndClearedOnOpen()
        {
            await Session.SendAsync(kQuestion);
            Assert.That(Session.HasNewAnswer, Is.True);

            Session.Open();
            Assert.That(Session.HasNewAnswer, Is.False);

            await Session.SendAsync(kNextQuestion);
            Assert.That(Session.HasNewAnswer, Is.False);
        }

        [Test]
        public void ToggleOpenAndMinimize_ChangeThePanelAndNotify()
        {
            int changes = 0;
            Session.Changed += () => changes++;

            Session.Toggle();
            Assert.That(Session.IsOpen, Is.True);
            Session.Open();
            Session.Toggle();
            Assert.That(Session.IsOpen, Is.False);
            Session.Minimize();

            Assert.That(changes, Is.EqualTo(2));
        }

        [TestCaseSource(nameof(kPanelWidths))]
        public void PanelWidth_IsKeptInItsLimits(int width, int expectedWidth)
        {
            int changes = 0;
            Session.Changed += () => changes++;

            Session.PanelWidth = width;
            Session.PanelWidth = width;

            Assert.Multiple(() =>
            {
                Assert.That(Session.PanelWidth, Is.EqualTo(expectedWidth));
                Assert.That(changes, Is.EqualTo(expectedWidth == AiChatSession.kDefaultPanelWidth ? 0 : 1));
            });
        }

        [Test]
        public async Task Changed_IsRaisedForTheStartAndTheEndOfARun()
        {
            int changes = 0;
            Session.Changed += () => changes++;

            await Session.SendAsync(kQuestion);

            Assert.That(changes, Is.GreaterThanOrEqualTo(2));
        }

        [Test]
        public async Task Changed_HandlerFailure_DoesNotStopTheRun()
        {
            Session.Changed += () => throw new InvalidOperationException("handler failed");

            await Session.SendAsync(kQuestion);

            Assert.That(Session.GetEntries()[1].State, Is.EqualTo(AiChatAnswerState.Completed));
        }

        [Test]
        public async Task Dispose_CancelsTheRunAndIgnoresLaterMessages()
        {
            test.ChatClient = ScriptedChatClient.WaitingForCancellation();
            Task run = Session.SendAsync(kQuestion);
            WaitUntil(() => test.ChatClient.Calls.Count == 1);

            Session.Dispose();
            await run;
            Session.Dispose();
            await Session.SendAsync(kNextQuestion);

            Assert.Multiple(() =>
            {
                Assert.That(Session.GetEntries()[1].State, Is.EqualTo(AiChatAnswerState.Cancelled));
                Assert.That(test.ChatClient.Calls, Has.Count.EqualTo(1));
            });
        }

        [TestCaseSource(nameof(kSameUserCases))]
        public void IsSameUser_ComparesTheDistinguishedNames(string leftDn, string rightDn, bool expected)
        {
            Assert.That(AiChatSession.IsSameUser(leftDn, rightDn), Is.EqualTo(expected));
        }

        /// <summary>
        /// Returns a client answering with the thread, the answer (the last text) and the conversation of the given texts.
        /// </summary>
        private static ScriptedChatClient CreateAnsweringClient(params string[] conversationTexts)
        {
            return ScriptedChatClient.Returning(AiChatTestUpdates.RunStarted(kThreadId), ScriptedChatClient.Text(conversationTexts[^1]),
                AiChatTestUpdates.Snapshot(AiChatTestUpdates.Conversation(conversationTexts)));
        }

        /// <summary>
        /// Waits until the condition holds, failing after <see cref="kWaitTimeout"/>.
        /// </summary>
        private static void WaitUntil(Func<bool> condition)
        {
            Assert.That(SpinWait.SpinUntil(condition, kWaitTimeout), Is.True, "The condition was not met in time.");
        }
    }
}
