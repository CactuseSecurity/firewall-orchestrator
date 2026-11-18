using FWO.Logging;

namespace FWO.Api.Client.Queries
{
    public class RuleQueries : Queries
    {
        public static readonly string ruleOverviewFragments;
        public static readonly string ruleOverviewForChangeReportFragments;
        public static readonly string ruleDetailsFragments;
        public static readonly string ruleDetailsForReportFragments;
        public static readonly string ruleDetailsForAppRuleReportFragments;
        public static readonly string ruleDetailsForChangeReportFragments;
        public static readonly string natRuleOverviewFragments;
        public static readonly string natRuleDetailsFragments;
        public static readonly string natRuleDetailsForReportFragments;
        public static readonly string natRuleOverviewForChangeReportFragments;
        public static readonly string natRuleDetailsForChangeReportFragments;

        public static readonly string getRuleOverview;
        public static readonly string getRuleDetails;
        // public static readonly string getRuleDetailsForReport;
        public static readonly string getRuleByUid;
        public static readonly string getRuleLayers;
        public static readonly string getSectionLinks;
        public static readonly string getRuleEnforcingGateways;
        public static readonly string getRuleNetworkObjectDetails;
        public static readonly string getRuleIdsOfImport;
        public static readonly string getRuleUidsOfDevice;
        public static readonly string getRuleUidsOfRulebase;
        public static readonly string getRulesByManagement;
        public static readonly string getModelledRulesByManagementName;
        public static readonly string getModelledRulesByManagementComment;
        public static readonly string getNatRuleOverview;
        public static readonly string getNatRuleDetails;
        // public static readonly string getNatRuleDetailsForReport;

        public static readonly string countRules;
        public static readonly string countActiveRules;
        public static readonly string countActiveRulesForOwnerMapping;
        public static readonly string getActiveRulesByUids;
        public static readonly string getRulesWithCurrentViolationsByChunk;
        public static readonly string getRulesForSelectedManagements;
        public static readonly string getRulesForOwnerMappingCustomField;
        public static readonly string getRulesForOwnerMappingNameField;
        public static readonly string getRulesForOwnerMappingIpBased;
        public static readonly string getChangedRulesForRuleOwnerMappingCustomField;
        public static readonly string getChangedRulesForRuleOwnerMappingNameField;
        public static readonly string getChangedRulesForRuleOwnerMappingIpBased;
        public static readonly string getRulesForRuleOwnerCustomField;
        public static readonly string getRulesForRuleOwnerNameField;
        public static readonly string getRulesForRuleOwnerIpBased;
        public static readonly string getRuleDetailsById;
        public static readonly string getTimeBasedRulesByOwner;
        public static readonly string getRuleIdsByRuleOwner;
        public static readonly string getRuleIdsByRuleUid;
        public static readonly string getActiveRulesByOwner;
        public static readonly string getModelledRulesByRuleOwnerNameField;
        public static readonly string getNameFieldRuleOwnerPreFilterCompletenessRules;
        public static readonly string getRuleLastHits;

        /// <summary>Fragments of the rule overview in change reports: rules and objects before and after the change.</summary>
        private static string BuildRuleOverviewForChangeReportFragments()
        {
            return
                GetQueryText("networkObject/fragments/networkObjectOverview.graphql") +
                GetQueryText("networkService/fragments/networkServiceOverview.graphql") +
                GetQueryText("user/fragments/userOverview.graphql") +
                GetQueryText("rule/fragments/ruleOverviewChangesOld.graphql") +
                GetQueryText("rule/fragments/ruleOverviewChangesNew.graphql") +
                GetQueryText("networkObject/fragments/networkObjectDetailsChangesOld.graphql") +
                GetQueryText("networkObject/fragments/networkObjectDetailsChangesNew.graphql") +
                GetQueryText("networkService/fragments/networkServiceDetailsChangesOld.graphql") +
                GetQueryText("networkService/fragments/networkServiceDetailsChangesNew.graphql") +
                GetQueryText("user/fragments/userDetailsChangesOld.graphql") +
                GetQueryText("user/fragments/userDetailsChangesNew.graphql");
        }

