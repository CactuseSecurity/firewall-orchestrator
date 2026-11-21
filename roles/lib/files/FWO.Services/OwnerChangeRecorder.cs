using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Data;

namespace FWO.Services
{
    /// <summary>
    /// Records a change of an owner made outside the app data import, e.g. a changed app id in the owner settings, as an
    /// owner import (import control of type OWNER with a changelog entry). The middleware maps the rules of the owner again
    /// with the next incremental rule owner mapping run, which also brings the App labels of these rules in line with the
    /// new app id.
    /// </summary>
    /// <param name="apiConnection">Api connection with write access to import controls and the owner changelog.</param>
    public class OwnerChangeRecorder(ApiConnection apiConnection)
    {
        /// <summary>
        /// Records the change of an owner as a completed owner import that the rule owner mapping has not processed yet.
        /// </summary>
        /// <param name="ownerId">Id of the changed owner.</param>
        /// <param name="importSource">Import source of the owner, if any.</param>
        /// <returns>The id of the import control of the change.</returns>
        /// <exception cref="InvalidOperationException">Thrown if no import control was created.</exception>
        public async Task<long> RecordChange(int ownerId, string? importSource)
        {
            InsertImportControl result = await apiConnection.SendQueryAsync<InsertImportControl>(ImportQueries.addImportForOwner,
                new { importTypeId = ImportType.OWNER });
            long controlId = result.Returning.FirstOrDefault()?.ControlId
                ?? throw new InvalidOperationException("Failed to create the import control of an owner change.");
            await apiConnection.SendQueryAsync<object>(OwnerQueries.updateChangelogOwner, new
            {
                control_id = controlId,
                old_owner_id = (long)ownerId,
                new_owner_id = (long)ownerId,
                change_action = ChangelogActionType.CHANGE,
                source_id = importSource
            });
            await apiConnection.SendQueryAsync<ImportControl>(ImportQueries.updateImportControlForRuleOwnerFull, new
            {
                controlId,
                stopTime = DateTime.UtcNow,
                successful = true,
                rule_owner_mapping_done = false
            });
            return controlId;
        }
    }
}
