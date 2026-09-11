using FWO.Basics;
using FWO.Basics.Enums;
using FWO.Data;
using FWO.Services.Triviality;
using FWO.Ui.Display;
using NetTools;

namespace FWO.Compliance
{
    /// <summary>
    /// Evaluates the single criteria of a compliance policy for one rule.
    /// </summary>
    public partial class ComplianceCheck
    {
        /// <summary>
        /// Checks whether a rule can be assessed, i.e. contains only evaluable network objects.
        /// </summary>
        /// <param name="rule">Rule that is currently under test.</param>
        /// <param name="resolvedSources">Fully resolved source objects.</param>
        /// <param name="resolvedDestinations">Fully resolved destination objects.</param>
        /// <param name="criterion">Compliance criterion for assessability.</param>
        /// <returns>True if the rule can be assessed, otherwise false.</returns>
        public Task<bool> CheckAssessability(Rule rule, List<NetworkObject> resolvedSources, List<NetworkObject> resolvedDestinations, ComplianceCriterion criterion)
        {
            bool isAssessable = true;

            // If treated as part of internet zone dynamic and domain objects are irrelevant for the assessability check.

            resolvedSources = TryFilterDynamicAndDomainObjects(resolvedSources);
            resolvedDestinations = TryFilterDynamicAndDomainObjects(resolvedDestinations);

            // Check only accept rules for assessability.

            if (rule.Action == RuleActions.Accept)
            {
                foreach (NetworkObject networkObject in resolvedSources.Concat(resolvedDestinations))
                {
                    // Get assessability issue type if existing.

                    AssessabilityIssue? assessabilityIssue = TryGetAssessabilityIssue(networkObject);

                    if (assessabilityIssue != null)
                    {
                        // Create check result object.

                        ComplianceCheckResult complianceCheckResult;

                        if (resolvedSources.Contains(networkObject))
                        {
                            complianceCheckResult = new(rule, ComplianceViolationType.NotAssessable)
                            {
                                Source = networkObject
                            };
                        }
                        else
                        {
                            complianceCheckResult = new(rule, ComplianceViolationType.NotAssessable)
                            {
                                Destination = networkObject
                            };
                        }

                        complianceCheckResult.AssessabilityIssue = assessabilityIssue;
                        complianceCheckResult.Criterion = criterion;

                        // Create violation.

                        CreateViolation(ComplianceViolationType.NotAssessable, rule, complianceCheckResult);
                        isAssessable = false;
                    }
                }
            }

            return Task.FromResult(isAssessable);
        }

        /// <summary>
        /// Evaluates a rule against all configured compliance criteria.
        /// </summary>
        /// <param name="rule">Rule whose compliance should be checked.</param>
        /// <param name="criteria">Set of criteria derived from the policy.</param>
        /// <returns>True if the rule is compliant with every criterion.</returns>
        public async Task<bool> CheckRuleCompliance(Rule rule, IEnumerable<ComplianceCriterion> criteria, RuleBidirectionalDuplicateIndex? duplicateIndex = null)
        {
            bool ruleIsCompliant = true;

            if (rule.Action == RuleActions.Accept)
            {
                // Resolve network locations

                NetworkLocation[] networkLocations = rule.Froms.Concat(rule.Tos).ToArray();
                List<NetworkLocation> resolvedNetworkLocations = RuleDisplayBase.GetResolvedNetworkLocations(networkLocations);

                List<NetworkObject> resolvedSources = RuleDisplayBase
                    .GetResolvedNetworkLocations(rule.Froms)
                    .Select(from => from.Object)
                    .ToList();

                List<NetworkObject> resolvedDestinations = RuleDisplayBase
                    .GetResolvedNetworkLocations(rule.Tos)
                    .Select(to => to.Object)
                    .ToList();

                try
                {
                    foreach (var criterion in criteria)
                    {
                        switch (criterion.CriterionType)
                        {
                            case nameof(CriterionType.Assessability):
                                ruleIsCompliant &= CheckAssessability(rule, resolvedSources, resolvedDestinations, criterion).Result;
                                break;
                            case nameof(CriterionType.Matrix):
                                ruleIsCompliant &= await CheckMatrixCompliance(rule, criterion, resolvedSources, resolvedDestinations);
                                break;
                            case nameof(CriterionType.ForbiddenService):
                                ruleIsCompliant &= CheckForForbiddenService(rule, criterion);
                                break;
                            case nameof(CriterionType.MinimumCIDRLength):
                            case nameof(CriterionType.ForbidZonesAsSource):
                            case nameof(CriterionType.ForbidZonesAsDestination):
                            case nameof(CriterionType.ForbidBidirectionalDuplicate):
                                ruleIsCompliant &= CheckTrivialityCriterion(rule, criterion, duplicateIndex);
                                break;
                            default:
                                break;
                        }
                    }
                }
                catch (System.Exception e)
                {
                    Logger.TryWriteError("Compliance Check", e, true);
                }

            }

            return ruleIsCompliant;
        }

