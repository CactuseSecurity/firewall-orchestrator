using FWO.Api.Client.Queries;
using FWO.Data.Flow;
using FWO.Data.Workflow;

namespace FWO.Services.Workflow
{
    public partial class WfDbAccess
    {
        private const string kIneligibleFlowElementTextKey = "E8017";

        /// <summary>
        /// Checks that every Flow catalog entry the request task attaches may still be requested, before
        /// any element of the task is written.
        /// The element columns that carry the Flow ids are writable by the requesting user, so the ids can
        /// name an entry the request module never offered - one that is hidden, retired, or an internal
        /// representation such as the canonical ANY service. The Hasura permissions refuse such a write,
        /// but they refuse the whole mutation with a permission error; checking here first turns that into
        /// a message that names what has to be corrected, and keeps a stale editor from silently
        /// re-attaching an entry that was withdrawn while the task was open.
        /// Flow group ids are not checked here: they are written by the flow creation in the middleware,
        /// never by the request module, and the requesting user cannot read the Flow group tables at all.
        /// </summary>
        /// <param name="reqtask">The request task whose elements are about to be written.</param>
        /// <returns>True when every attached Flow entry and protocol may be requested.</returns>
        private async Task<bool> FlowReferencesAreRequestable(WfReqTask reqtask)
        {
            if (reqtask.Elements.Any(element => !FlowObjectEligibility.IsRequestableProtocolId(element.ProtoId)))
            {
                DisplayMessageInUi(null, UserConfig.GetText("save_task"), UserConfig.GetText(kIneligibleFlowElementTextKey), true);
                return false;
            }

            List<long> nwObjectIds = CollectFlowIds(reqtask, element => element.FlowNetworkObjectId);
            List<long> svcObjectIds = CollectFlowIds(reqtask, element => element.FlowServiceObjectId);
            if (nwObjectIds.Count == 0 && svcObjectIds.Count == 0)
            {
                return true;
            }

            if (!await AllIdsAreRequestable<FlowNwObject>(FlowQueries.getRequestableFlowNwObjectIds, "nwObjIds", nwObjectIds, flowObject => flowObject.Id)
                || !await AllIdsAreRequestable<FlowSvcObject>(FlowQueries.getRequestableFlowSvcObjectIds, "svcObjIds", svcObjectIds, flowObject => flowObject.Id))
            {
                DisplayMessageInUi(null, UserConfig.GetText("save_task"), UserConfig.GetText(kIneligibleFlowElementTextKey), true);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Collects the distinct Flow ids the task elements carry in one of their Flow id columns.
        /// </summary>
        /// <param name="reqtask">The request task to read the elements from.</param>
        /// <param name="selectId">Selects the Flow id column to collect.</param>
        /// <returns>The distinct ids that are actually set.</returns>
        private static List<long> CollectFlowIds(WfReqTask reqtask, Func<WfReqElement, long?> selectId)
        {
            return [.. reqtask.Elements
                .Select(selectId)
                .Where(id => id is > 0)
                .Select(id => id!.Value)
                .Distinct()];
        }

        /// <summary>
        /// Asks the API which of the given Flow ids still name a requestable entry and reports whether all
        /// of them do. The query carries the eligibility predicate itself, so the answer does not depend on
        /// the permissions of the role the query runs under.
        /// </summary>
        /// <typeparam name="TFlowObject">The Flow catalog type being checked.</typeparam>
        /// <param name="query">The API call that returns the requestable entries among the given ids.</param>
        /// <param name="variableName">Name of the id list variable of that API call.</param>
        /// <param name="ids">The ids to check, never empty when the API is asked.</param>
        /// <param name="selectId">Reads the id from a returned entry.</param>
        /// <returns>True when every given id was returned as requestable.</returns>
        private async Task<bool> AllIdsAreRequestable<TFlowObject>(string query, string variableName, List<long> ids, Func<TFlowObject, long> selectId)
        {
            if (ids.Count == 0)
            {
                return true;
            }

            Dictionary<string, object> variables = new() { [variableName] = ids };
            List<TFlowObject> requestableObjects = await ApiConnection.SendQueryAsync<List<TFlowObject>>(query, variables) ?? [];
            return ids.TrueForAll(id => requestableObjects.Exists(flowObject => selectId(flowObject) == id));
        }
    }
}
