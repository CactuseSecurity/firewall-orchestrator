using FWO.Logging;

namespace FWO.Api.Client.Queries
{
    public class RecertQueries : Queries
    {
        public static readonly string ruleOverviewFragments;
        public static readonly string ruleOpenRecertFragments;

        public static readonly string prepareNextRecertification;
        public static readonly string recertify;
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
                    File.ReadAllText(QueryPath + "networkObject/fragments/networkObjectOverview.graphql") +
                    File.ReadAllText(QueryPath + "networkService/fragments/networkServiceOverview.graphql") +
                    File.ReadAllText(QueryPath + "user/fragments/userOverview.graphql") +
                    File.ReadAllText(QueryPath + "rule/fragments/ruleOverview.graphql") +
                    File.ReadAllText(QueryPath + "rule/fragments/rulebaseOverview.graphql");
                ruleOpenRecertFragments = ruleOverviewFragments + File.ReadAllText(QueryPath + "recertification/fragments/ruleOpenCertOverview.graphql");

                prepareNextRecertification = File.ReadAllText(QueryPath + "recertification/prepareNextRecertification.graphql");
                recertify = File.ReadAllText(QueryPath + "recertification/recertify.graphql");
                getOpenRecertsForRule = File.ReadAllText(QueryPath + "recertification/getOpenRecertsForRule.graphql");
                getOpenRecerts = File.ReadAllText(QueryPath + "recertification/getOpenRecerts.graphql");
                clearOpenRecerts = File.ReadAllText(QueryPath + "recertification/clearOpenRecerts.graphql");
                addRecertEntries = File.ReadAllText(QueryPath + "recertification/addRecertEntries.graphql");
                refreshViewRuleWithOwner = File.ReadAllText(QueryPath + "recertification/refreshViewRuleWithOwner.graphql");
            }
            catch (Exception exception)
            {
                Log.WriteError("Initialize Api Queries", "Api Recert Queries could not be loaded.", exception);
                Environment.Exit(-1);
            }
        }
    }
}