        /// <summary>
        /// Compliance check used in current UI implementation.
        /// </summary>
        /// <param name="sourceIpRange">Source range provided by the UI.</param>
        /// <param name="destinationIpRange">Destination range provided by the UI.</param>
        /// <param name="networkZones">Network zones to test against the provided ranges.</param>
        /// <returns>List of forbidden communications found by the matrix check.</returns>
        public List<(ComplianceNetworkZone, ComplianceNetworkZone)> CheckIpRangeInputCompliance(IPAddressRange? sourceIpRange, IPAddressRange? destinationIpRange, List<ComplianceNetworkZone> networkZones)
        {
            NetworkZones = networkZones;
            List<(ComplianceNetworkZone, ComplianceNetworkZone)> forbiddenCommunicationsOutput = [];

            if (sourceIpRange != null && destinationIpRange != null)
            {
                CheckMatrixCompliance
                (
                    [sourceIpRange],
                    [destinationIpRange],
                    out forbiddenCommunicationsOutput
                );
            }

            return forbiddenCommunicationsOutput;
        }

        /// <summary>
        /// Performs the matrix compliance check for a rule by mapping resolved objects to zones.
        /// </summary>
        /// <param name="rule">Rule under test.</param>
        /// <param name="criterion">Matrix criterion.</param>
        /// <param name="resolvedSources">Resolved source objects.</param>
        /// <param name="resolvedDestinations">Resolved destination objects.</param>
        private async Task<bool> CheckMatrixCompliance(Rule rule, ComplianceCriterion criterion, List<NetworkObject> resolvedSources, List<NetworkObject> resolvedDestinations)
        {
            List<ComplianceNetworkZone> networkZones = GetNetworkZonesForCriterion(criterion);

            Task<List<(NetworkObject networkObject, List<IPAddressRange> ipRanges)>> fromsTask = GetNetworkObjectsWithIpRanges(resolvedSources);
            Task<List<(NetworkObject networkObject, List<IPAddressRange> ipRanges)>> tosTask = GetNetworkObjectsWithIpRanges(resolvedDestinations);

            await Task.WhenAll(fromsTask, tosTask);

            List<(NetworkObject networkObject, List<ComplianceNetworkZone> networkZones)> sourceZones = MapZonesToNetworkObjects(fromsTask.Result, networkZones, out List<NetworkObject> notAssessableSources);
            List<(NetworkObject networkObject, List<ComplianceNetworkZone> networkZones)> destinationZones = MapZonesToNetworkObjects(tosTask.Result, networkZones, out List<NetworkObject> notAssessableDestinations);

            // Objects of an address family that no zone of this criterion covers cannot be compared against the
            // matrix at all. They are reported as not assessable, while every object that could be assigned to a
            // zone is still checked below.
            bool ruleIsCompliant = CreateZoneAssessabilityViolations(rule, criterion, notAssessableSources, notAssessableDestinations);

            Dictionary<ComplianceNetworkZone, List<NetworkObject>> sourceObjectsByZone = MapObjectsByZone(sourceZones);
            Dictionary<ComplianceNetworkZone, List<NetworkObject>> destinationObjectsByZone = MapObjectsByZone(destinationZones);

            foreach ((ComplianceNetworkZone sourceZone, List<NetworkObject> sourceObjects) in sourceObjectsByZone)
            {
                foreach ((ComplianceNetworkZone destinationZone, List<NetworkObject> destinationObjects) in destinationObjectsByZone)
                {
                    if (!sourceZone.CommunicationAllowedTo(destinationZone))
                    {
                        ruleIsCompliant = false;
                        string sourceObjectsString = string.Join(", ", sourceObjects.Select(GetNwObjectString).Distinct());
                        string destinationObjectsString = string.Join(", ", destinationObjects.Select(GetNwObjectString).Distinct());

                        string details = $"{_userConfig.GetText("H5839")}: {sourceZone.Name} ({sourceObjectsString}) -> {destinationZone.Name} ({destinationObjectsString})";

                        ComplianceCheckResult complianceCheckResult = new(rule, ComplianceViolationType.MatrixViolation)
                        {
                            Criterion = criterion,
                            SourceZone = sourceZone,
                            DestinationZone = destinationZone
                        };

                        CreateViolation(ComplianceViolationType.MatrixViolation, rule, complianceCheckResult, details);
                    }
                }
            }

            return ruleIsCompliant;
        }

