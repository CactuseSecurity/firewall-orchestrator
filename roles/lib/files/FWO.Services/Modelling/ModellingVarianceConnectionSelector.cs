using FWO.Data.Modelling;

namespace FWO.Services.Modelling
{
    /// <summary>
    /// Connections handed to the variance analysis and the connections left out of it.
    /// </summary>
    public class ModellingVarianceConnectionSelection
    {
        /// <summary>
        /// Connections that are analysed (no interfaces).
        /// </summary>
        public List<ModellingConnection> Analysed { get; } = [];

        /// <summary>
        /// Connections that are not analysed, with the reason (no interfaces).
        /// </summary>
        public List<ModellingExcludedConnection> Excluded { get; } = [];

        /// <summary>
        /// Tells whether the given connection is left out of the analysis.
        /// </summary>
        public bool IsExcluded(ModellingConnection connection)
        {
            return Excluded.Any(excluded => excluded.Connection.Id == connection.Id);
        }
    }

    /// <summary>
    /// Single place deciding which connections the variance analysis looks at. The report, the
    /// connection status (refresh button and background job) and the request all use it, so a
    /// connection is analysed by all of them or by none, and never silently dropped by only one.
    /// </summary>
    public static class ModellingVarianceConnectionSelector
    {
        /// <summary>
        /// Splits the connections into analysed and excluded ones. Interfaces are in neither list,
        /// as they are not implemented as rules.
        /// </summary>
        /// <param name="connections">Connections of the owner, with used interfaces already extracted.</param>
        /// <param name="dummyAppRoleId">Id of the dummy app role, which never counts as empty.</param>
        /// <param name="rolloutRemoved">Setting "rollout removed app servers".</param>
        public static ModellingVarianceConnectionSelection Select(IEnumerable<ModellingConnection> connections, long dummyAppRoleId, bool rolloutRemoved)
        {
            ModellingVarianceConnectionSelection selection = new();
            foreach (ModellingConnection connection in connections.Where(c => !c.IsInterface))
            {
                ConState? reason = connection.GetVarianceExclusionReason(dummyAppRoleId, rolloutRemoved);
                if (reason == null)
                {
                    selection.Analysed.Add(connection);
                }
                else
                {
                    selection.Excluded.Add(new() { Connection = connection, Reason = reason.Value });
                }
            }
            return selection;
        }
    }
}
