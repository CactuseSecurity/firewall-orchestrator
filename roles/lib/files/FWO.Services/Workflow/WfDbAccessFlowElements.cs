using FWO.Api.Client.Queries;
using FWO.Data.Flow;
using FWO.Data.Workflow;

namespace FWO.Services.Workflow
{
    public partial class WfDbAccess
    {
        private const string kIneligibleFlowElementTextKey = "E8017";

        /// <summary>
        /// Checks that every Flow catalog entry the request task attaches may still be written, before
        /// any element of the task is written.
        /// The element columns that carry the Flow ids are writable by the requesting user, so the ids can
        /// name an entry the request module never offered - one that is hidden or retired. The Hasura
        /// permissions refuse such a write, but they refuse the whole mutation with a permission error;
        /// checking here first turns that into a message that names what has to be corrected, and keeps a
        /// stale editor from silently re-attaching an entry that was withdrawn while the task was open.
        /// The check is as strict as the Hasura permission that will decide the write, and that differs
        /// between the two: an element the user is authoring now has to name an entry the request module
        /// offers, while an element that is already stored is written back with the values it carries,
        /// which include the ones the platform itself attached - the canonical ANY service and the ANY IP
        /// protocol of a protocol-agnostic external request. Holding a stored element to the stricter
        /// predicate would make every later save of such a ticket fail.
        /// Flow group ids are not checked here: they are written by the flow creation in the middleware,
        /// never by the request module, and the requesting user cannot read the Flow group tables at all.
        /// </summary>
        /// <param name="reqtask">The request task whose elements are about to be written.</param>
        /// <param name="taskIsBeingCreated">Whether the task is being created, which inserts every element
        /// it carries, rather than updated, which inserts only the elements that have no id yet.</param>
        /// <returns>True when every attached Flow entry and protocol may be written.</returns>
        private async Task<bool> FlowReferencesAreWritable(WfReqTask reqtask, bool taskIsBeingCreated)
        {
            List<WfReqElement> insertedElements = ElementsBeingInserted(reqtask, taskIsBeingCreated);
            if (insertedElements.Exists(element => !FlowObjectEligibility.IsRequestableProtocolId(element.ProtoId)))
            {
                return RefuseIneligibleFlowReference();
            }

            if (!await FlowNwObjectsAreLive(reqtask))
            {
                return false;
            }

            return await FlowSvcObjectsAreWritable(reqtask, insertedElements);
        }

        /// <summary>
        /// The elements that will be written by an insert, and that therefore have to satisfy the stricter
        /// insert check rather than only naming a live entry.
        /// Which elements those are follows from the operation, not from the element id: creating a task
        /// inserts every element it carries, whatever id the element happens to hold, while updating one
        /// inserts the elements that have no id yet and writes the rest back with an update.
        /// </summary>
        /// <param name="reqtask">The request task whose elements are about to be written.</param>
        /// <param name="taskIsBeingCreated">Whether the task is being created rather than updated.</param>
        /// <returns>The elements an insert will write.</returns>
        private static List<WfReqElement> ElementsBeingInserted(WfReqTask reqtask, bool taskIsBeingCreated)
        {
            return taskIsBeingCreated ? reqtask.Elements : [.. reqtask.Elements.Where(element => element.Id == 0)];
        }

        /// <summary>
        /// Checks the Flow network object ids of every element of the task. Visibility and lifecycle are
        /// the whole predicate for network objects, so a newly authored element and a stored one are held
        /// to the same one.
        /// </summary>
        /// <param name="reqtask">The request task whose elements are about to be written.</param>
        /// <returns>True when every attached Flow network object is live.</returns>
        private async Task<bool> FlowNwObjectsAreLive(WfReqTask reqtask)
        {
            List<long> nwObjectIds = CollectFlowIds(reqtask.Elements, element => element.FlowNetworkObjectId);
            List<FlowNwObject> liveObjects = await ReadLiveFlowObjects<FlowNwObject>(FlowQueries.getLiveFlowNwObjectIds, "nwObjIds", nwObjectIds);
            return AllIdsAreCovered(nwObjectIds, liveObjects, flowObject => flowObject.Id) || RefuseIneligibleFlowReference();
        }

