using FWO.Api.Data;
using FWO.Api.Client;
using FWO.Api.Client.Queries;

namespace FWO.Services
{
    public class ExtStateHandler
    {
        private readonly ApiConnection apiConnection;
        private List<WfExtState> extStates = [];

        public ExtStateHandler(ApiConnection apiConnection)
        {
            this.apiConnection = apiConnection;
            Task.Run(Init);
        }

        public async Task Init()
        {
            extStates = await apiConnection.SendQueryAsync<List<WfExtState>>(RequestQueries.getExtStates);
        }

        public int? GetInternalStateId(ExtStates extState)
        {
            return extStates.FirstOrDefault(e => e.Name == extState.ToString())?.StateId;
        }

        public int? GetInternalStateId(string extState)
        {
            return extStates.FirstOrDefault(e => e.Name == extState)?.StateId;
        }

        public bool IsInProgress(int stateId)
        {
            int doneStateId = GetInternalStateId(ExtStates.ExtReqDone) ?? 999;
            int rejectedStateId = GetInternalStateId(ExtStates.ExtReqRejected) ?? 999;
            return stateId < doneStateId && stateId < rejectedStateId;
        }

        public bool IsDone(int stateId)
        {
            return stateId == GetInternalStateId(ExtStates.ExtReqDone);
        }
    }
}
