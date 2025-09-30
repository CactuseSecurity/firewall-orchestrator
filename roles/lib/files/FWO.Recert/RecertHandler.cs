using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api;
using FWO.Data;

namespace FWO.Recert
{
    public class RecertHandler(ApiConnection apiConnection, UserConfig userConfig)
    {
        public async Task<FwoOwner> RecertifyOwnerWithRules(FwoOwner owner, List<Rule> rules, string? comment)
        {
            FwoOwner recertifiedOwner = await RecertifyOwner(owner, comment);
            foreach (var rule in rules)
            {
                await RecertifySingleRuleFromOwner(rule, recertifiedOwner, comment);
            }
            return recertifiedOwner;
        }

        public async Task<bool> RecertifySingleRule(Rule rule, FwoOwner? owner, string? comment)
        {
            var variables = new
            {
                ruleId = rule.Id,
                ownerId = owner?.Id ?? 0,
                userDn = userConfig.User.Dn,
                recertified = rule.Metadata.Recert,
                recertDate = DateTime.Now,
                comment = comment,
                ownerRecertId = owner?.LastRecertId
            };
            bool recertOk = (await apiConnection.SendQueryAsync<ReturnId>(RecertQueries.recertify, variables)).AffectedRows > 0;
            if (rule.Metadata.Recert)
            {
                await InitRuleRecert(rule, owner);
            }
            return recertOk;
        }

        private async Task<FwoOwner> RecertifyOwner(FwoOwner owner, string? comment = "")
        {
            FwoOwner recertifiedOwner = new(owner);
            DateTime recertDate = DateTime.Now;
            int recertInterval = owner.RecertInterval != null ? (int)owner.RecertInterval : userConfig.RecertificationPeriod;
            DateTime? nextRecertDate = recertInterval > 0 ? recertDate.AddDays((double)recertInterval) : (DateTime?)null;
            var recertVariables = new
            {
                ownerId = owner.Id,
                userDn = userConfig.User.Dn,
                recertified = true,
                recertDate = recertDate,
                nextRecertDate = nextRecertDate,
                comment = comment
            };
            ReturnId[]? returnIds = (await apiConnection.SendQueryAsync<ReturnIdWrapper>(RecertQueries.recertifyOwner, recertVariables)).ReturnIds;
            if (returnIds != null && returnIds.Length > 0)
            {
                recertifiedOwner.LastRecertId = returnIds[0].NewIdLong;
                recertifiedOwner.LastRecertified = recertDate;
                recertifiedOwner.LastRecertifierId = userConfig.User.DbId;
                recertifiedOwner.LastRecertifierDn = userConfig.User.Dn;
                recertifiedOwner.NextRecertDate = nextRecertDate;
                await UpdateRecertifiedOwner(recertifiedOwner);
            }
            return recertifiedOwner;
        }

        private async Task UpdateRecertifiedOwner(FwoOwner owner)
        {
            var ownerVariables = new
            {
                id = owner.Id,
                lastRecert = owner.LastRecertified,
                lastRecertifierId = owner.LastRecertifierId,
                lastRecertifierDn = owner.LastRecertifierDn,
                nextRecertDate = owner.NextRecertDate
            };
            await apiConnection.SendQueryAsync<ReturnId>(OwnerQueries.setOwnerLastRecert, ownerVariables);
        }

        private async Task<bool> RecertifySingleRuleFromOwner(Rule rule, FwoOwner? owner, string? comment)
        {
            var variables = new
            {
                ruleMetadataId = rule.Metadata.Id,
                ruleId = rule.Id,
                ownerId = owner?.Id ?? 0,
                nextRecertDate = owner?.NextRecertDate,
                userDn = userConfig.User.Dn,
                recertified = true,
                recertDate = DateTime.Now,
                comment = comment,
                ownerRecertId = owner?.LastRecertId
            };
            return (await apiConnection.SendQueryAsync<ReturnIdWrapper>(RecertQueries.recertifyRuleDirectly, variables)).ReturnIds?[0].NewIdLong > 0;
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
