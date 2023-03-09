using System.Diagnostics;
using FWO.Api.Data;
using FWO.Api.Client;
using FWO.Logging;
using FWO.Middleware.Client;
using FWO.Config.Api;

namespace FWO.Recert
{
    public class RecertRefresh
    {
        protected UserConfig userConfig;
        protected List<Management> managements;
        private readonly ApiConnection apiConnection;

        public RecertRefresh (UserConfig userConfigIn, ApiConnection apiConnectionIn)
        {
            userConfig = userConfigIn;
            apiConnection = apiConnectionIn;
        }

        public async Task<bool> RecalcRecerts()
        {
            double refreshDuration = 0;
            Stopwatch watch = new System.Diagnostics.Stopwatch();
            string secs = "";
            var noVariables = new { };

            try
            {
                JwtReader jwt = new JwtReader(userConfig.User.Jwt);
                jwt.Validate();

                watch.Start();
                List<FwoOwner> owners = await apiConnection.SendQueryAsync<List<FwoOwner>>(FWO.Api.Client.Queries.OwnerQueries.getOwners);
                List<Management> managements = await apiConnection.SendQueryAsync<List<Management>>(FWO.Api.Client.Queries.DeviceQueries.getManagementDetailsWithoutSecrets);
                ReturnId[]? returnIds =
                    (await apiConnection.SendQueryAsync<NewReturning>(FWO.Api.Client.Queries.RecertQueries.clearOpenRecerts, noVariables)).ReturnIds;
                // the clearOpenRecerts refreshes materialized view view_rule_with_owner as a side-effect
                watch.Stop();
                refreshDuration = watch.ElapsedMilliseconds / 1000.0;
                secs = refreshDuration.ToString("0.00");
                Log.WriteDebug("Refresh materialized view view_rule_with_owner", $"refresh took {secs} seconds");

                foreach (FwoOwner owner in owners)
                    await RecalcRecertsOfOwner(owner, managements);
            }
            catch (Exception)
            {
                return true;
            }
            return false;
        }

        private async Task RecalcRecertsOfOwner(FwoOwner owner, List<Management> managements)
        {
            double refreshDuration = 0;
            Stopwatch watch = new System.Diagnostics.Stopwatch();
            string secs = "";
            watch.Start();

            foreach (Management mgm in managements)
            {
                List<RecertificationBase> currentRecerts =
                    await apiConnection.SendQueryAsync<List<RecertificationBase>>(FWO.Api.Client.Queries.RecertQueries.getOpenRecerts, new { ownerId = owner.Id, mgmId = mgm.Id });

                if (currentRecerts.Count > 0)
                {
                    ReturnId[]? returnedIds = (await apiConnection.SendQueryAsync<NewReturning>(FWO.Api.Client.Queries.RecertQueries.addRecertEntries, new { recerts = currentRecerts })).ReturnIds;
                }
            }

            watch.Stop();
            refreshDuration = watch.ElapsedMilliseconds / 1000.0;
            secs = refreshDuration.ToString("0.00");
            Log.WriteDebug("Refresh Recertification", $"refresh for owner {owner.Name} took {secs} seconds");
        }

    }
}

