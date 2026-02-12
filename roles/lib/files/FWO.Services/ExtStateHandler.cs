using FWO.Data;
using FWO.Data.Workflow;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Logging;

namespace FWO.Services
{
    public class ExtStateHandler
    {
        private readonly ApiConnection apiConnection;
        private List<WfExtState> extStates = [];

        public ExtStateHandler(ApiConnection apiConnection)
        {
            this.apiConnection = apiConnection;
            Task.Run(Init).Wait();
        }

        public async Task Init()
        {
            ApiResponse<List<WfExtState>> extStateResponse = await apiConnection.SendQuerySafeAsync<List<WfExtState>>(RequestQueries.getExtStates);
            if (!extStateResponse.HasErrors && extStateResponse.Result != null)
            {
                extStates = extStateResponse.Result;
                return;
            }

            if (IsMissingExtStateRootField(extStateResponse.Errors))
            {
                ApiResponse<List<WfExtState>> legacyResponse =
                    await apiConnection.SendQuerySafeAsync<List<WfExtState>>(GetLegacyExtStateQuery());
                if (!legacyResponse.HasErrors && legacyResponse.Result != null)
                {
                    extStates = legacyResponse.Result;
                    return;
                }

                if (IsMissingExtStateRootField(legacyResponse.Errors))
                {
                    extStates = [];
                    Log.WriteWarning("GetExtStates",
                        "No GraphQL root field for ext state found (checked ext_state and request_ext_state). Continuing with empty ext-state mapping.");
                    return;
                }

                throw new InvalidOperationException($"Could not fetch external states: {BuildErrorMessage(legacyResponse.Errors)}");
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

        private static string GetLegacyExtStateQuery()
        {
            return "query getExtStatesLegacy { request_ext_state (order_by: { id: asc }) { id name state_id } }";
        }

        private static bool IsMissingExtStateRootField(string[]? errors)
        {
            if (errors == null || errors.Length == 0)
            {
                return false;
            }

            return errors.Any(error =>
                !string.IsNullOrWhiteSpace(error)
                && error.Contains("field", StringComparison.OrdinalIgnoreCase)
                && error.Contains("query_root", StringComparison.OrdinalIgnoreCase)
                && (error.Contains("ext_state", StringComparison.OrdinalIgnoreCase)
                    || error.Contains("request_ext_state", StringComparison.OrdinalIgnoreCase)));
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
