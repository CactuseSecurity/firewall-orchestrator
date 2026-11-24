using AGUI.Abstractions;
using FWO.Ui.Services;
using Microsoft.Extensions.AI;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Tests the collection of the outcome of a reason check run from the updates of the FWO agent chat client.
    /// </summary>
    [TestFixture]
    [Parallelizable]
    internal class UiAiReasonCheckRunTest
    {
        private const string kThreadId = "thread-1";
        private const string kRunId = "run-1";
        private const string kFirstText = "Verdict: ";
        private const string kSecondText = "GENUINE";
        private const string kOtherText = "Criteria: met";
        private const string kErrorMessage = "no provider";
        private const string kOtherErrorMessage = "later error";

        private static readonly List<AIContent> kTwoApprovalRequests =
        [
            new ToolApprovalRequestContent("interrupt-1", new FunctionCallContent("call-1", "delete_owner")),
            new ToolApprovalRequestContent("interrupt-2", new FunctionCallContent("call-2", "delete_owner"))
        ];
        private static readonly List<AIContent> kToolResult = [new FunctionResultContent("call-1", "result text")];
        private static readonly List<AIContent> kFirstError = [new ErrorContent(kErrorMessage) { ErrorCode = "provider" }];
        private static readonly List<AIContent> kSecondError = [new ErrorContent(kOtherErrorMessage)];

        /// <summary>
        /// Verifies that the texts of a message are passed on as they are, and that the text of another message starts
        /// with the separator.
        /// </summary>
        [Test]
        public void Apply_WithTexts_ReturnsTextsSeparatedPerMessage()
        {
            AiReasonCheckRun run = new();

            string first = run.Apply(ScriptedChatClient.Text(kFirstText, "message-1"));
            string second = run.Apply(ScriptedChatClient.Text(kSecondText, "message-1"));
            string withoutId = run.Apply(new ChatResponseUpdate(ChatRole.Assistant, kOtherText));
            string other = run.Apply(ScriptedChatClient.Text(kOtherText, "message-2"));

            Assert.Multiple(() =>
            {
                Assert.That(first, Is.EqualTo(kFirstText));
                Assert.That(second, Is.EqualTo(kSecondText));
                Assert.That(withoutId, Is.EqualTo(kOtherText));
                Assert.That(other, Is.EqualTo(AiReasonCheckRun.kMessageSeparator + kOtherText));
                Assert.That(run.PendingApprovals, Is.Zero);
                Assert.That(run.Error, Is.Null);
            });
        }

        /// <summary>
        /// Verifies that the first text of a run never starts with the separator, and that updates without text and
        /// tool results add no text.
        /// </summary>
        [Test]
        public void Apply_WithoutAssistantText_ReturnsNoText()
        {
            AiReasonCheckRun run = new();

            string toolResult = run.Apply(new ChatResponseUpdate(ChatRole.Tool, kToolResult) { MessageId = "message-0" });
            string empty = run.Apply(new ChatResponseUpdate { MessageId = "message-0" });
            string first = run.Apply(ScriptedChatClient.Text(kFirstText, "message-1"));

            Assert.Multiple(() =>
            {
                Assert.That(toolResult, Is.Empty);
                Assert.That(empty, Is.Empty);
                Assert.That(first, Is.EqualTo(kFirstText));
            });
        }

        /// <summary>
        /// Verifies that the tool approval requests are counted, that the first error is kept, and that the thread of
        /// RUN_STARTED is taken over.
        /// </summary>
        [Test]
        public void Apply_WithApprovalsErrorsAndThread_CollectsThem()
        {
            AiReasonCheckRun run = new();

            run.Apply(new ChatResponseUpdate { RawRepresentation = new RunStartedEvent { ThreadId = kThreadId, RunId = kRunId } });
            run.Apply(new ChatResponseUpdate(ChatRole.Assistant, kTwoApprovalRequests));
            run.Apply(new ChatResponseUpdate(ChatRole.Assistant, kFirstError));
            run.Apply(new ChatResponseUpdate(ChatRole.Assistant, kSecondError));

            Assert.Multiple(() =>
            {
                Assert.That(run.ThreadId, Is.EqualTo(kThreadId));
                Assert.That(run.PendingApprovals, Is.EqualTo(2));
                Assert.That(run.Error?.Message, Is.EqualTo(kErrorMessage));
            });
        }

        /// <summary>
        /// Verifies that a missing update is refused.
        /// </summary>
        [Test]
        public void Apply_WithNull_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new AiReasonCheckRun().Apply(null!));
        }
    }
}
