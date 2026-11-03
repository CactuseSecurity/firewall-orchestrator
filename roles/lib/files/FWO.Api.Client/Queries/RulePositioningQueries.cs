using System.Diagnostics.CodeAnalysis;
using System.Runtime.ExceptionServices;
using FWO.Logging;

namespace FWO.Api.Client.Queries;

public class RulePositioningQueries : Queries
{
    public static readonly string getLatestSuccessfulRuleImport;
    public static readonly string getGatewayRulebaseLinksAtImport;
    public static readonly string getRulebasesAtImport;
    public static readonly string getCheckPointLayerGuardsAtImport;

    static RulePositioningQueries()
    {
        try
        {
            getLatestSuccessfulRuleImport = GetQueryText("rulePositioning/getLatestSuccessfulRuleImport.graphql");
            getGatewayRulebaseLinksAtImport = GetQueryText("rulePositioning/getGatewayRulebaseLinksAtImport.graphql");
            getRulebasesAtImport = GetQueryText("rulePositioning/getRulebasesAtImport.graphql");
            getCheckPointLayerGuardsAtImport = GetQueryText("rulePositioning/getCheckPointLayerGuardsAtImport.graphql");
        }
        catch (Exception exception) { FailInitialization(exception); }
    }

    /// <summary>
    /// Reports an unrecoverable query-loader failure and preserves its original stack trace.
    /// </summary>
    [DoesNotReturn, ExcludeFromCodeCoverage]
    private static void FailInitialization(Exception exception)
    {
        Log.WriteError("Initialize RulePositioningQueries", "API rule positioning queries could not be loaded.", exception);
#if RELEASE
        Environment.Exit(-1);
#endif
        ExceptionDispatchInfo.Capture(exception).Throw();
    }
}
