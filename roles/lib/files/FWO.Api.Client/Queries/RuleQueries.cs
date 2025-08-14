using FWO.Logging;

namespace FWO.Api.Client.Queries
{
    public class RuleQueries : Queries
    {
        public static readonly string ruleOverviewFragments;
        public static readonly string ruleOverviewForChangeReportFragments;
        public static readonly string ruleDetailsFragments;
        public static readonly string ruleDetailsForReportFragments;
        public static readonly string ruleDetailsForChangeReportFragments;
        public static readonly string natRuleOverviewFragments;
        public static readonly string natRuleDetailsFragments;
        public static readonly string natRuleDetailsForReportFragments;

        public static readonly string getRuleOverview;
        public static readonly string getRuleDetails;
        // public static readonly string getRuleDetailsForReport;
        public static readonly string getRuleByUid;
        public static readonly string getRuleNetworkObjectDetails;
        public static readonly string getRuleIdsOfImport;
        public static readonly string getRuleUidsOfDevice;
        public static readonly string getRulesByManagement;
        public static readonly string getModelledRulesByManagementName;
        public static readonly string getModelledRulesByManagementComment;
        public static readonly string getNatRuleOverview;
        public static readonly string getNatRuleDetails;
        // public static readonly string getNatRuleDetailsForReport;

        public static readonly string countRules;
        public static readonly string getRulesChunk;

    
        static RuleQueries()
        {
            try
            {
                ruleOverviewFragments =
                    GetQueryText("networkObject/fragments/networkObjectOverview.graphql") +
                    GetQueryText("networkService/fragments/networkServiceOverview.graphql") +
                    GetQueryText("user/fragments/userOverview.graphql") +
                    GetQueryText("rule/fragments/ruleOverview.graphql");
                ruleOverviewForChangeReportFragments =
                    GetQueryText("networkObject/fragments/networkObjectOverview.graphql") +
                    GetQueryText("networkService/fragments/networkServiceOverview.graphql") +
                    GetQueryText("user/fragments/userOverview.graphql") +
                    GetQueryText("rule/fragments/ruleOverviewChangesOld.graphql") +
                    GetQueryText("rule/fragments/ruleOverviewChangesNew.graphql");
                ruleDetailsFragments =
                    ObjectQueries.networkObjectDetailsFragment +
                    ObjectQueries.networkServiceDetailsFragment +
                    ObjectQueries.userDetailsFragment +
                    GetQueryText("rule/fragments/ruleDetails.graphql");
                ruleDetailsForReportFragments =
                    ObjectQueries.networkObjectDetailsFragment +
                    ObjectQueries.networkServiceDetailsFragment +
                    ObjectQueries.userDetailsFragment +
                    GetQueryText("rule/fragments/ruleDetailsForReport.graphql");
                natRuleOverviewFragments = ruleOverviewFragments + GetQueryText("rule/fragments/natRuleOverview.graphql");
                natRuleDetailsFragments = ruleDetailsFragments + GetQueryText("rule/fragments/natRuleDetails.graphql");
                natRuleDetailsForReportFragments =
                    ObjectQueries.networkObjectDetailsFragment +
                    ObjectQueries.networkServiceDetailsFragment +
                    ObjectQueries.userDetailsFragment +
                    GetQueryText("rule/fragments/natRuleDetailsForReport.graphql");
                ruleDetailsForChangeReportFragments =
                    GetQueryText("networkObject/fragments/networkObjectDetailsChangesOld.graphql") +
                    GetQueryText("networkObject/fragments/networkObjectDetailsChangesNew.graphql") +
                    GetQueryText("networkService/fragments/networkServiceDetailsChangesOld.graphql") +
                    GetQueryText("networkService/fragments/networkServiceDetailsChangesNew.graphql") +
                    GetQueryText("user/fragments/userDetailsChangesOld.graphql") +
                    GetQueryText("user/fragments/userDetailsChangesNew.graphql") +
                    GetQueryText("rule/fragments/ruleDetailsChangesOld.graphql") +
                    GetQueryText("rule/fragments/ruleDetailsChangesNew.graphql");

                getRuleOverview = ruleOverviewFragments + GetQueryText("rule/getRuleOverview.graphql");
                getRuleDetails = ruleDetailsFragments + GetQueryText("rule/getRuleDetails.graphql");
                // getRuleDetailsForReport = ruleDetailsForReportFragments + GetQueryText("rule/getRuleDetails.graphql");
                getRuleByUid = GetQueryText("rule/getRuleByUid.graphql");
                getRuleNetworkObjectDetails = ObjectQueries.networkObjectDetailsFragment;
                getRuleIdsOfImport = GetQueryText("report/getRuleIdsOfImport.graphql");
                getRuleUidsOfDevice = GetQueryText("report/getRuleUidsOfDevice.graphql");
                getRulesByManagement = ruleDetailsFragments + GetQueryText("report/getRulesByManagement.graphql");
                getModelledRulesByManagementName = ruleDetailsFragments + GetQueryText("report/getModelledRulesByManagementName.graphql");
                getModelledRulesByManagementComment = ruleDetailsFragments + GetQueryText("report/getModelledRulesByManagementComment.graphql");
                getNatRuleOverview = natRuleOverviewFragments + GetQueryText("rule/getNatRuleOverview.graphql");
                getNatRuleDetails = natRuleDetailsFragments + GetQueryText("rule/getNatRuleDetails.graphql");
                // getNatRuleDetailsForReport = natRuleDetailsForReportFragments + GetQueryText("rule/getNatRuleDetails.graphql");
                getRulesChunk = ruleDetailsFragments + GetQueryText("rule/getRulesChunk.graphql");
                countRules = GetQueryText("rule/countRules.graphql");
            }
            catch (Exception exception)
            {
                Log.WriteError("Initialize Api Queries", "Api Rule Queries could not be loaded.", exception);
                Environment.Exit(-1);
            }
        }
    }
}