        /// <summary>
        /// Checks the Flow service object ids of the task. Every attached entry has to be live; an entry an
        /// inserted element names additionally has to be one the request module offers, which the canonical
        /// ANY service is not.
        /// </summary>
        /// <param name="reqtask">The request task whose elements are about to be written.</param>
        /// <param name="insertedElements">The elements of that task an insert will write.</param>
        /// <returns>True when every attached Flow service object may be written by its element.</returns>
        private async Task<bool> FlowSvcObjectsAreWritable(WfReqTask reqtask, List<WfReqElement> insertedElements)
        {
            List<long> svcObjectIds = CollectFlowIds(reqtask.Elements, element => element.FlowServiceObjectId);
            List<FlowSvcObject> liveObjects = await ReadLiveFlowObjects<FlowSvcObject>(FlowQueries.getLiveFlowSvcObjectIds, "svcObjIds", svcObjectIds);
            if (!AllIdsAreCovered(svcObjectIds, liveObjects, flowObject => flowObject.Id))
            {
                return RefuseIneligibleFlowReference();
            }

            List<long> insertedSvcObjectIds = CollectFlowIds(insertedElements, element => element.FlowServiceObjectId);
            bool insertedElementNamesInternalObject = liveObjects.Exists(flowObject => insertedSvcObjectIds.Contains(flowObject.Id)
                && FlowObjectEligibility.IsInternalProtocolId(flowObject.ProtoId));
            return !insertedElementNamesInternalObject || RefuseIneligibleFlowReference();
        }

        /// <summary>
        /// Reports an attached Flow entry that may not be written and names it to the user.
        /// </summary>
        /// <returns>Always false, so a caller can return it directly.</returns>
        private bool RefuseIneligibleFlowReference()
        {
            DisplayMessageInUi(null, UserConfig.GetText("save_task"), UserConfig.GetText(kIneligibleFlowElementTextKey), true);
            return false;
        }

        /// <summary>
        /// Collects the distinct Flow ids the given elements carry in one of their Flow id columns.
        /// </summary>
        /// <param name="elements">The request elements to read.</param>
        /// <param name="selectId">Selects the Flow id column to collect.</param>
        /// <returns>The distinct ids that are actually set.</returns>
        private static List<long> CollectFlowIds(List<WfReqElement> elements, Func<WfReqElement, long?> selectId)
        {
            return [.. elements
                .Select(selectId)
                .Where(id => id is > 0)
                .Select(id => id!.Value)
                .Distinct()];
        }

        /// <summary>
        /// Asks the API which of the given Flow ids still name a live entry. The query carries the
        /// lifecycle predicate itself, so the answer does not depend on the permissions of the role the
        /// query runs under.
        /// </summary>
        /// <typeparam name="TFlowObject">The Flow catalog type being read.</typeparam>
        /// <param name="query">The API call that returns the live entries among the given ids.</param>
        /// <param name="variableName">Name of the id list variable of that API call.</param>
        /// <param name="ids">The ids to read, possibly empty.</param>
        /// <returns>The entries that are live, empty when nothing was asked for.</returns>
        private async Task<List<TFlowObject>> ReadLiveFlowObjects<TFlowObject>(string query, string variableName, List<long> ids)
        {
            if (ids.Count == 0)
            {
                return [];
            }

            Dictionary<string, object> variables = new() { [variableName] = ids };
            return await ApiConnection.SendQueryAsync<List<TFlowObject>>(query, variables) ?? [];
        }

        /// <summary>
        /// Reports whether every requested id was returned by the API.
        /// </summary>
        /// <typeparam name="TFlowObject">The Flow catalog type being checked.</typeparam>
        /// <param name="ids">The ids that were asked for.</param>
        /// <param name="flowObjects">The entries the API returned.</param>
        /// <param name="selectId">Reads the id from a returned entry.</param>
        /// <returns>True when every given id was covered.</returns>
        private static bool AllIdsAreCovered<TFlowObject>(List<long> ids, List<TFlowObject> flowObjects, Func<TFlowObject, long> selectId)
        {
            return ids.TrueForAll(id => flowObjects.Exists(flowObject => selectId(flowObject) == id));
        }
    }
}
