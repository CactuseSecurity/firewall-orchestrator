using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data.Workflow;

namespace FWO.Services.Workflow
{
    /// <summary>
    /// Lists workflow configurations page by page, filtered and ordered in the database.
    /// </summary>
    public class WorkflowConfigurationListService(ApiConnection apiConnection)
    {
        /// <summary>
        /// Reads one page of workflow configurations, ordered by name and then id.
        /// </summary>
        /// <param name="filter">Criteria; null or unset criteria do not restrict the result.</param>
        /// <param name="limit">Number of configurations to read.</param>
        /// <param name="offset">Number of configurations to skip.</param>
        public async Task<List<WorkflowConfigurationSummary>> GetConfigurationsAsync(WorkflowConfigurationFilter? filter, int limit, int offset)
        {
            return await apiConnection.SendQueryAsync<List<WorkflowConfigurationSummary>>(
                RequestQueries.getWorkflowConfigurationPage, new { where = BuildWhere(filter), limit, offset }) ?? [];
        }

        /// <summary>
        /// Builds the GraphQL filter from the set criteria only; text criteria match case-insensitively anywhere, with
        /// LIKE wildcards in them matched literally.
        /// </summary>
        /// <param name="filter">Criteria; null builds an empty filter that matches every configuration.</param>
        public static Dictionary<string, object> BuildWhere(WorkflowConfigurationFilter? filter)
        {
            Dictionary<string, object> where = [];
            if (filter is null)
            {
                return where;
            }
            if (filter.Id is int id)
            {
                where["id"] = new Dictionary<string, object> { ["_eq"] = id };
            }
            if (filter.Name is not null)
            {
                where["name"] = new Dictionary<string, object> { ["_ilike"] = WfObjectSearch.ToContainsPattern(filter.Name) };
            }
            if (filter.Description is not null)
            {
                where["description"] = new Dictionary<string, object> { ["_ilike"] = WfObjectSearch.ToContainsPattern(filter.Description) };
            }
            if (filter.IsActive is bool isActive)
            {
                where["is_active"] = new Dictionary<string, object> { ["_eq"] = isActive };
            }
            return where;
        }
    }
}
