using FWO.Basics;
using FWO.Data;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;

namespace FWO.Services.Triviality
{
    /// <summary>
    /// Builds a reusable lookup for detecting reverse-direction duplicate rules within one specific rule list.
    /// Create a new instance whenever the caller receives a new list snapshot.
    /// </summary>
    public sealed class RuleBidirectionalDuplicateIndex
    {
        private readonly Dictionary<RuleDirectionSignature, List<string>> _ruleKeysBySignature = [];

        public RuleBidirectionalDuplicateIndex(IEnumerable<Rule> rules)
        {
            foreach (Rule rule in rules.Where(IsRelevantRule))
            {
                RuleDirectionSignature signature = CreateSignature(
                    rule.Froms.Select(source => source.Object),
                    rule.Tos.Select(destination => destination.Object),
                    rule.Services.Select(service => service.Content),
                    reverseServices: false);
                if (!_ruleKeysBySignature.TryGetValue(signature, out List<string>? ruleKeys))
                {
                    ruleKeys = [];
                    _ruleKeysBySignature[signature] = ruleKeys;
                }

                ruleKeys.Add(CreateRuleKey(rule));
            }
        }

        public bool HasReverseDuplicate(Rule rule)
        {
            if (!IsRelevantRule(rule))
            {
                return false;
            }

            RuleDirectionSignature reverseSignature = CreateSignature(
                rule.Tos.Select(destination => destination.Object),
                rule.Froms.Select(source => source.Object),
                rule.Services.Select(service => service.Content),
                reverseServices: true);
            if (!_ruleKeysBySignature.TryGetValue(reverseSignature, out List<string>? matchingRuleKeys))
            {
                return false;
            }

            //check if duplicate other than itself exists
            string currentRuleKey = CreateRuleKey(rule);
            return matchingRuleKeys.Any(ruleKey => !string.Equals(ruleKey, currentRuleKey, StringComparison.Ordinal));
        }

        private static bool IsRelevantRule(Rule rule)
        {
            return rule is { Disabled: false, Action: RuleActions.Accept };
        }

        private static RuleDirectionSignature CreateSignature(IEnumerable<NetworkObject> from, IEnumerable<NetworkObject> to, IEnumerable<NetworkService> services, bool reverseServices)
        {
            return new(
                CreateNetworkObjectSetSignature(from),
                CreateNetworkObjectSetSignature(to),
                CreateServiceSetSignature(services, reverseServices));
        }

        private static ImmutableArray<NetworkObjectSignature> CreateNetworkObjectSetSignature(IEnumerable<NetworkObject> objects)
        {
            return [..
                RuleTrivialityNormalization.FlattenRuleNetworkObjects(objects)
                    .Select(obj => new NetworkObjectSignature(obj.IP ?? "", obj.IpEnd ?? ""))
                    .Distinct()
                    .OrderBy(signature => signature.StartIp, StringComparer.Ordinal)
                    .ThenBy(signature => signature.EndIp, StringComparer.Ordinal)];
        }

        private static ImmutableArray<ServiceSignature> CreateServiceSetSignature(IEnumerable<NetworkService> services, bool reverseServices)
        {
            return [..
                RuleTrivialityNormalization.FlattenRuleServices(services)
                    .Select(service => CreateServiceSignature(service, reverseServices))
                    .Distinct()
                    .OrderBy(signature => signature.ProtocolId ?? -1)
                    .ThenBy(signature => signature.SourcePortStart)
                    .ThenBy(signature => signature.SourcePortEnd)
                    .ThenBy(signature => signature.DestinationPortStart)
                    .ThenBy(signature => signature.DestinationPortEnd)];
        }

        private static ServiceSignature CreateServiceSignature(NetworkService service, bool reverseServices)
        {
            int? protocolId = service.Protocol?.Id ?? service.ProtoId;
            int sourcePortStart = reverseServices ? service.DestinationPort ?? 0 : service.SourcePort ?? 0;
            int sourcePortEnd = reverseServices ? service.DestinationPortEnd ?? service.DestinationPort ?? 0 : service.SourcePortEnd ?? service.SourcePort ?? 0;
            int destinationPortStart = reverseServices ? service.SourcePort ?? 0 : service.DestinationPort ?? 0;
            int destinationPortEnd = reverseServices ? service.SourcePortEnd ?? service.SourcePort ?? 0 : service.DestinationPortEnd ?? service.DestinationPort ?? 0;

            if (protocolId.HasValue)
            {
                return new(protocolId.Value, sourcePortStart, sourcePortEnd, destinationPortStart, destinationPortEnd);
            }

            return new(null, sourcePortStart, sourcePortEnd, destinationPortStart, destinationPortEnd);
        }
        private static string CreateRuleKey(Rule rule)
        {
            if (!string.IsNullOrWhiteSpace(rule.Uid))
            {
                return $"uid:{rule.Uid}";
            }

            if (rule.Id > 0)
            {
                return $"id:{rule.Id}";
            }

            return $"ref:{RuntimeHelpers.GetHashCode(rule)}";
        }
    }
    
    internal readonly record struct NetworkObjectSignature(string StartIp, string EndIp);

    internal readonly record struct ServiceSignature(int? ProtocolId, int SourcePortStart, int SourcePortEnd, int DestinationPortStart, int DestinationPortEnd);

    internal sealed class RuleDirectionSignature : IEquatable<RuleDirectionSignature>
    {
        public ImmutableArray<NetworkObjectSignature> Sources { get; }
        public ImmutableArray<NetworkObjectSignature> Destinations { get; }
        public ImmutableArray<ServiceSignature> Services { get; }

        public RuleDirectionSignature(ImmutableArray<NetworkObjectSignature> sources, ImmutableArray<NetworkObjectSignature> destinations, ImmutableArray<ServiceSignature> services)
        {
            Sources = sources;
            Destinations = destinations;
            Services = services;
        }

        public bool Equals(RuleDirectionSignature? other)
        {
            return other != null
                   && Sources.SequenceEqual(other.Sources)
                   && Destinations.SequenceEqual(other.Destinations)
                   && Services.SequenceEqual(other.Services);
        }

        public override bool Equals(object? obj)
        {
            return obj is RuleDirectionSignature other && Equals(other);
        }

        public override int GetHashCode()
        {
            HashCode hash = new();

            foreach (NetworkObjectSignature source in Sources)
            {
                hash.Add(source);
            }

            hash.Add(1);

            foreach (NetworkObjectSignature destination in Destinations)
            {
                hash.Add(destination);
            }

            hash.Add(2);

            foreach (ServiceSignature service in Services)
            {
                hash.Add(service);
            }

            return hash.ToHashCode();
        }
    }
}
