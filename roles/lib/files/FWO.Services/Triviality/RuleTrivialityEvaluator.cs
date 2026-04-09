using FWO.Data;
using FWO.Data.Modelling;
using FWO.Data.Networking;

namespace FWO.Services.Triviality
{
    /// <summary>
    /// Evaluates rule-level triviality criteria without binding to UI or workflow concerns.
    /// </summary>
    public class RuleTrivialityEvaluator
    {
        public const string BroadNetworkObjectReason = "BroadNetworkObject";
        public const string BidirectionalDuplicateReason = "BidirectionalDuplicate";
        public const string Ipv6NotSupportedReason = "IPv6NotSupported";

        private readonly RuleRecognitionOption _ruleRecognitionOption = new();
        private readonly NetworkObjectRangeAnalyzer _rangeAnalyzer = new();
        private readonly NetworkObjectComparer _networkObjectComparer;
        private readonly NetworkServiceComparer _networkServiceComparer;

        public RuleTrivialityEvaluator()
        {
            _networkObjectComparer = new(_ruleRecognitionOption);
            _networkServiceComparer = new(_ruleRecognitionOption);
        }

        /// <summary>
        /// Evaluates whether the rule remains trivial under a minimum IPv4 prefix-length criterion.
        /// </summary>
        public TrivialityCheckResult EvaluateBroadNetworkObjectCriterion(Rule rule, int minPrefixLength)
        {
            List<NetworkObject> ruleObjects =
            [
                .. FlattenRuleNetworkObjects(rule.Froms.Select(source => source.Object)),
                .. FlattenRuleNetworkObjects(rule.Tos.Select(destination => destination.Object))
            ];

            List<NetworkObjectRangeAnalysis> analyses = _rangeAnalyzer.AnalyzeMany(ruleObjects);

            if (analyses.Any(analysis => !analysis.IsSupported))
            {
                return new()
                {
                    IsTrivial = false,
                    Reason = Ipv6NotSupportedReason
                };
            }

            if (analyses.Any(analysis => analysis.PrefixLength < minPrefixLength))
            {
                return new()
                {
                    IsTrivial = false,
                    Reason = BroadNetworkObjectReason
                };
            }

            return new()
            {
                IsTrivial = true
            };
        }

        /// <summary>
        /// Evaluates whether an active accept rule has an identical reverse-direction counterpart.
        /// </summary>
        public TrivialityCheckResult EvaluateBidirectionalDuplicateCriterion(Rule rule, IEnumerable<Rule> candidateRules)
        {
            if (rule.Disabled || rule.Action != FWO.Basics.RuleActions.Accept)
            {
                return new()
                {
                    IsTrivial = true
                };
            }

            List<NetworkObject> sourceObjects = FlattenRuleNetworkObjects(rule.Froms.Select(source => source.Object));
            List<NetworkObject> destinationObjects = FlattenRuleNetworkObjects(rule.Tos.Select(destination => destination.Object));
            List<NetworkService> services = FlattenRuleServices(rule.Services.Select(service => service.Content));

            foreach (Rule candidateRule in candidateRules)
            {
                if (ReferenceEquals(candidateRule, rule)
                    || (rule.Id > 0 && candidateRule.Id == rule.Id)
                    || (!string.IsNullOrEmpty(rule.Uid) && rule.Uid == candidateRule.Uid)
                    || candidateRule.Disabled
                    || candidateRule.Action != FWO.Basics.RuleActions.Accept
                    || candidateRule.MgmtId != rule.MgmtId)
                {
                    continue;
                }

                List<NetworkObject> candidateSources = FlattenRuleNetworkObjects(candidateRule.Froms.Select(source => source.Object));
                List<NetworkObject> candidateDestinations = FlattenRuleNetworkObjects(candidateRule.Tos.Select(destination => destination.Object));
                List<NetworkService> candidateServices = FlattenRuleServices(candidateRule.Services.Select(service => service.Content));

                if (ObjectSetsMatch(sourceObjects, candidateDestinations)
                    && ObjectSetsMatch(destinationObjects, candidateSources)
                    && ServiceSetsMatch(services, candidateServices))
                {
                    return new()
                    {
                        IsTrivial = false,
                        Reason = BidirectionalDuplicateReason
                    };
                }
            }

            return new()
            {
                IsTrivial = true
            };
        }

        private static List<NetworkObject> FlattenRuleNetworkObjects(IEnumerable<NetworkObject> objects)
        {
            return objects
                .SelectMany(obj =>
                    new[] { obj }
                        .Concat(obj.ObjectGroupFlats.Select(groupFlat => groupFlat.Object)))
                .OfType<NetworkObject>()
                .ToList();
        }

        private List<NetworkService> FlattenRuleServices(IEnumerable<NetworkService> services)
        {
            return services
                .SelectMany(service =>
                    service.Type.Name == FWO.Basics.ServiceType.Group && _ruleRecognitionOption.SvcResolveGroup
                        ? service.ServiceGroupFlats.Select(groupFlat => groupFlat.Object)
                        : new[] { service })
                .OfType<NetworkService>()
                .Where(service => service.Type.Name != FWO.Basics.ServiceType.Group)
                .ToList();
        }

        private bool ObjectSetsMatch(List<NetworkObject> left, List<NetworkObject> right)
        {
            return left.Count == right.Count
                && !left.Except(right, _networkObjectComparer).Any()
                && !right.Except(left, _networkObjectComparer).Any();
        }

        private bool ServiceSetsMatch(List<NetworkService> left, List<NetworkService> right)
        {
            return left.Count == right.Count
                && !left.Except(right, _networkServiceComparer).Any()
                && !right.Except(left, _networkServiceComparer).Any();
        }
    }

    /// <summary>
    /// Minimal result model for rule-level triviality evaluation.
    /// </summary>
    public class TrivialityCheckResult
    {
        public bool IsTrivial { get; set; }
        public string Reason { get; set; } = "";
    }
}
