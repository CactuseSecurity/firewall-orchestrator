using FWO.Data;
using FWO.Data.Workflow;
using FWO.Api.Client;
using FWO.Api.Client.Queries;

namespace FWO.Services.Workflow
{
    public class ExtStateHandler
    {
        private readonly ApiConnection apiConnection;
        private List<WfExtState> extStates = [];

        public ExtStateHandler(ApiConnection apiConnection)
        {
            this.apiConnection = apiConnection;
            Init().GetAwaiter().GetResult();
        }

        public async Task Init()
        {
            ApiResponse<List<WfExtState>> extStateResponse = await apiConnection.SendQuerySafeAsync<List<WfExtState>>(RequestQueries.getExtStates);
            if (!extStateResponse.HasErrors && extStateResponse.Result != null)
            {
                extStates = extStateResponse.Result;
                return;
            }

            throw new InvalidOperationException($"Could not fetch external states: {BuildErrorMessage(extStateResponse.Errors)}");
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

        private static string BuildErrorMessage(string[]? errors)
        {
            if (errors == null || errors.Length == 0)
            {
                return "unknown error";
            }
            return string.Join(" | ", errors);
        }
    }
}
