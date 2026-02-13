using FWO.Data;
using FWO.Data.Workflow;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Logging;

namespace FWO.Services
{
    public class ExtStateHandler
    {
        private const string CurrentRootField = "ext_state";
        private const string LegacyRootField = "request_ext_state";
        private readonly ApiConnection apiConnection;
        private List<WfExtState> extStates = [];

        public ExtStateHandler(ApiConnection apiConnection)
        {
            this.apiConnection = apiConnection;
            Task.Run(Init).Wait();
        }

        public async Task Init()
        {
            string? rootField = await DetectAvailableExtStateRootField();
            if (await TryLoadByDetectedRootField(rootField))
            {
                return;
            }

            await LoadWithCompatibilityFallback();
        }

        private async Task<bool> TryLoadByDetectedRootField(string? rootField)
        {
            if (rootField == CurrentRootField)
            {
                await LoadOrThrow(RequestQueries.getExtStates);
                return true;
            }
            if (rootField == LegacyRootField)
            {
                await LoadOrThrow(GetLegacyExtStateQuery());
                return true;
            }
            return false;
        }

        private async Task LoadWithCompatibilityFallback()
        {
            ApiResponse<List<WfExtState>> extStateResponse = await apiConnection.SendQuerySafeAsync<List<WfExtState>>(RequestQueries.getExtStates);
            if (TrySetExtStates(extStateResponse))
            {
                return;
            }

            // Fallback keeps compatibility when introspection is unavailable or disabled.
            if (IsMissingExtStateRootField(extStateResponse.Errors))
            {
                ApiResponse<List<WfExtState>> legacyResponse =
                    await apiConnection.SendQuerySafeAsync<List<WfExtState>>(GetLegacyExtStateQuery());
                if (TrySetExtStates(legacyResponse))
                {
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

        private async Task LoadOrThrow(string query)
        {
            ApiResponse<List<WfExtState>> response = await apiConnection.SendQuerySafeAsync<List<WfExtState>>(query);
            if (!TrySetExtStates(response))
            {
                throw new InvalidOperationException($"Could not fetch external states: {BuildErrorMessage(response.Errors)}");
            }
        }

        private bool TrySetExtStates(ApiResponse<List<WfExtState>> response)
        {
            if (response.HasErrors || response.Result == null)
            {
                return false;
            }
            extStates = response.Result;
            return true;
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
            return $"query getExtStatesLegacy {{ {LegacyRootField} (order_by: {{ id: asc }}) {{ id name state_id }} }}";
        }

        private async Task<string?> DetectAvailableExtStateRootField()
        {
            try
            {
                ApiResponse<GraphQlTypeInfo> queryRootResponse = await apiConnection.SendQuerySafeAsync<GraphQlTypeInfo>(
                    "query detectExtStateRootField { __type(name: \"query_root\") { fields { name } } }");
                if (queryRootResponse.HasErrors || queryRootResponse.Result?.Fields == null)
                {
                    return null;
                }

                if (queryRootResponse.Result.Fields.Any(field => field.Name == CurrentRootField))
                {
                    return CurrentRootField;
                }
                if (queryRootResponse.Result.Fields.Any(field => field.Name == LegacyRootField))
                {
                    return LegacyRootField;
                }
            }
            catch
            {
                // Fallback query path below handles non-introspectable schemas and test doubles.
            }
            return null;
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

        private sealed class GraphQlTypeInfo
        {
            public List<GraphQlFieldInfo> Fields { get; set; } = [];
        }

        private sealed class GraphQlFieldInfo
        {
            public string Name { get; set; } = "";
        }
    }
}
