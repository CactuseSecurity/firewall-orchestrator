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
        public const string AddressNotAssessableReason = "AddressNotAssessable";

        private readonly NetworkObjectRangeAnalyzer _rangeAnalyzer = new();

        /// <summary>
        /// Evaluates whether the rule remains trivial under a minimum IPv4 prefix-length criterion.
        /// </summary>
        public TrivialityCheckResult EvaluateMinimumCIDRLengthCriterion(Rule rule, int minPrefixLength)
        {
            List<NetworkObject> ruleObjects =
            [
                .. NetworkObject.FlattenRuleNetworkObjects(rule.Froms.Select(source => source.Object)),
                .. NetworkObject.FlattenRuleNetworkObjects(rule.Tos.Select(destination => destination.Object))
            ];

            List<NetworkObjectRangeAnalysis> analyses = _rangeAnalyzer.AnalyzeMany(ruleObjects);

            // The criterion is evaluated for every object it can assess, so that a prefix length violation is
            // still reported when another object of the rule cannot be assessed at all.
            List<NetworkObjectRangeAnalysis> notAssessableAnalyses = [.. analyses.Where(analysis => !analysis.IsSupported)];
            string? notAssessableReason = notAssessableAnalyses.Count > 0 ? GetNotAssessableReason(notAssessableAnalyses) : null;

            if (analyses.Any(analysis => analysis.IsSupported && analysis.PrefixLength < minPrefixLength))
            {
                return new()
                {
                    IsTrivial = false,
                    Reason = MinimumCIDRLengthReason,
                    NotAssessableReason = notAssessableReason
                };
            }

            if (notAssessableReason != null)
            {
                return new()
                {
                    IsTrivial = false,
                    Reason = notAssessableReason,
                    NotAssessableReason = notAssessableReason
                };
            }

            return new()
            {
                IsTrivial = true
            };
        }

        /// <summary>
        /// Reports whether a reason states that a criterion could not be evaluated instead of describing a violation.
        /// </summary>
        /// <param name="reason">Reason reported by an evaluation.</param>
        public static bool IsNotAssessableReason(string reason)
        {
            return reason == Ipv6NotSupportedReason || reason == AddressNotAssessableReason;
        }

        /// <summary>
        /// Distinguishes objects that carry an unsupported address family from objects without a usable address.
        /// </summary>
        /// <param name="notAssessableAnalyses">Analyses of the objects that could not be assessed.</param>
        private static string GetNotAssessableReason(List<NetworkObjectRangeAnalysis> notAssessableAnalyses)
        {
            return notAssessableAnalyses.All(analysis => analysis.Start == null)
                ? AddressNotAssessableReason
                : Ipv6NotSupportedReason;
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
        /// Evaluates whether the rule directly uses a source object whose name contains the configured token.
        /// </summary>
        public TrivialityCheckResult EvaluateForbidNamesAsSourceCriterion(Rule rule, string objectNameToken)
        {
            bool containsMatchingObject = NetworkObject.FlattenRuleNetworkObjects(rule.Froms.Select(source => source.Object))
                .Any(networkObject => HasNameMatch(networkObject, objectNameToken));

            return containsMatchingObject
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
        /// Evaluates whether the rule directly uses a destination object whose name contains the configured token.
        /// </summary>
        public TrivialityCheckResult EvaluateForbidNamesAsDestinationCriterion(Rule rule, string objectNameToken)
        {
            bool containsMatchingObject = NetworkObject.FlattenRuleNetworkObjects(rule.Tos.Select(destination => destination.Object))
                .Any(networkObject => HasNameMatch(networkObject, objectNameToken));

            return containsMatchingObject
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

        private static bool HasNameMatch(NetworkObject networkObject, string objectNameToken)
        {
            return !string.IsNullOrWhiteSpace(objectNameToken)
                && !string.IsNullOrWhiteSpace(networkObject.Name)
                && networkObject.Name.Contains(objectNameToken, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Minimal result model for rule-level triviality evaluation.
    /// </summary>
    public class TrivialityCheckResult
    {
        public bool IsTrivial { get; set; }
        public string Reason { get; set; } = "";

        /// <summary>
        /// Reason for the objects the criterion could not assess, or null when every object could be assessed.
        /// It is reported in addition to <see cref="Reason"/> when the criterion found a violation as well.
        /// </summary>
        public string? NotAssessableReason { get; set; }
    }
}
