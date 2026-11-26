using FWO.Data.Workflow;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Tests the parameters of the action "Create implementation tasks": defaults, reading and writing, and the check of the
    /// target state for request tasks of an external ticket system.
    /// </summary>
    [TestFixture]
    internal class CreateImplTasksActionParamsTest
    {
        private const int kPlannedStateId = 149;
        private const int kPlanVerifiedStateId = 199;
        private const int kToImplementStateId = 200;
        private const int kInPlanningStateId = 110;
        private const int kUnknownStateId = 777;

        private static readonly List<int> kExistingStateIds = [kInPlanningStateId, kPlannedStateId, kPlanVerifiedStateId, kToImplementStateId];

        [TestCase("")]
        [TestCase("   ")]
        [TestCase("{}")]
        [TestCase("{not json")]
        public void FromExternalParams_WithoutValidTarget_DefaultsToToImplement(string externalParams)
        {
            CreateImplTasksActionParams parameters = CreateImplTasksActionParams.FromExternalParams(externalParams);

            Assert.That(parameters.ExternalTicketSystemTargetStateId, Is.EqualTo(CreateImplTasksActionParams.kToImplementStateId));
        }

        [Test]
        public void ToExternalParams_IsReadBackUnchanged()
        {
            CreateImplTasksActionParams parameters = new() { ExternalTicketSystemTargetStateId = kPlanVerifiedStateId };

            CreateImplTasksActionParams readBack = CreateImplTasksActionParams.FromExternalParams(parameters.ToExternalParams());

            Assert.That(readBack.ExternalTicketSystemTargetStateId, Is.EqualTo(kPlanVerifiedStateId));
        }

        [TestCase(kToImplementStateId, true)]
        [TestCase(kPlannedStateId, true)]
        [TestCase(kInPlanningStateId, false)]
        [TestCase(kUnknownStateId, false)]
        public void HasValidTargetState_NeedsAnExistingStateNotBelowTheRequiredOne(int targetStateId, bool expected)
        {
            CreateImplTasksActionParams parameters = new() { ExternalTicketSystemTargetStateId = targetStateId };

            Assert.That(parameters.HasValidTargetState(kExistingStateIds, kPlannedStateId), Is.EqualTo(expected));
        }
    }
}
