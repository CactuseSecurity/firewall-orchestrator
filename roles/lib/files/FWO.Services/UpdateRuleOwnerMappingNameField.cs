using FWO.Api.Client;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Services.EventMediator.Events;
using System;
using System.Collections.Generic;
using System.Text;

namespace FWO.Services
{
    public class UpdateRuleOwnerMappingNameField : UpdateRuleOwnerMappingBase
    {
        public override OwnerMappingSourceStm Source => OwnerMappingSourceStm.NameField;

        public UpdateRuleOwnerMappingNameField(ApiConnection apiConnection, GlobalConfig globalConfig)
            : base(apiConnection, globalConfig)
        {
        }

        public override async Task<bool> RunAsync(UpdateRuleOwnerMappingEventArgs? eventArgs = null)
        {
            bool isFullReInitialize = eventArgs?.isFullReInitialize ?? false;
            return await UpdateRuleOwners(RunFullReinitialize, RunIncremental, isFullReInitialize);
        }

        private async Task<bool> RunIncremental()
        {
            throw new NotImplementedException();
        }

        private async Task<bool> RunFullReinitialize()
        {
            throw new NotImplementedException();
        }
    }
}
