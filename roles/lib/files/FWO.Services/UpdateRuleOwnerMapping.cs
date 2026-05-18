using FWO.Api.Client;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Logging;
using FWO.Services.EventMediator.Events;


namespace FWO.Services
{
    public class UpdateRuleOwnerMapping : FWImportChangesNotifierBase<UpdateRuleOwnerMappingEventArgs>
    {
        protected readonly ApiConnection apiConnection;
        protected GlobalConfig globalConfig;

        private readonly UpdateRuleOwnerMappingIpBased updateRuleOwnerMappingIpBased;
        private readonly UpdateRuleOwnerMappingCustomField updateRuleOwnerMappingCustomField;
        private readonly UpdateRuleOwnerMappingNameField updateRuleOwnerMappingNameField;

        public UpdateRuleOwnerMapping(ApiConnection apiConnection, GlobalConfig globalConfig)
        {
            this.apiConnection = apiConnection;
            this.globalConfig = globalConfig;

            updateRuleOwnerMappingIpBased = new UpdateRuleOwnerMappingIpBased(apiConnection, globalConfig);
            updateRuleOwnerMappingCustomField = new UpdateRuleOwnerMappingCustomField(apiConnection, globalConfig);
            updateRuleOwnerMappingNameField = new UpdateRuleOwnerMappingNameField(apiConnection, globalConfig);
        }

        protected override async Task<bool> Execute(UpdateRuleOwnerMappingEventArgs? eventArgs = null)
        {
            return (OwnerMappingSourceStm)globalConfig.OwnerSoruceMappingID switch
            {
                OwnerMappingSourceStm.IpBased => await updateRuleOwnerMappingIpBased.RunAsync(eventArgs),
                OwnerMappingSourceStm.CustomField => await updateRuleOwnerMappingCustomField.RunAsync(eventArgs),
                OwnerMappingSourceStm.NameField => await updateRuleOwnerMappingNameField.RunAsync(eventArgs),
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
