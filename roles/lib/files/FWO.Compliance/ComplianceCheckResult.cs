using FWO.Basics.Enums;
using FWO.Data;

namespace FWO.Compliance
{
    public class ComplianceCheckResult
    {
        public Rule Rule { get; set; }
        public ComplianceViolationType Compliance { get; set; }

        public ComplianceCriterion? Criterion { get; set; }

        public NetworkObject? Source { get; set; }
        public ComplianceNetworkZone? SourceZone { get; set; }
        public NetworkObject? Destination { get; set; }
        public ComplianceNetworkZone? DestinationZone { get; set; }
        public NetworkService? Service { get; set; }
        public AssessabilityIssue? AssessabilityIssue { get; set; }

        public ComplianceCheckResult(Rule rule, ComplianceViolationType compliance = ComplianceViolationType.None)
        {
            Rule = rule;
            Compliance = compliance;
        }
        
    }
    
}