        /// <summary>
        /// Executes rule-level criteria that are modeled as triviality checks.
        /// </summary>
        /// <param name="rule">Rule under test.</param>
        /// <param name="criterion">Criterion to execute.</param>
        /// <param name="duplicateIndex">Optional index for reverse-direction duplicate checks.</param>
        private bool CheckTrivialityCriterion(Rule rule, ComplianceCriterion criterion, RuleBidirectionalDuplicateIndex? duplicateIndex)
        {
            TrivialityCheckResult result;
            ComplianceViolationType violationType;

            switch (criterion.CriterionType)
            {
                case nameof(CriterionType.MinimumCIDRLength):
                    if (!int.TryParse(criterion.Content, out int minPrefixLength) || minPrefixLength < 0 || minPrefixLength > 32)
                    {
                        Logger.TryWriteError("Compliance Check", $"Criterion {criterion.Id} ({criterion.Name}) has invalid content '{criterion.Content}' for {criterion.CriterionType}.", true);
                        return true;
                    }

                    result = _ruleTrivialityEvaluator.EvaluateMinimumCIDRLengthCriterion(rule, minPrefixLength);
                    violationType = ComplianceViolationType.MinimumCIDRLengthViolation;
                    break;

                case nameof(CriterionType.ForbidZonesAsSource):
                    if (!TryGetNonEmptyCriterionContent(criterion, out string sourceObjectToken))
                    {
                        return true;
                    }

                    result = _ruleTrivialityEvaluator.EvaluateForbidNamesAsSourceCriterion(rule, sourceObjectToken);
                    violationType = ComplianceViolationType.ZoneObjectSourceViolation;
                    break;

                case nameof(CriterionType.ForbidZonesAsDestination):
                    if (!TryGetNonEmptyCriterionContent(criterion, out string destinationObjectToken))
                    {
                        return true;
                    }

                    result = _ruleTrivialityEvaluator.EvaluateForbidNamesAsDestinationCriterion(rule, destinationObjectToken);
                    violationType = ComplianceViolationType.ZoneObjectDestinationViolation;
                    break;

                case nameof(CriterionType.ForbidBidirectionalDuplicate):
                    if (duplicateIndex == null)
                    {
                        Logger.TryWriteError("Compliance Check", $"Criterion {criterion.Id} ({criterion.Name}) requires a duplicate index, but none was provided.", true);
                        return true;
                    }

                    result = _ruleTrivialityEvaluator.EvaluateForbidBidirectionalDuplicateCriterion(rule, duplicateIndex);
                    violationType = ComplianceViolationType.BidirectionalDuplicateViolation;
                    break;

                default:
                    return true;
            }

            if (result.IsTrivial)
            {
                return true;
            }

            CreateTrivialityViolations(rule, criterion, result, violationType);
            return false;
        }

