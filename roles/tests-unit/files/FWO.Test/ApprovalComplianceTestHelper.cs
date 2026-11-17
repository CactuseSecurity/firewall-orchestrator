using FWO.Api.Client;
using FWO.Data;
using FWO.Data.Workflow;
using FWO.Services.Workflow;

namespace FWO.Test
{
    /// <summary>Shared setup for tests of the compliance status in the approval views.</summary>
    internal static class ApprovalComplianceTestHelper
    {
        /// <summary>Attaches an action handler whose workflow has a conditional policy check, answered by the given checker.</summary>
        public static async Task AttachPolicyCheck(WfHandler handler, ApiConnection apiConnection, IRequestedRulePolicyChecker checker)
        {
            handler.ActionHandler = new ActionHandler(apiConnection, handler, null, true, checker);
            await handler.ActionHandler.Init([new WfState { Id = 2, Actions = [ConditionalPolicyCheckAction()] }]);
        }

        /// <summary>A conditional auto-promote action with a policy check.</summary>
        public static WfStateActionDataHelper ConditionalPolicyCheckAction()
        {
            return new WfStateActionDataHelper
            {
                Action = new WfStateAction
                {
                    ActionType = StateActionTypes.AutoPromote.ToString(),
                    ExternalParams = "{\"to_be_called\":\"PolicyCheck\",\"policy_ids\":[5]}"
                }
            };
        }

        /// <summary>An access task with source, destination and service, as checked by the policy check.</summary>
        public static WfReqTask CheckableAccessTask(long id, string title)
        {
            return new WfReqTask
            {
                Id = id,
                StateId = 2,
                Title = title,
                TaskType = WfTaskType.access.ToString(),
                Elements =
                [
                    new WfReqElement { Field = ElemFieldType.source.ToString(), Name = "client", IpString = "10.0.0.1" },
                    new WfReqElement { Field = ElemFieldType.destination.ToString(), Name = "server", IpString = "10.0.1.1" },
                    new WfReqElement { Field = ElemFieldType.service.ToString(), Name = "https", Port = 443, ProtoId = 6 }
                ]
            };
        }
    }

    /// <summary>Policy checker returning fixed results and counting the checks.</summary>
    internal sealed class FixedResultPolicyChecker(IReadOnlyDictionary<long, RequestTaskComplianceResult> results) : IRequestedRulePolicyChecker
    {
        /// <summary>Number of performed per-task checks.</summary>
        public int CheckCount { get; private set; }

        public Task<bool> AreRequestTasksCompliant(IEnumerable<int> policyIds, IEnumerable<WfReqTask> requestTasks)
        {
            return Task.FromResult(results.Values.All(result => result.IsCompliant));
        }

        public Task<IReadOnlyDictionary<long, RequestTaskComplianceResult>> CheckRequestTasks(IEnumerable<int> policyIds, IEnumerable<WfReqTask> requestTasks)
        {
            CheckCount++;
            return Task.FromResult(results);
        }
    }
}