        /// <summary>Fragments of the rule details in change reports: rules and objects before and after the change.</summary>
        private static string BuildRuleDetailsForChangeReportFragments()
        {
            return
                GetQueryText("networkObject/fragments/networkObjectOverview.graphql") +
                GetQueryText("networkObject/fragments/networkObjectDetailsChangesOld.graphql") +
                GetQueryText("networkObject/fragments/networkObjectDetailsChangesNew.graphql") +
                GetQueryText("networkService/fragments/networkServiceDetailsChangesOld.graphql") +
                GetQueryText("networkService/fragments/networkServiceDetailsChangesNew.graphql") +
                GetQueryText("user/fragments/userDetailsChangesOld.graphql") +
                GetQueryText("user/fragments/userDetailsChangesNew.graphql") +
                GetQueryText("rule/fragments/ruleDetailsChangesOld.graphql") +
                GetQueryText("rule/fragments/ruleDetailsChangesNew.graphql");
        }

        /// <summary>Fragments of the rule details in reports, with the gateways a rule takes effect on.</summary>
        private static string BuildRuleDetailsForReportFragments()
        {
            return
                ObjectQueries.networkObjectDetailsFragment +
                ObjectQueries.networkServiceDetailsFragment +
                ObjectQueries.userDetailsFragment +
                GetQueryText("rule/fragments/ruleEnforcedOnGateways.graphql") +
                GetQueryText("rule/fragments/ruleDetailsForReport.graphql");
        }

        /// <summary>Fragments of the rule details in app rule reports.</summary>
        private static string BuildRuleDetailsForAppRuleReportFragments()
        {
            return
                GetQueryText("networkObject/fragments/networkObjectDetailsForAppRules.graphql") +
                GetQueryText("networkService/fragments/networkServiceOverview.graphql") +
                GetQueryText("user/fragments/userOverview.graphql") +
                GetQueryText("rule/fragments/ruleDetailsForAppRuleReport.graphql");
        }