        /// <summary>
        /// Checks two IP range sets against the network zone matrix.
        /// </summary>
        /// <param name="source">Source ranges.</param>
        /// <param name="destination">Destination ranges.</param>
        /// <param name="forbiddenCommunication">Output list of forbidden zone combinations.</param>
        private bool CheckMatrixCompliance(List<IPAddressRange> source, List<IPAddressRange> destination, out List<(ComplianceNetworkZone, ComplianceNetworkZone)> forbiddenCommunication)
        {
            // Determine all matching source zones
            List<ComplianceNetworkZone> sourceZones = DetermineZones(source);

            // Determine all matching destination zones
            List<ComplianceNetworkZone> destinationZones = DetermineZones(destination);

            forbiddenCommunication = [];

            foreach (ComplianceNetworkZone sourceZone in sourceZones)
            {
                foreach (ComplianceNetworkZone destinationZone in destinationZones.Where(d => !sourceZone.CommunicationAllowedTo(d)))
                {
                    forbiddenCommunication.Add((sourceZone, destinationZone));
                }
            }

            return forbiddenCommunication.Count == 0;
        }

        /// <summary>
        /// Validates whether a rule uses a service forbidden by the given criterion.
        /// </summary>
        /// <param name="rule">Rule that may contain forbidden services.</param>
        /// <param name="criterion">Criterion defining the restricted service set.</param>
        private bool CheckForForbiddenService(Rule rule, ComplianceCriterion criterion)
        {
            bool ruleIsCompliant = true;

            List<string> restrictedServices = [.. criterion.Content.Split(',').Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s))];

            if (restrictedServices.Count > 0)
            {
                foreach (NetworkService service in GetServicesForForbiddenServiceCheck(rule)
                    .Where(service => restrictedServices.Any(restrictedService => MatchesRestrictedService(service, restrictedService))))
                {
                    ComplianceCheckResult complianceCheckResult = new(rule, ComplianceViolationType.ServiceViolation)
                    {
                        Criterion = criterion,
                        Service = service
                    };

                    CreateViolation(ComplianceViolationType.ServiceViolation, rule, complianceCheckResult);
                    ruleIsCompliant = false;
                }
            }

