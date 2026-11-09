using FWO.Api.Client;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Logging;
using FWO.Services.EventMediator.Events;


namespace FWO.Services
{
    public class UpdateRuleOwnerMapping : FWImportChangesNotifierBase<UpdateRuleOwnerMappingEventArgs>
    {
        private const string kLogMessageTitle = "Update rule_owner Notifier";

        protected readonly ApiConnection apiConnection;
        protected GlobalConfig globalConfig;

        private readonly UpdateRuleOwnerMappingIpBased updateRuleOwnerMappingIpBased;
        private readonly UpdateRuleOwnerMappingCustomField updateRuleOwnerMappingCustomField;
        private readonly UpdateRuleOwnerMappingNameField updateRuleOwnerMappingNameField;
        private readonly UpdateRuleOwnerMappingDisabled updateRuleOwnerMappingDisabled;

        public UpdateRuleOwnerMapping(ApiConnection apiConnection, GlobalConfig globalConfig)
        {
            this.apiConnection = apiConnection;
            this.globalConfig = globalConfig;

            updateRuleOwnerMappingIpBased = new UpdateRuleOwnerMappingIpBased(apiConnection, globalConfig);
            updateRuleOwnerMappingCustomField = new UpdateRuleOwnerMappingCustomField(apiConnection, globalConfig);
            updateRuleOwnerMappingNameField = new UpdateRuleOwnerMappingNameField(apiConnection, globalConfig);
            updateRuleOwnerMappingDisabled = new UpdateRuleOwnerMappingDisabled(apiConnection, globalConfig);
        }

        /// <summary>
        /// Names the run that is about to start. A scheduled run with an empty backlog writes nothing else
        /// at all, so without this the log cannot tell a working scheduler from a stopped one.
        /// </summary>
        /// <param name="source">Configured mapping source.</param>
        /// <param name="isFullReInitialize">True when every mapping is rebuilt instead of the backlog processed.</param>
        /// <param name="triggeredByChange">True when a deliberate configuration change caused the run.</param>
        /// <returns>The message to log.</returns>
        public static string BuildRunStartMessage(OwnerMappingSourceStm source, bool isFullReInitialize, bool triggeredByChange)
        {
            string mode = isFullReInitialize ? "full reinitialize" : "incremental";
            string trigger = triggeredByChange ? "configuration change" : "schedule";
            return $"Starting rule_owner mapping run. Source: {source}, mode: {mode}, triggered by: {trigger}.";
        }

        protected override async Task<bool> Execute(UpdateRuleOwnerMappingEventArgs? eventArgs, CancellationToken cancellationToken)
        {
            OwnerMappingSourceStm source = (OwnerMappingSourceStm)globalConfig.OwnerSoruceMappingID;
            Log.WriteInfo(kLogMessageTitle, BuildRunStartMessage(source, eventArgs?.isFullReInitialize ?? false,
                eventArgs?.TriggeredByChange ?? false));

            return source switch
            {
                OwnerMappingSourceStm.IpBased => await updateRuleOwnerMappingIpBased.RunAsync(eventArgs, cancellationToken),
                OwnerMappingSourceStm.CustomField => await updateRuleOwnerMappingCustomField.RunAsync(eventArgs, cancellationToken),
                OwnerMappingSourceStm.NameField => await updateRuleOwnerMappingNameField.RunAsync(eventArgs, cancellationToken),
                OwnerMappingSourceStm.Disabled => await updateRuleOwnerMappingDisabled.RunAsync(eventArgs, cancellationToken),
                _ => false
            };
        }

        public async Task HandleEvent(UpdateRuleOwnerMappingEvent evt)
        {
            try
            {
                bool success = await Run(evt.EventArgs);
                evt.EventArgs.Completion?.SetResult(success);
            }
            catch (Exception ex)
            {
                Log.WriteError("UpdateOwnerRuleMappings failed", ex.ToString());
                evt.EventArgs.Completion?.SetException(ex);
            }
        }
    }
}
