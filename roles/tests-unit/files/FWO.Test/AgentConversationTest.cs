using FWO.Ai.Client;
using Microsoft.Extensions.AI;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Tests of the conversation kept after a run of the FWO agent that failed or ended without answer text: the tool
    /// calls the run made stay in it, and a run that ran no tool keeps the conversation sent before.
    /// </summary>
    [TestFixture]
    [Parallelizable]
    internal class AgentConversationTest
    {
        private const string kQuestion = "Store the value.";
        private const string kAnswer = "Stored.";
        private const string kCallId = "call-1";
        private const string kToolName = "store_value";
        private const string kResult = "stored";

        private static readonly List<ChatRole> kToolCallRoles = [ChatRole.User, ChatRole.Assistant, ChatRole.Tool];

        [Test]
        public void GetConversationUpToToolResults_WithoutSnapshot_ReturnsNull()
        {
            Assert.That(AgentConversation.GetConversationUpToToolResults(null), Is.Null);
        }

        [Test]
        public void GetConversationUpToToolResults_EndingWithToolResult_ReturnsIt()
        {
            List<ChatMessage> snapshot = CreateToolCallConversation();

            List<ChatMessage>? conversation = AgentConversation.GetConversationUpToToolResults(snapshot);

            Assert.That(conversation?.Select(message => message.Role), Is.EqualTo(kToolCallRoles));
        }

        [Test]
        public void GetConversationUpToToolResults_WithModelMessagesWithoutTextAfterToolResult_LeavesThemOut()
        {
            List<ChatMessage> snapshot = [.. CreateToolCallConversation(), new ChatMessage(ChatRole.Assistant, ""), new ChatMessage(ChatRole.Assistant, " ")];

            List<ChatMessage>? conversation = AgentConversation.GetConversationUpToToolResults(snapshot);

            Assert.That(conversation?.Select(message => message.Role), Is.EqualTo(kToolCallRoles));
        }

        [Test]
        public void GetConversationUpToToolResults_EndingWithUserMessage_ReturnsNull()
        {
            List<ChatMessage> snapshot = [new ChatMessage(ChatRole.User, kQuestion), new ChatMessage(ChatRole.Assistant, "")];

            Assert.That(AgentConversation.GetConversationUpToToolResults(snapshot), Is.Null);
        }

        [Test]
        public void GetConversationUpToToolResults_EndingWithAnswerText_ReturnsNull()
        {
            List<ChatMessage> snapshot = [.. CreateToolCallConversation(), new ChatMessage(ChatRole.Assistant, kAnswer)];

            Assert.That(AgentConversation.GetConversationUpToToolResults(snapshot), Is.Null);
        }

        [Test]
        public void GetConversationUpToToolResults_EndingWithToolCallWithoutResult_ReturnsNull()
        {
            List<AIContent> callContents = [new FunctionCallContent("call-2", kToolName)];
            List<ChatMessage> snapshot = [.. CreateToolCallConversation(), new ChatMessage(ChatRole.Assistant, callContents)];

            Assert.That(AgentConversation.GetConversationUpToToolResults(snapshot), Is.Null);
        }

        [Test]
        public void GetConversationUpToToolResults_WithEmptySnapshot_ReturnsNull()
        {
            List<ChatMessage> snapshot = [];

            Assert.That(AgentConversation.GetConversationUpToToolResults(snapshot), Is.Null);
        }

        /// <summary>
        /// Returns a conversation of a question, a tool call and its result.
        /// </summary>
        private static List<ChatMessage> CreateToolCallConversation()
        {
            List<AIContent> callContents = [new FunctionCallContent(kCallId, kToolName)];
            List<AIContent> resultContents = [new FunctionResultContent(kCallId, kResult)];
            return
            [
                new ChatMessage(ChatRole.User, kQuestion),
                new ChatMessage(ChatRole.Assistant, callContents),
                new ChatMessage(ChatRole.Tool, resultContents)
            ];
        }
    }
}
