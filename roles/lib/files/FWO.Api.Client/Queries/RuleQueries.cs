﻿using FWO.Logging;

namespace FWO.Api.Client.Queries
{
    public class RuleQueries : Queries
    {
        public static readonly string ruleOverviewFragments;
        public static readonly string ruleDetailsFragments;
        public static readonly string ruleDetailsForReportFragments;
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

    
        static RuleQueries()
        {
            try
            {
                ruleOverviewFragments =
                    File.ReadAllText(QueryPath + "networkObject/fragments/networkObjectOverview.graphql") +
                    File.ReadAllText(QueryPath + "networkService/fragments/networkServiceOverview.graphql") +
                    File.ReadAllText(QueryPath + "user/fragments/userOverview.graphql") +
                    File.ReadAllText(QueryPath + "rule/fragments/ruleOverview.graphql") +
                    File.ReadAllText(QueryPath + "rule/fragments/rulebaseOverview.graphql");

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
                natRuleOverviewFragments = ruleOverviewFragments + File.ReadAllText(QueryPath + "rule/fragments/natRuleOverview.graphql");
                natRuleDetailsFragments = ruleDetailsFragments + File.ReadAllText(QueryPath + "rule/fragments/natRuleDetails.graphql");
                natRuleDetailsForReportFragments =
                    ObjectQueries.networkObjectDetailsFragment +
                    ObjectQueries.networkServiceObjectDetailsFragment +
                    ObjectQueries.userDetailsFragment +
                    File.ReadAllText(QueryPath + "rule/fragments/natRuleDetailsForReport.graphql");

                getRuleOverview = ruleOverviewFragments + File.ReadAllText(QueryPath + "rule/getRuleOverview.graphql");
                getRuleDetails = ruleDetailsFragments + File.ReadAllText(QueryPath + "rule/getRuleDetails.graphql");
                // getRuleDetailsForReport = ruleDetailsForReportFragments + File.ReadAllText(QueryPath + "rule/getRuleDetails.graphql");
                getRuleByUid = File.ReadAllText(QueryPath + "rule/getRuleByUid.graphql");
                getRuleNetworkObjectDetails = ObjectQueries.networkObjectDetailsFragment;
                getRuleIdsOfImport = File.ReadAllText(QueryPath + "report/getRuleIdsOfImport.graphql");
                getRuleUidsOfDevice = File.ReadAllText(QueryPath + "report/getRuleUidsOfDevice.graphql");
                getRulesByManagement = ruleDetailsFragments + File.ReadAllText(QueryPath + "report/getRulesByManagement.graphql");
                getModelledRulesByManagementName = ruleDetailsFragments + File.ReadAllText(QueryPath + "report/getModelledRulesByManagementName.graphql");
                getModelledRulesByManagementComment = ruleDetailsFragments + File.ReadAllText(QueryPath + "report/getModelledRulesByManagementComment.graphql");
                getNatRuleOverview = natRuleOverviewFragments + File.ReadAllText(QueryPath + "rule/getNatRuleOverview.graphql");
                getNatRuleDetails = natRuleDetailsFragments + File.ReadAllText(QueryPath + "rule/getNatRuleDetails.graphql");
                // getNatRuleDetailsForReport = natRuleDetailsForReportFragments + File.ReadAllText(QueryPath + "rule/getNatRuleDetails.graphql");
            }
            catch (Exception exception)
            {
                Log.WriteError("Initialize Api Queries", "Api Rule Queries could not be loaded.", exception);
                Environment.Exit(-1);
            }
        }
    }
}