            return ruleIsCompliant;
        }

        /// <summary>
        /// Collects direct and flattened services that should be evaluated for forbidden-service checks.
        /// </summary>
        /// <param name="rule">Rule whose services should be checked.</param>
        private static List<NetworkService> GetServicesForForbiddenServiceCheck(Rule rule)
        {
            Dictionary<ServiceMatchKey, NetworkService> services = [];

            foreach (NetworkService service in rule.Services.Select(wrapper => wrapper.Content))
            {
                AddForbiddenServiceCandidate(services, service);

                foreach (NetworkService flattenedService in service.ServiceGroupFlats
                    .Where(groupFlat => groupFlat.Object != null)
                    .Select(groupFlat => groupFlat.Object!))
                {
                    AddForbiddenServiceCandidate(services, flattenedService);
                }
            }

            return [.. services.Values];
        }

        /// <summary>
        /// Adds a service candidate if it has not already been seen.
        /// </summary>
        /// <param name="services">Lookup of collected services.</param>
        /// <param name="service">Service to add.</param>
        private static void AddForbiddenServiceCandidate(IDictionary<ServiceMatchKey, NetworkService> services, NetworkService service)
        {
            ServiceMatchKey serviceKey = ServiceMatchKey.FromService(service);

            if (!services.ContainsKey(serviceKey))
            {
                services.Add(serviceKey, service);
            }
        }

        /// <summary>
        /// Checks whether a service matches a restricted service entry.
        /// </summary>
        /// <param name="service">Service to evaluate.</param>
        /// <param name="restrictedService">Restricted entry, either a UID or a port/protocol definition.</param>
        private static bool MatchesRestrictedService(NetworkService service, string restrictedService)
        {
            if (TryParseRestrictedServiceDefinition(restrictedService, out int rangeStart, out int rangeEnd, out string protocolToken))
            {
                return MatchesRestrictedServiceDefinition(service, rangeStart, rangeEnd, protocolToken);
            }

            return string.Equals(service.Uid, restrictedService, StringComparison.Ordinal);
        }

        /// <summary>
        /// Parses a restricted service definition in the form port/protocol or start-end/protocol.
        /// </summary>
        /// <param name="restrictedService">Restricted service entry to parse.</param>
        /// <param name="rangeStart">Parsed start port.</param>
        /// <param name="rangeEnd">Parsed end port.</param>
        /// <param name="protocolToken">Parsed protocol token.</param>
        private static bool TryParseRestrictedServiceDefinition(string restrictedService, out int rangeStart, out int rangeEnd, out string protocolToken)
        {
            rangeStart = 0;
            rangeEnd = 0;
            protocolToken = "";

            string[] definitionParts = restrictedService.Split('/', StringSplitOptions.TrimEntries);
            if (definitionParts.Length != 2 || string.IsNullOrWhiteSpace(definitionParts[0]) || string.IsNullOrWhiteSpace(definitionParts[1]))
            {
                return false;
            }

            protocolToken = definitionParts[1];
            string[] portParts = definitionParts[0].Split('-', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            if (portParts.Length == 1
                && int.TryParse(portParts[0], out rangeStart)
                && rangeStart is > 0 and <= GlobalConst.kMaxPortNumber)
            {
                rangeEnd = rangeStart;
                return true;
            }

            if (portParts.Length == 2
                && int.TryParse(portParts[0], out rangeStart)
                && rangeStart is > 0 and <= GlobalConst.kMaxPortNumber
                && int.TryParse(portParts[1], out rangeEnd)
                && rangeEnd is > 0 and <= GlobalConst.kMaxPortNumber
                && rangeStart <= rangeEnd)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Checks whether a service matches a parsed restricted service definition.
        /// </summary>
        /// <param name="service">Service to evaluate.</param>
        /// <param name="rangeStart">Restricted start port.</param>
        /// <param name="rangeEnd">Restricted end port.</param>
        /// <param name="protocolToken">Restricted protocol token.</param>
        private static bool MatchesRestrictedServiceDefinition(NetworkService service, int rangeStart, int rangeEnd, string protocolToken)
        {
            if (IsCanonicalAnyService(service))
            {
                return true;
            }

            if (!ServiceProtocolMatches(service, protocolToken) || service.DestinationPort == null)
            {
                return false;
            }

            int serviceRangeStart = service.DestinationPort.Value;
            int serviceRangeEnd = service.DestinationPortEnd ?? serviceRangeStart;

            return serviceRangeStart <= rangeEnd && serviceRangeEnd >= rangeStart;
        }

        /// <summary>
        /// Determines whether a service represents the imported, protocol-agnostic ANY service.
        /// </summary>
        /// <param name="service">Service to evaluate.</param>
        private static bool IsCanonicalAnyService(NetworkService service)
        {
            return (service.ProtoId ?? service.Protocol?.Id) == GlobalConst.kAnyIpProtocolId
                && service.DestinationPort == null
                && service.DestinationPortEnd == null;
        }

        /// <summary>
        /// Checks whether the service protocol matches the requested protocol token.
        /// </summary>
        /// <param name="service">Service to evaluate.</param>
        /// <param name="protocolToken">Restricted protocol token.</param>
        private static bool ServiceProtocolMatches(NetworkService service, string protocolToken)
        {
            if (string.IsNullOrWhiteSpace(protocolToken))
            {
                return false;
            }

            if (int.TryParse(protocolToken, out int protocolId))
            {
                return service.ProtoId == protocolId || service.Protocol?.Id == protocolId;
            }

            return string.Equals(service.Protocol?.Name, protocolToken, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Returns the trimmed string content for criteria that require a non-empty content field.
        /// </summary>
        /// <param name="criterion">Criterion whose content should be validated.</param>
        /// <param name="content">Validated content.</param>
        private bool TryGetNonEmptyCriterionContent(ComplianceCriterion criterion, out string content)
        {
            content = criterion.Content?.Trim() ?? "";

            if (!string.IsNullOrWhiteSpace(content))
            {
                return true;
            }

            Logger.TryWriteError("Compliance Check", $"Criterion {criterion.Id} ({criterion.Name}) has empty content for {criterion.CriterionType}.", true);
            return false;
        }
    }
}
