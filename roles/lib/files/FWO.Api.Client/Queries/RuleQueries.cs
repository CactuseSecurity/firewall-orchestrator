﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using FWO.Logging;

namespace FWO.ApiClient.Queries
{
    public class RuleQueries : Queries
    {
        public static readonly string ruleOverviewFragments;
        public static readonly string ruleDetailsFragments;
        public static readonly string ruleDetailsForReportFragments;
        public static readonly string getRuleOverview;
        public static readonly string getRuleDetails;
        public static readonly string getRuleDetailsForReport;
        public static readonly string getRuleNetworkObjectDetails;
        public static readonly string getRuleIdsOfImport;
        public static readonly string updateRuleMetadataRecert;
        public static readonly string updateRuleMetadataDecert;

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

                getRuleNetworkObjectDetails =
                    ObjectQueries.networkObjectDetailsFragment;

                getRuleIdsOfImport =
                    File.ReadAllText(QueryPath + "report/getRuleIdsOfImport.graphql");

                updateRuleMetadataRecert =
                    File.ReadAllText(QueryPath + "rule/updateRuleMetadataRecert.graphql");

                updateRuleMetadataDecert =
                    File.ReadAllText(QueryPath + "rule/updateRuleMetadataDecert.graphql");
            }
            catch (Exception exception)
            {
                Log.WriteError("Initialize Api Queries", "Api Rule Queries could not be loaded.", exception);
                Environment.Exit(-1);
            }
        }
    }
}
