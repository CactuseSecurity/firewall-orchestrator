using FWO.Api.Data;
using FWO.Api.Client;
using FWO.Api.Client.Queries;

namespace FWO.Ui.Services
{
    public class ExtStateHandler
    {
        private readonly ApiConnection apiConnection;
        private List<RequestExtState> extStates = [];

        public ExtStateHandler(ApiConnection apiConnection)
        {
            this.apiConnection = apiConnection;
        }

        public async Task Init()
        {
            extStates = await apiConnection.SendQueryAsync<List<RequestExtState>>(RequestQueries.getExtStates);
        }

        public int? GetInternalStateId(ExtStates extState)
        {
            return extStates.FirstOrDefault(e => e.Name == extState.ToString())?.StateId;
        }
    }
}
