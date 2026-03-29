using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data.Workflow;

namespace FWO.Services.Workflow
{
    public class WfStateDict
    {
        public Dictionary<int, string> Name = [];

        /// <summary>
        /// Returns the workflow state name for a state id or the numeric id as fallback.
        /// </summary>
        public string GetName(int stateId)
        {
            return Name.TryGetValue(stateId, out string? stateName) && !string.IsNullOrWhiteSpace(stateName)
                ? stateName
                : stateId.ToString();
        }

        public async Task Init(ApiConnection apiConnection)
        {
            List<WfState> states = await apiConnection.SendQueryAsync<List<WfState>>(RequestQueries.getStates);
            Name = [];
            foreach (var state in states)
            {
                Name.Add(state.Id, state.Name);
            }
        }
    }
}
