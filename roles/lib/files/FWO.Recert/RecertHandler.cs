using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api;
using FWO.Data;

namespace FWO.Recert
{
    public class RecertHandler(ApiConnection apiConnection, UserConfig userConfig)
    {
        public async Task RecertifyOwnerWithRules(FwoOwner owner, List<Rule> rules, string? comment = "")
        {
            await RecertifyOwner(owner, comment);
            foreach (var rule in rules)
            {
                await RecertifySingleRule(rule, owner, comment);
            }
        }

        public async Task RecertifyOwner(FwoOwner owner, string? comment = "")
        {
            DateTime recertDate = DateTime.Now;
            DateTime? nextRecertDate = owner.RecertInterval != null ? recertDate.AddDays((double)owner.RecertInterval) : (DateTime?)null;
            var recertVariables = new
            {
                ownerId = owner.Id,
                userDn = userConfig.User.Dn,
                recertified = true,
                recertDate = recertDate,
                nextRecertDate = nextRecertDate,
                comment = comment
            };
            ReturnId []? returnIds = (await apiConnection.SendQueryAsync<ReturnIdWrapper>(RecertQueries.recertifyOwner, recertVariables)).ReturnIds;
            if (returnIds != null && returnIds.Length > 0)
            {
                var ownerVariables = new
                {
                    id = owner.Id,
                    lastRecert = recertDate,
                    lastRecertifierId = userConfig.User.DbId,
                    lastRecertifierDn = userConfig.User.Dn,
                    nextRecertDate = nextRecertDate
                };
                await apiConnection.SendQueryAsync<ReturnId>(OwnerQueries.setOwnerLastRecert, ownerVariables);
            }
        }

        public async Task<bool> RecertifySingleRule(Rule rule, FwoOwner? owner, string? comment = "")
        {
            var variables = new
            {
                ruleMetadataId = rule.Metadata.Id,
                ruleId = rule.Id,
                ownerId = owner?.Id ?? 0,
                userDn = userConfig.User.Dn,
                recertified = rule.Metadata.Recert,
                recertDate = DateTime.Now,
                comment = comment
            };
            bool recertOk = (await apiConnection.SendQueryAsync<ReturnId>(RecertQueries.recertify, variables)).AffectedRows > 0;
            if (recertOk && rule.Metadata.Recert)
            {
                await InitRuleRecert(rule, owner);
            }
            return recertOk;
        }

        private async Task InitRuleRecert(Rule rule, FwoOwner? owner)
        {
            int recertInterval = owner?.RecertInterval ?? userConfig.RecertificationPeriod;
            var prepvariables = new
            {
                ruleMetadataId = rule.Metadata.Id,
                ruleId = rule.Id,
                ipMatch = rule.IpMatch != "" ? rule.IpMatch : null,
                ownerId = owner?.Id ?? 0,
                nextRecertDate = DateTime.Now.AddDays(recertInterval)
            };
            await apiConnection.SendQueryAsync<object>(RecertQueries.prepareNextRecertification, prepvariables);
        }
    }
}
