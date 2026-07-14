using FWO.Config.Api;
using FWO.Data.Modelling;

namespace FWO.Services.Modelling
{
    public static class ModellingRequestConnectionSelector
    {
        public static List<ModellingConnection> ForWorkflowNotifications(List<ModellingConnection> connections, UserConfig userConfig, long dummyAppRoleId)
        {
            string stateMarker = ModIntegrationStateConfig.EffectiveMarker(userConfig.ModIntegrationStateMarker);
            HashSet<string> includedRequestStateNames = ModIntegrationStateConfig.IncludedRequestStateNames(userConfig.ModIntegrationStates);
            return [.. RelevantConnections(connections, userConfig, dummyAppRoleId)
                .Where(connection => connection.IsIntegrationStateIncludedForRequest(stateMarker, includedRequestStateNames))
                .OrderByDescending(connection => connection.IsCommonService)];
        }

        public static List<ModellingConnection> ForRegularRequests(List<ModellingConnection> connections, UserConfig userConfig, long dummyAppRoleId)
        {
            return [.. connections.Where(connection => connection.IsRelevantForVarianceAnalysis(dummyAppRoleId,
                userConfig.ModRolloutRemovedAppServers, userConfig.ModRequestOnlyOwnObjects))
                .OrderByDescending(connection => connection.IsCommonService)];
        }

        public static List<ModellingConnection> RelevantConnections(List<ModellingConnection> connections, UserConfig userConfig, long dummyAppRoleId)
        {
            return [.. connections.Where(connection => !connection.IsDocumentationOnly() &&
                connection.IsRelevantForVarianceAnalysis(dummyAppRoleId, userConfig.ModRolloutRemovedAppServers))];
        }
    }
}
