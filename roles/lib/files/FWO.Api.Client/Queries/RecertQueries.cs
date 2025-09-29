using FWO.Logging;

namespace FWO.Api.Client.Queries
{
    public class RecertQueries : Queries
    {
        public static readonly string ruleOverviewFragments;
        public static readonly string ruleOpenRecertFragments;

        public static readonly string prepareNextRecertification;
        public static readonly string recertify;
        public static readonly string recertifyOwner;
        public static readonly string getOpenRecertsForRule;
        public static readonly string getOpenRecerts;
        public static readonly string clearOpenRecerts;
        public static readonly string addRecertEntries;
        public static readonly string refreshViewRuleWithOwner;


        static RecertQueries()
        {
            try
            {
                ruleOverviewFragments =
                    GetQueryText("networkObject/fragments/networkObjectOverview.graphql") +
                    GetQueryText("networkService/fragments/networkServiceOverview.graphql") +
                    GetQueryText("user/fragments/userOverview.graphql") +
                    GetQueryText("rule/fragments/ruleOverview.graphql") +
                    GetQueryText("rule/fragments/rulebaseOverview.graphql");
                ruleOpenRecertFragments = ruleOverviewFragments + GetQueryText("recertification/fragments/ruleOpenCertOverview.graphql");

                prepareNextRecertification = GetQueryText("recertification/prepareNextRecertification.graphql");
                recertify = GetQueryText("recertification/recertify.graphql");
                recertifyOwner = GetQueryText("recertification/recertifyOwner.graphql");
                getOpenRecertsForRule = GetQueryText("recertification/getOpenRecertsForRule.graphql");
                getOpenRecerts = GetQueryText("recertification/getOpenRecerts.graphql");
                clearOpenRecerts = GetQueryText("recertification/clearOpenRecerts.graphql");
                addRecertEntries = GetQueryText("recertification/addRecertEntries.graphql");
                refreshViewRuleWithOwner = GetQueryText("recertification/refreshViewRuleWithOwner.graphql");
            }
            catch (Exception exception)
            {
                Log.WriteError("Initialize Api Queries", "Api Recert Queries could not be loaded.", exception);
                Environment.Exit(-1);
            }
        }
    }
}
