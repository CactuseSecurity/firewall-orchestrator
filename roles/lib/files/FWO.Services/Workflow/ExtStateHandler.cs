using FWO.Data;
using FWO.Data.Workflow;
using FWO.Api.Client;
using FWO.Api.Client.Queries;

namespace FWO.Services.Workflow
{
    public class ExtStateHandler
    {
        private static readonly HashSet<string> StaticStateNames = new(Enum.GetNames<ExtStates>());
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

            throw new InvalidOperationException($"Could not fetch external states: {BuildErrorMessage(extStateResponse.Errors)}");
        }

        public int? GetInternalStateId(ExtStates extState)
        {
            return extStates.FirstOrDefault(e => e.Name == extState.ToString() && e.StateId != null)?.StateId;
        }

        public int? GetInternalStateId(string extState)
        {
            return extStates.FirstOrDefault(e => e.Name == extState && e.StateId != null)?.StateId;
        }

        public string? GetExternalStateName(int stateId, bool preferManual = false)
        {
            return GetPreferredExternalStateName(extStates, stateId, preferManual);
        }

        public static string? GetPreferredExternalStateName(IEnumerable<WfExtState> extStates, int stateId, bool preferManual = false)
        {
            IEnumerable<WfExtState> matches = extStates
                .Where(e => e.StateId == stateId && !string.IsNullOrWhiteSpace(e.Name));

            if (preferManual)
            {
                string? manualMatch = matches.FirstOrDefault(e => !StaticStateNames.Contains(e.Name))?.Name;
                if (!string.IsNullOrWhiteSpace(manualMatch))
                {
                    return manualMatch;
                }
            }

            return matches.FirstOrDefault()?.Name;
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
