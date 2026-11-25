using FWO.Ui.Services;
using Microsoft.Extensions.AI;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Tests of the collection of the updates of a run of the AI chat and of the tool approvals it shows.
    /// </summary>
    [TestFixture]
    internal class UiAiChatRunTest
    {
        private const string kThreadId = "thread-7";
        private const string kTool = "delete_owner";

        private static readonly List<string> kConversationTexts = ["question", "answer"];
        private static readonly Dictionary<string, object?> kArguments = new() { { "ownerId", 7 }, { "name", "App 1" } };

        [Test]
        public void Apply_CollectsTextThreadConversationApprovalsAndFirstError()
        {
            AiChatRun run = new();

            bool firstText = run.Apply(ScriptedChatClient.Text("Part 1", "message-1"));
            bool started = run.Apply(AiChatTestUpdates.RunStarted(kThreadId));
            bool secondText = run.Apply(ScriptedChatClient.Text("Part 2", "message-2"));
            run.Apply(AiChatTestUpdates.ApprovalRequest("request-1", kTool));
            run.Apply(AiChatTestUpdates.RunError("first"));
            run.Apply(AiChatTestUpdates.RunError("second"));
            run.Apply(AiChatTestUpdates.Snapshot(AiChatTestUpdates.Conversation("question", "answer")));

            Assert.Multiple(() =>
            {
                Assert.That(firstText, Is.True);
                Assert.That(started, Is.False);
                Assert.That(secondText, Is.True);
                Assert.That(run.Text, Is.EqualTo("Part 1" + AgentAnswerText.kMessageSeparator + "Part 2"));
                Assert.That(run.ThreadId, Is.EqualTo(kThreadId));
                Assert.That(run.ApprovalRequests.Single().RequestId, Is.EqualTo("request-1"));
                Assert.That(run.Error?.Message, Is.EqualTo("first"));
                Assert.That(AiChatTestUpdates.GetTexts(run.Conversation!), Is.EqualTo(kConversationTexts));
            });
        }

        [Test]
        public void Apply_IgnoresTextsOfOtherRoles()
        {
            AiChatRun run = new();

            bool addedText = run.Apply(new ChatResponseUpdate(ChatRole.User, "echo"));

            Assert.Multiple(() =>
            {
                Assert.That(addedText, Is.False);
                Assert.That(run.Text, Is.Empty);
                Assert.That(run.Conversation, Is.Null);
            });
        }

        [Test]
        public void Apply_NullUpdate_Throws()
        {
            AiChatRun run = new();

            Assert.Throws<ArgumentNullException>(() => run.Apply(null!));
        }

        [Test]
        public void Approval_ShowsToolNameAndIndentedArguments()
        {
            ToolApprovalRequestContent request = (ToolApprovalRequestContent)AiChatTestUpdates.ApprovalRequest("request-1", kTool, kArguments).Contents.Single();

            AiChatApproval approval = new(request);

            Assert.Multiple(() =>
            {
                Assert.That(approval.RequestId, Is.EqualTo("request-1"));
                Assert.That(approval.ToolName, Is.EqualTo(kTool));
                Assert.That(approval.Arguments, Does.Contain("\"ownerId\": 7").And.Contain("\"name\": \"App 1\""));
                Assert.That(approval.Arguments, Does.Contain("\n"));
                Assert.That(approval.Approved, Is.Null);
            });
        }

        [Test]
        public void Approval_WithoutArguments_ShowsNone()
        {
            ToolApprovalRequestContent request = (ToolApprovalRequestContent)AiChatTestUpdates.ApprovalRequest("request-1", kTool).Contents.Single();

            Assert.That(new AiChatApproval(request).Arguments, Is.Empty);
        }

        [Test]
        public void FormatArguments_CutsLongArguments()
        {
            Dictionary<string, object?> arguments = new() { { "text", new string('a', AiChatApproval.kMaxArgumentsLength * 2) } };

            string formatted = AiChatApproval.FormatArguments(arguments);

            Assert.Multiple(() =>
            {
                Assert.That(formatted, Has.Length.EqualTo(AiChatApproval.kMaxArgumentsLength));
                Assert.That(formatted, Does.EndWith(AiChatApproval.kCutMarker));
            });
        }

        [Test]
        public void FormatArguments_DoesNotSplitASurrogatePair()
        {
            string emojis = string.Concat(Enumerable.Repeat("\U0001F600", AiChatApproval.kMaxArgumentsLength));
            Dictionary<string, object?> arguments = new() { { "text", emojis } };

            string formatted = AiChatApproval.FormatArguments(arguments);
            string kept = formatted[..^AiChatApproval.kCutMarker.Length];

            Assert.Multiple(() =>
            {
                Assert.That(formatted, Has.Length.LessThanOrEqualTo(AiChatApproval.kMaxArgumentsLength));
                Assert.That(char.IsHighSurrogate(kept[^1]), Is.False);
            });
        }

        [Test]
        public void Approval_NullRequest_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => _ = new AiChatApproval(null!));
        }

        [Test]
        public void Entry_OfTheUserIsCompletedAndAnAnswerStartsRunning()
        {
            AiChatEntry message = new(AiChatEntryKind.User, "question");
            AiChatEntry answer = new(AiChatEntryKind.Assistant);

            Assert.Multiple(() =>
            {
                Assert.That(message.State, Is.EqualTo(AiChatAnswerState.Completed));
                Assert.That(answer.State, Is.EqualTo(AiChatAnswerState.Running));
                Assert.That(answer.Text, Is.Empty);
                Assert.That(answer.Approvals, Is.Empty);
                Assert.That(message.Id, Is.Not.EqualTo(answer.Id));
            });
        }
    }
}
