using FWO.Ai.Client;
using FWO.Data.Ai;
using FWO.Ui.Services;
using Microsoft.Extensions.AI;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Tests of the texts, styles and availability of the AI chat.
    /// </summary>
    [TestFixture]
    internal class UiAiChatDisplayTest
    {
        private const string kWarningAlert = "alert alert-warning";
        private const string kErrorAlert = "alert alert-danger";

        private static readonly List<TestCaseData> kAvailabilityCases = new()
        {
            new TestCaseData(true, AiProvider.OpenAi, true),
            new TestCaseData(true, AiProvider.None, false),
            new TestCaseData(false, AiProvider.Anthropic, false),
            new TestCaseData(false, AiProvider.None, false)
        };

        private static readonly List<TestCaseData> kStateCases = new()
        {
            new TestCaseData(AiChatAnswerState.Running, null, "", kErrorAlert).SetName("StateMessage_Running"),
            new TestCaseData(AiChatAnswerState.Completed, null, "", kErrorAlert).SetName("StateMessage_Completed"),
            new TestCaseData(AiChatAnswerState.Cancelled, null, AiChatDisplay.kCancelledTextKey, kWarningAlert).SetName("StateMessage_Cancelled"),
            new TestCaseData(AiChatAnswerState.Failed, null, "E9034", kErrorAlert).SetName("StateMessage_FailedRun"),
            new TestCaseData(AiChatAnswerState.Failed, AgentClientFailure.Forbidden, "E9036", kWarningAlert).SetName("StateMessage_Forbidden"),
            new TestCaseData(AiChatAnswerState.Failed, AgentClientFailure.Unavailable, "E9041", kErrorAlert).SetName("StateMessage_Unavailable")
        };

        [TestCaseSource(nameof(kAvailabilityCases))]
        public void IsAvailable_NeedsTheSwitchAndAProvider(bool chatEnabled, AiProvider provider, bool expected)
        {
            SimulatedUserConfig userConfig = new() { AiChatEnabled = chatEnabled, AiProvider = provider };

            Assert.That(AiChatDisplay.IsAvailable(userConfig), Is.EqualTo(expected));
        }

        [TestCaseSource(nameof(kStateCases))]
        public void StateMessageAndAlertClass_DependOnTheStateAndFailure(AiChatAnswerState state, AgentClientFailure? failure,
            string expectedMessage, string expectedAlertClass)
        {
            AiChatEntry entry = new(AiChatEntryKind.Assistant)
            {
                State = state,
                ClientFailure = failure == null ? null : new AgentClientException(failure.Value, "failure")
            };

            Assert.Multiple(() =>
            {
                Assert.That(AiChatDisplay.StateMessage(new SimulatedUserConfig(), entry), Is.EqualTo(expectedMessage));
                Assert.That(AiChatDisplay.StateAlertClass(entry), Is.EqualTo(expectedAlertClass));
            });
        }

        [TestCase(true, AiChatDisplay.kApprovedBadgeClass, AiChatDisplay.kApprovedTextKey)]
        [TestCase(false, AiChatDisplay.kRejectedBadgeClass, AiChatDisplay.kRejectedTextKey)]
        public void ApprovalBadge_ShowsTheAnswer(bool approved, string expectedClass, string expectedTextKey)
        {
            ToolApprovalRequestContent request = (ToolApprovalRequestContent)AiChatTestUpdates.ApprovalRequest("request-1", "store").Contents.Single();
            AiChatApproval approval = new(request) { Approved = approved };

            Assert.Multiple(() =>
            {
                Assert.That(AiChatDisplay.ApprovalBadgeClass(approval), Is.EqualTo(expectedClass));
                Assert.That(AiChatDisplay.ApprovalTextKey(approval), Is.EqualTo(expectedTextKey));
            });
        }
    }
}
