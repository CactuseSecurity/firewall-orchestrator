using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Data;

namespace FWO.Middleware.Server
{
    /// <summary>
    /// Class handling the OwnerChange Data Import and tracking the import process in the database
    /// </summary>
    public class OwnerChangeImportTracker
    {
        private readonly ApiConnection apiConnection;
        private long _importControlId = 0;

        /// <summary>
        /// Constructor for OwngerChange Data Import Tracker
        /// </summary>
        public OwnerChangeImportTracker(ApiConnection apiConnection)
        {
            this.apiConnection = apiConnection;
        }


        /// <summary>
        /// Ensure create Import_control when there are OwnerChanges 
        /// </summary>
        public async Task<long> EnsureAnCreateImportControl()
        {
            if (_importControlId != 0)
            {
                return _importControlId;
            }

            var lastImportControl = await apiConnection.SendQueryAsync<List<ImportControl>>(ImportQueries.getLastImportControl);
            var lastControlId = lastImportControl.FirstOrDefault()?.ControlId ?? 0;
            _importControlId = lastControlId + 1;

            await apiConnection.SendQueryAsync<ImportControl>(ImportQueries.addImportForOwner, new { controlId = _importControlId, import_type_id = ImportType.OWNER });

            return _importControlId;
        }

        /// <summary>
        /// create changelog_owner entrie when there are OwnerChanges 
        /// </summary>
        public async Task AddOwnerChange(long? oldOwnerId, long? newOwnerId, char action, string? importSource)
        {
            var controlId = await EnsureAnCreateImportControl();

            var variables = new
            {
                control_id = controlId,
                old_owner_id = oldOwnerId,
                new_owner_id = newOwnerId,
                change_action = action,
                source_id = importSource
            };

            await apiConnection.SendQueryAsync<object>(OwnerQueries.updateChangelogOwner, variables);
        }

        /// <summary>
        /// Ensure Import_control closes with success or failure after the import process is finished
        /// </summary>
        public async Task CompleteImport(bool successful)
        {
            if (_importControlId == 0) return;

            await apiConnection.SendQueryAsync<ImportControl>(ImportQueries.updateImportControlForRuleOwnerFull,
                new
                {
                    controlId = _importControlId,
                    stopTime = DateTime.UtcNow,
                    successful = successful,
                    rule_owner_mapping_done = false
                });

            _importControlId = 0;
        }
    }
}
