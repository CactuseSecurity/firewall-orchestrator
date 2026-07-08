using FWO.Data.Workflow;

namespace FWO.Services.Workflow
{
    /// <summary>
    /// Evaluates workflow visibility-group access against state matrices.
    /// </summary>
    public static class WorkflowVisibilityHelper
    {
        /// <summary>
        /// Returns whether the given object state is visible for the supplied visibility-group ids.
        /// </summary>
        public static bool CanAccessStatefulObject(WfStatefulObject statefulObject, StateMatrix stateMatrix, IEnumerable<int> userVisibilityGroupIds,
            IEnumerable<int>? exclusiveVisibilityGroupIds = null)
        {
            return stateMatrix.CanAccessState(statefulObject.StateId, userVisibilityGroupIds, exclusiveVisibilityGroupIds);
        }

        /// <summary>
        /// Returns whether the given state is visible for the supplied visibility-group ids.
        /// </summary>
        public static bool CanAccessState(int stateId, StateMatrix stateMatrix, IEnumerable<int> userVisibilityGroupIds,
            IEnumerable<int>? exclusiveVisibilityGroupIds = null)
        {
            return stateMatrix.CanAccessState(stateId, userVisibilityGroupIds, exclusiveVisibilityGroupIds);
        }
    }
}
