using FWO.Logging;

namespace FWO.Api.Client.Queries
{
    public class RuleQueries : Queries
    {
        public static readonly string ruleOverviewFragments, tenantRuleOverviewFragments;
        public static readonly string ruleDetailsFragments, tenantRuleDetailsFragments;
        public static readonly string ruleDetailsForReportFragments, tenantRuleDetailsForReportFragments;
        public static readonly string getRuleOverview, getTenantRuleOverview;
        public static readonly string getRuleDetails, getTenantRuleDetails;
        public static readonly string getRuleDetailsForReport, getTenantRuleDetailsForReport;
        public static readonly string getRuleByUid, getTenantRuleByUid;
        public static readonly string getRuleNetworkObjectDetails;
        public static readonly string getRuleIdsOfImport;

        public static readonly string natRuleOverviewFragments, tenantNatRuleOverviewFragments;
        public static readonly string natRuleDetailsFragments, tenantNatRuleDetailsFragments;
        public static readonly string natRuleDetailsForReportFragments, tenantNatRuleDetailsForReportFragments;
        public static readonly string getNatRuleOverview, getTenantNatRuleOverview;
        public static readonly string getNatRuleDetails, getTenantNatRuleDetails;
        public static readonly string getNatRuleDetailsForReport, getTenantNatRuleDetailsForReport;

    
        static RuleQueries()
        {
            try
            {
                ruleOverviewFragments =
                    File.ReadAllText(QueryPath + "networkObject/fragments/networkObjectOverview.graphql") +
                    File.ReadAllText(QueryPath + "networkService/fragments/networkServiceOverview.graphql") +
                    File.ReadAllText(QueryPath + "user/fragments/userOverview.graphql") +
                    File.ReadAllText(QueryPath + "rule/fragments/ruleOverview.graphql");

                getRuleOverview = ruleOverviewFragments + File.ReadAllText(QueryPath + "rule/getRuleOverview.graphql");

                ruleDetailsFragments =
                    ObjectQueries.networkObjectDetailsFragment +
                    ObjectQueries.networkServiceObjectDetailsFragment +
                    ObjectQueries.userDetailsFragment +
                    File.ReadAllText(QueryPath + "rule/fragments/ruleDetails.graphql");

                ruleDetailsForReportFragments =
                    ObjectQueries.networkObjectDetailsFragment +
                    ObjectQueries.networkServiceObjectDetailsFragment +
                    ObjectQueries.userDetailsFragment +
                    File.ReadAllText(QueryPath + "rule/fragments/ruleDetailsForReport.graphql");

                getRuleDetails =
                    ruleDetailsFragments +
                    File.ReadAllText(QueryPath + "rule/getRuleDetails.graphql");

                getRuleDetailsForReport =
                    ruleDetailsForReportFragments +
                    File.ReadAllText(QueryPath + "rule/getRuleDetails.graphql");

                getRuleByUid = File.ReadAllText(QueryPath + "rule/getRuleByUid.graphql");

                getRuleNetworkObjectDetails =
                    ObjectQueries.networkObjectDetailsFragment;

                getRuleIdsOfImport =
                    File.ReadAllText(QueryPath + "report/getRuleIdsOfImport.graphql");

                natRuleOverviewFragments = ruleOverviewFragments +
                    File.ReadAllText(QueryPath + "rule/fragments/natRuleOverview.graphql");

                getNatRuleOverview = natRuleOverviewFragments + File.ReadAllText(QueryPath + "rule/getNatRuleOverview.graphql");

                natRuleDetailsFragments =
                    ruleDetailsFragments +
                    File.ReadAllText(QueryPath + "rule/fragments/natRuleDetails.graphql");

                natRuleDetailsForReportFragments =
                    ObjectQueries.networkObjectDetailsFragment +
                    ObjectQueries.networkServiceObjectDetailsFragment +
                    ObjectQueries.userDetailsFragment +
                    File.ReadAllText(QueryPath + "rule/fragments/natRuleDetailsForReport.graphql");

                getNatRuleDetails =
                    natRuleDetailsFragments +
                    File.ReadAllText(QueryPath + "rule/getNatRuleDetails.graphql");

                getNatRuleDetailsForReport =
                    natRuleDetailsForReportFragments +
                    File.ReadAllText(QueryPath + "rule/getNatRuleDetails.graphql");

                tenantRuleOverviewFragments =
                    File.ReadAllText(QueryPath + "networkObject/fragments/networkObjectOverview.graphql") +
                    File.ReadAllText(QueryPath + "networkService/fragments/networkServiceOverview.graphql") +
                    File.ReadAllText(QueryPath + "user/fragments/userOverview.graphql") +
                    File.ReadAllText(QueryPath + "rule/fragments/tenantRuleOverview.graphql");
                
                getTenantRuleOverview = tenantRuleOverviewFragments + File.ReadAllText(QueryPath + "rule/getTenantRuleOverview.graphql");

                tenantRuleDetailsFragments =
                    ObjectQueries.networkObjectDetailsFragment +
                    ObjectQueries.networkServiceObjectDetailsFragment +
                    ObjectQueries.userDetailsFragment +
                    File.ReadAllText(QueryPath + "rule/fragments/tenantRuleDetails.graphql");

                tenantRuleDetailsForReportFragments =
                    ObjectQueries.networkObjectDetailsFragment +
                    ObjectQueries.networkServiceObjectDetailsFragment +
                    ObjectQueries.userDetailsFragment +
                    File.ReadAllText(QueryPath + "rule/fragments/tenantRuleDetailsForReport.graphql");

                getTenantRuleDetails =
                    tenantRuleDetailsFragments +
                    File.ReadAllText(QueryPath + "rule/getTenantRuleDetails.graphql");

                getTenantRuleDetailsForReport =
                    tenantRuleDetailsForReportFragments +
                    File.ReadAllText(QueryPath + "rule/getTenantRuleDetails.graphql");

                getTenantRuleByUid = File.ReadAllText(QueryPath + "rule/getTenantRuleByUid.graphql");

                tenantNatRuleOverviewFragments = tenantRuleOverviewFragments +
                    File.ReadAllText(QueryPath + "rule/fragments/tenantNatRuleOverview.graphql");

                getTenantNatRuleOverview = tenantNatRuleOverviewFragments + File.ReadAllText(QueryPath + "rule/getTenantNatRuleOverview.graphql");

                tenantNatRuleDetailsFragments =
                    tenantRuleDetailsFragments +
                    File.ReadAllText(QueryPath + "rule/fragments/tenantNatRuleDetails.graphql");

                tenantNatRuleDetailsForReportFragments =
                    ObjectQueries.networkObjectDetailsFragment +
                    ObjectQueries.networkServiceObjectDetailsFragment +
                    ObjectQueries.userDetailsFragment +
                    File.ReadAllText(QueryPath + "rule/fragments/tenantNatRuleDetailsForReport.graphql");

                getTenantNatRuleDetails =
                    tenantNatRuleDetailsFragments +
                    File.ReadAllText(QueryPath + "rule/getTenantNatRuleDetails.graphql");

                getTenantNatRuleDetailsForReport =
                    tenantNatRuleDetailsForReportFragments +
                    File.ReadAllText(QueryPath + "rule/getTenantNatRuleDetails.graphql");

            }
            catch (Exception exception)
            {
                Log.WriteError("Initialize Api Queries", "Api Rule Queries could not be loaded.", exception);
                Environment.Exit(-1);
            }
        }
    }
}
