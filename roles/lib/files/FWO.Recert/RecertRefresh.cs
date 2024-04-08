﻿using System.Diagnostics;
using FWO.GlobalConstants;
using FWO.Api.Data;
using FWO.Api.Client;
using FWO.Logging;

namespace FWO.Recert
{
    public class RecertRefresh
    {
        private readonly ApiConnection apiConnection;

        public RecertRefresh (ApiConnection apiConnectionIn)
        {
            apiConnection = apiConnectionIn;
        }

        public async Task<bool> RecalcRecerts()
        {
            Stopwatch watch = new ();

            try
            {
                watch.Start();
                List<FwoOwner> owners = await apiConnection.SendQueryAsync<List<FwoOwner>>(Api.Client.Queries.OwnerQueries.getOwners);
                List<Management> managements = await apiConnection.SendQueryAsync<List<Management>>(Api.Client.Queries.DeviceQueries.getManagementDetailsWithoutSecrets);
                ReturnId[]? returnIds = (await apiConnection.SendQueryAsync<NewReturning>(Api.Client.Queries.RecertQueries.clearOpenRecerts)).ReturnIds;
                Log.WriteDebug("Delete open recerts", $"deleted Ids: {(returnIds != null ? string.Join(",", Array.ConvertAll(returnIds, Id => Id.DeletedId)) : "")}");
                // the clearOpenRecerts refreshes materialized view view_rule_with_owner as a side-effect
                watch.Stop();
                Log.WriteDebug("Refresh materialized view view_rule_with_owner", $"refresh took {(watch.ElapsedMilliseconds / 1000.0).ToString("0.00")} seconds");

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
            Stopwatch watch = new ();
            watch.Start();

            foreach (Management mgm in managements)
            {
                List<RecertificationBase> currentRecerts =
                    await apiConnection.SendQueryAsync<List<RecertificationBase>>(Api.Client.Queries.RecertQueries.getOpenRecerts, new { ownerId = owner.Id, mgmId = mgm.Id });

                if (currentRecerts.Count > 0)
                {
                    await apiConnection.SendQueryAsync<NewReturning>(Api.Client.Queries.RecertQueries.addRecertEntries, new { recerts = currentRecerts });
                }
            }

            watch.Stop();
            Log.WriteDebug("Refresh Recertification", $"refresh for owner {owner.Name} took {(watch.ElapsedMilliseconds / 1000.0).ToString("0.00")} seconds");
        }
    }
}

