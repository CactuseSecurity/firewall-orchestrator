using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data.Workflow;

namespace FWO.Services
{
    public class WfStateDict
    {
        public Dictionary<int, string> Name = [];

        public async Task Init(ApiConnection apiConnection)
        {
            List<WfState> states = await apiConnection.SendQueryAsync<List<WfState>>(RequestQueries.getStates);
            Name = [];
            foreach(var state in states)
            {
                Name.Add(state.Id, state.Name);
            }
        }
    }
}