        static RuleQueries()
        {
            try
            {
                ruleOverviewFragments =
                    GetQueryText("networkObject/fragments/networkObjectOverview.graphql") +
                    GetQueryText("networkService/fragments/networkServiceOverview.graphql") +
                    GetQueryText("user/fragments/userOverview.graphql") +
                    GetQueryText("rule/fragments/ruleEnforcedOnGateways.graphql") +
                    GetQueryText("rule/fragments/ruleOverview.graphql");
                ruleOverviewForChangeReportFragments = BuildRuleOverviewForChangeReportFragments();
                ruleDetailsFragments =
                ObjectQueries.networkObjectDetailsFragment +
                ObjectQueries.networkServiceDetailsFragment +
                ObjectQueries.userDetailsFragment +
                GetQueryText("rule/fragments/ruleDetails.graphql");
                ruleDetailsForReportFragments = BuildRuleDetailsForReportFragments();
                ruleDetailsForAppRuleReportFragments = BuildRuleDetailsForAppRuleReportFragments();
                natRuleOverviewFragments = ruleOverviewFragments + GetQueryText("rule/fragments/natRuleOverview.graphql");
                natRuleDetailsFragments = ruleDetailsFragments + GetQueryText("rule/fragments/natRuleDetails.graphql");
                natRuleDetailsForReportFragments =
                    ObjectQueries.networkObjectDetailsFragment +
                    ObjectQueries.networkServiceDetailsFragment +
                    ObjectQueries.userDetailsFragment +
                    GetQueryText("rule/fragments/natRuleDetailsForReport.graphql");
                ruleDetailsForChangeReportFragments = BuildRuleDetailsForChangeReportFragments();
                natRuleOverviewForChangeReportFragments = ruleOverviewForChangeReportFragments +
                    GetQueryText("rule/fragments/natRuleOverviewChangesOld.graphql") +
                    GetQueryText("rule/fragments/natRuleOverviewChangesNew.graphql");
                natRuleDetailsForChangeReportFragments = ruleDetailsForChangeReportFragments +
                    GetQueryText("rule/fragments/natRuleDetailsChangesOld.graphql") +
                    GetQueryText("rule/fragments/natRuleDetailsChangesNew.graphql");

                getRuleOverview = ruleOverviewFragments + GetQueryText("rule/getRuleOverview.graphql");
                getRuleDetails = ruleDetailsFragments + GetQueryText("rule/getRuleDetails.graphql");
                getRuleByUid = GetQueryText("rule/getRuleByUid.graphql");
                getRuleLayers = GetQueryText("rule/getRuleLayers.graphql");
                getSectionLinks = GetQueryText("rule/getSectionLinks.graphql");
                getRuleEnforcingGateways = GetQueryText("rule/getRuleEnforcingGateways.graphql");
                getRuleNetworkObjectDetails = ObjectQueries.networkObjectDetailsFragment;
                getRuleIdsOfImport = GetQueryText("rule/getRuleIdsOfImport.graphql");
                getRuleUidsOfDevice = GetQueryText("rule/getRuleUidsOfDevice.graphql");
                getRuleUidsOfRulebase = GetQueryText("rule/getRuleUidsOfRulebase.graphql");
                getRulesByManagement = ruleDetailsFragments + GetQueryText("rule/fragments/ruleEnforcedOnGateways.graphql") + GetQueryText("rule/getRulesByManagement.graphql");
                getModelledRulesByManagementName = ruleDetailsForReportFragments + GetQueryText("report/getModelledRulesByManagementName.graphql");
                getModelledRulesByManagementComment = ruleDetailsForReportFragments + GetQueryText("report/getModelledRulesByManagementComment.graphql");
                getNatRuleOverview = natRuleOverviewFragments + GetQueryText("rule/getNatRuleOverview.graphql");
                getNatRuleDetails = natRuleDetailsFragments + GetQueryText("rule/getNatRuleDetails.graphql");
                getActiveRulesByUids = ruleDetailsFragments + GetQueryText("rule/getActiveRulesByUids.graphql");
                getRulesWithCurrentViolationsByChunk = ruleDetailsFragments + GetQueryText("rule/getRulesWithCurrentViolationsByChunk.graphql");
                getRulesForSelectedManagements = ruleDetailsFragments + GetQueryText("rule/getRulesForSelectedManagements.graphql");
                countRules = GetQueryText("rule/countRules.graphql");
                countActiveRules = GetQueryText("rule/countActiveRules.graphql");
                countActiveRulesForOwnerMapping = GetQueryText("rule/countActiveRulesForOwnerMapping.graphql");
                getRulesForOwnerMappingCustomField = GetQueryText("rule/getRulesForOwnerMappingCustomField.graphql");
                getRulesForOwnerMappingNameField = GetQueryText("rule/getRulesForOwnerMappingNameField.graphql");
                getRulesForOwnerMappingIpBased = GetQueryText("rule/getRulesForOwnerMappingIpBased.graphql");
                getChangedRulesForRuleOwnerMappingCustomField = GetQueryText("rule/getChangedRulesForRuleOwnerMappingCustomField.graphql");
                getChangedRulesForRuleOwnerMappingNameField = GetQueryText("rule/getChangedRulesForRuleOwnerMappingNameField.graphql");
                getChangedRulesForRuleOwnerMappingIpBased = GetQueryText("rule/getChangedRulesForRuleOwnerMappingIpBased.graphql");
                getRulesForRuleOwnerCustomField = GetQueryText("rule/getRulesForRuleOwnerCustomField.graphql");
                getRulesForRuleOwnerNameField = GetQueryText("rule/getRulesForRuleOwnerNameField.graphql");
                getRulesForRuleOwnerIpBased = GetQueryText("rule/getRulesForRuleOwnerIpBased.graphql");
                getRuleDetailsById = GetQueryText("rule/getRuleDetailByID.graphql");
                getTimeBasedRulesByOwner = GetQueryText("rule/getTimeBasedRulesByOwner.graphql");
                getRuleIdsByRuleOwner = GetQueryText("rule/getRuleIdsByRuleOwner.graphql");
                getRuleIdsByRuleUid = GetQueryText("rule/getRuleIdsByRuleUid.graphql");
                getActiveRulesByOwner = GetQueryText("rule/getActiveRulesByOwner.graphql");
                getModelledRulesByRuleOwnerNameField = ruleDetailsForReportFragments + GetQueryText("report/getModelledRulesByRuleOwnerNameField.graphql");
                getNameFieldRuleOwnerPreFilterCompletenessRules = GetQueryText("report/getNameFieldRuleOwnerPreFilterCompletenessRules.graphql");
                getRuleLastHits = GetQueryText("rule_metadata/getRuleLastHits.graphql");
            }
            catch (Exception exception)
            {
                Log.WriteError("Initialize Api Queries", "Api Rule Queries could not be loaded.", exception);
#if RELEASE
                Environment.Exit(-1);
#else
                throw;
#endif
            }
        }
    }
}
