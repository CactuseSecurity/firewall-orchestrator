using FWO.Data;
using FWO.ExternalSystems.Tufin.SecureChange;

namespace FWO.Test
{
    /// <summary>
    /// SecureChange template source for unit tests: the given templates apply globally, optionally overridden per management.
    /// </summary>
    internal sealed class FixedSCTemplateSource(List<ExternalTicketTemplate> globalTemplates,
        Dictionary<int, List<ExternalTicketTemplate>>? templatesByManagement = null) : ISCTemplateSource
    {
        /// <summary>Management ids of every load, to check that the templates of all managements of a ticket are resolved at once.</summary>
        public List<List<int>> LoadedManagementIds { get; } = [];

        /// <inheritdoc/>
        public Task<SCTemplateLookup> Load(IReadOnlyCollection<int> managementIds)
        {
            LoadedManagementIds.Add(managementIds.ToList());
            return Task.FromResult(new SCTemplateLookup(globalTemplates, templatesByManagement));
        }
    }
}
