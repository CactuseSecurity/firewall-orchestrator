using FWO.Basics;
using FWO.Data;
using FWO.Data.Networking;

namespace FWO.Services.Triviality
{
    /// <summary>
    /// Evaluates rule-level triviality criteria without binding to UI or workflow concerns.
    /// </summary>
    public class RuleTrivialityEvaluator
    {
        public const string MinimumCIDRLengthReason = nameof(CriterionType.MinimumCIDRLength);
        public const string ForbidBidirectionalDuplicateReason = nameof(CriterionType.ForbidBidirectionalDuplicate);
        public const string ForbidZonesAsDestinationReason = nameof(CriterionType.ForbidZonesAsDestination);
        public const string ForbidZonesAsSourceReason = nameof(CriterionType.ForbidZonesAsSource);
        public const string Ipv6NotSupportedReason = "IPv6NotSupported";

        private readonly NetworkObjectRangeAnalyzer _rangeAnalyzer = new();

        /// <summary>
        /// Evaluates whether the rule remains trivial under a minimum IPv4 prefix-length criterion.
        /// </summary>
        public TrivialityCheckResult EvaluateMinimumCIDRLengthCriterion(Rule rule, int minPrefixLength)
        {
            List<NetworkObject> ruleObjects =
            [
                .. RuleTrivialityNormalization.FlattenRuleNetworkObjects(rule.Froms.Select(source => source.Object)),
                .. RuleTrivialityNormalization.FlattenRuleNetworkObjects(rule.Tos.Select(destination => destination.Object))
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
                    Reason = MinimumCIDRLengthReason
                };
            }

            return new()
            {
                IsTrivial = true
            };
        }

        /// <summary>
        /// Evaluates whether an active accept rule has an identical reverse-direction counterpart in the provided index.
        /// </summary>
        public TrivialityCheckResult EvaluateForbidBidirectionalDuplicateCriterion(Rule rule, RuleBidirectionalDuplicateIndex duplicateIndex)
        {
            if (rule.Disabled || rule.Action != RuleActions.Accept)
            {
                return new()
                {
                    IsTrivial = true
                };
            }

            if (duplicateIndex.HasReverseDuplicate(rule))
            {
                return new()
                {
                    IsTrivial = false,
                    Reason = ForbidBidirectionalDuplicateReason
                };
            }

            return new()
            {
                IsTrivial = true
            };
        }

        /// <summary>
        /// Evaluates whether the rule directly uses a zone object in source.
        /// </summary>
        public TrivialityCheckResult EvaluateForbidZonesAsSourceCriterion(Rule rule)
        {
            bool containsZoneObject = RuleTrivialityNormalization.FlattenRuleNetworkObjects(rule.Froms.Select(source => source.Object)).Any(IsZoneObject);

            return containsZoneObject
                ? new()
                {
                    IsTrivial = false,
                    Reason = ForbidZonesAsSourceReason
                }
                : new()
                {
                    IsTrivial = true
                };
        }

        /// <summary>
        /// Evaluates whether the rule directly uses a zone object in destination.
        /// </summary>
        public TrivialityCheckResult EvaluateForbidZonesAsDestinationCriterion(Rule rule)
        {
            bool containsZoneObject = RuleTrivialityNormalization.FlattenRuleNetworkObjects(rule.Tos.Select(destination => destination.Object)).Any(IsZoneObject);

            return containsZoneObject
                ? new()
                {
                    IsTrivial = false,
                    Reason = ForbidZonesAsDestinationReason
                }
                : new()
                {
                    IsTrivial = true
                };
        }

        private static bool IsZoneObject(NetworkObject networkObject)
        {
            return !string.IsNullOrWhiteSpace(networkObject.Name)
                && networkObject.Name.EndsWith("_ZONE", StringComparison.OrdinalIgnoreCase);
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
