using FWO.Data.Workflow;

namespace FWO.ExternalSystems.FirewallWrite
{
    /// <summary>
    /// Fills the source, destination and service placeholders of a rule step from the elements of an implementation task.
    /// The full lists hold all elements not being deleted. A side without elements is never widened to the vendor's "any":
    /// its full list placeholders get no value, so a template that needs them fails. Allowing any takes an explicit element.
    /// </summary>
    public static class FirewallRuleValues
    {
        private const string kCsvSeparator = ",";
        private const string kTcpAndUdp = "TCP/UDP";
        private const string kSourceSide = "source";
        private const string kDestinationSide = "destination";
        private const string kServiceSide = "service";

        private static readonly List<string> SourceFields = [nameof(ElemFieldType.source), nameof(ElemFieldType.modelled_source)];
        private static readonly List<string> DestinationFields = [nameof(ElemFieldType.destination), nameof(ElemFieldType.modelled_destination)];
        private static readonly List<string> AddActions = [nameof(RequestAction.create), nameof(RequestAction.addAfterCreation)];
        private static readonly HashSet<string> TcpAndUdpProtocols = new(StringComparer.OrdinalIgnoreCase) { "TCP", "UDP" };

        /// <summary>Sets all rule element placeholders of the step.</summary>
        public static void SetRuleElements(FirewallWriteStep step, WfImplTask implTask, FirewallVendorProfile profile)
        {
            List<WfImplElement> sources = [.. implTask.ImplElements.Where(element => SourceFields.Contains(element.Field))];
            List<WfImplElement> destinations = [.. implTask.ImplElements.Where(element => DestinationFields.Contains(element.Field))];
            List<WfImplElement> services = [.. implTask.ImplElements.Where(FirewallElementValues.IsServiceElement)];

            SetNetworkList(step, sources, profile, new(kSourceSide, FirewallWritePlaceholders.RuleSources, FirewallWritePlaceholders.RuleSourcesToAdd,
                FirewallWritePlaceholders.RuleSourcesToRemove, FirewallWritePlaceholders.RuleSourcesNameObjects,
                FirewallWritePlaceholders.RuleSourcesCsv, FirewallWritePlaceholders.RuleSourceAddresses));
            SetNetworkList(step, destinations, profile, new(kDestinationSide, FirewallWritePlaceholders.RuleDestinations,
                FirewallWritePlaceholders.RuleDestinationsToAdd, FirewallWritePlaceholders.RuleDestinationsToRemove,
                FirewallWritePlaceholders.RuleDestinationsNameObjects, FirewallWritePlaceholders.RuleDestinationsCsv,
                FirewallWritePlaceholders.RuleDestinationAddresses));
            SetServiceList(step, services, profile);
        }

        private sealed record NetworkListPlaceholders(string Side, string Full, string ToAdd, string ToRemove, string NameObjects, string Csv,
            string Addresses);

        private static void SetNetworkList(FirewallWriteStep step, List<WfImplElement> elements, FirewallVendorProfile profile,
            NetworkListPlaceholders placeholders)
        {
            List<WfImplElement> current = Current(elements);
            List<string> names = Names(current, profile);
            List<string> addresses = profile.ReferencesObjectsByAddress
                ? [.. current.Select(FirewallElementValues.AddressNotation).Distinct()]
                : [];
            string reason = NoElementReason(placeholders.Side);
            step.SetStringList(placeholders.ToAdd, Names(Added(elements), profile))
                .SetStringList(placeholders.ToRemove, Names(Removed(elements), profile));
            SetListOrMissing(step, placeholders.Full, names, reason);
            SetNameObjectsOrMissing(step, placeholders.NameObjects, names, reason);
            SetCsvOrMissing(step, placeholders.Csv, names, reason);
            SetListOrMissing(step, placeholders.Addresses, addresses, reason);
        }

        private static void SetServiceList(FirewallWriteStep step, List<WfImplElement> elements, FirewallVendorProfile profile)
        {
            List<WfImplElement> current = Current(elements);
            List<string> serviceNames = ServiceNames(current, profile);
            List<string> ports = [.. current.Select(FirewallElementValues.PortRange).Where(range => range.Length > 0).Distinct()];
            List<string> protocols = [.. current.Select(element => FirewallElementValues.ProtocolName(element.ProtoId).ToUpperInvariant()).Distinct()];
            // services without ports (e.g. icmp) match every port of their protocol
            List<string> renderedPorts = current.Count > 0 && ports.Count == 0 ? [profile.AnyServiceName] : ports;
            string reason = NoElementReason(kServiceSide);

            step.SetStringList(FirewallWritePlaceholders.RuleServicesToAdd, ServiceNames(Added(elements), profile))
                .SetStringList(FirewallWritePlaceholders.RuleServicesToRemove, ServiceNames(Removed(elements), profile));
            SetListOrMissing(step, FirewallWritePlaceholders.RuleServices, serviceNames, reason);
            SetNameObjectsOrMissing(step, FirewallWritePlaceholders.RuleServicesNameObjects, serviceNames, reason);
            SetCsvOrMissing(step, FirewallWritePlaceholders.RuleServicesCsv, serviceNames, reason);
            SetListOrMissing(step, FirewallWritePlaceholders.RuleServiceProtocols, protocols, reason);
            SetListOrMissing(step, FirewallWritePlaceholders.RuleServicePorts, renderedPorts, reason);
            SetRuleProtocol(step, protocols, reason);
        }

        /// <summary>A single protocol, or "TCP/UDP" for exactly these two; other mixes would widen the rule to any protocol.</summary>
        private static void SetRuleProtocol(FirewallWriteStep step, List<string> protocols, string noServiceReason)
        {
            if (protocols.Count == 0)
            {
                step.SetMissing(FirewallWritePlaceholders.RuleProtocol, noServiceReason);
            }
            else if (protocols.Count == 1)
            {
                step.SetText(FirewallWritePlaceholders.RuleProtocol, protocols[0]);
            }
            else if (protocols.All(TcpAndUdpProtocols.Contains))
            {
                step.SetText(FirewallWritePlaceholders.RuleProtocol, kTcpAndUdp);
            }
            else
            {
                step.SetMissing(FirewallWritePlaceholders.RuleProtocol, "the services use several protocols that one rule cannot express",
                    FirewallWriteErrorKind.Unsupported);
            }
        }

        private static string NoElementReason(string side) =>
            $"the task has no {side} element that can be written here; to allow any, request the vendor's any object explicitly";

        private static void SetListOrMissing(FirewallWriteStep step, string placeholder, List<string> values, string reason)
        {
            if (values.Count == 0)
            {
                step.SetMissing(placeholder, reason);
                return;
            }
            step.SetStringList(placeholder, values);
        }

        private static void SetNameObjectsOrMissing(FirewallWriteStep step, string placeholder, List<string> names, string reason)
        {
            if (names.Count == 0)
            {
                step.SetMissing(placeholder, reason);
                return;
            }
            step.SetNameObjectList(placeholder, names);
        }

        private static void SetCsvOrMissing(FirewallWriteStep step, string placeholder, List<string> names, string reason)
        {
            step.SetRequiredText(placeholder, string.Join(kCsvSeparator, names), reason);
        }

        private static List<WfImplElement> Current(List<WfImplElement> elements) =>
            [.. elements.Where(element => element.ImplAction != nameof(RequestAction.delete))];

        private static List<WfImplElement> Added(List<WfImplElement> elements) =>
            [.. elements.Where(element => AddActions.Contains(element.ImplAction))];

        private static List<WfImplElement> Removed(List<WfImplElement> elements) =>
            [.. elements.Where(element => element.ImplAction == nameof(RequestAction.delete))];

        /// <summary>Names of network elements. Vendors referencing objects by address skip elements without name.</summary>
        private static List<string> Names(List<WfImplElement> elements, FirewallVendorProfile profile)
        {
            return profile.ReferencesObjectsByAddress
                ? [.. elements.Where(element => !string.IsNullOrWhiteSpace(element.Name)).Select(element => element.Name!.Trim()).Distinct()]
                : [.. elements.Select(element => FirewallElementValues.RequireName(element, profile)).Distinct()];
        }

        private static List<string> ServiceNames(List<WfImplElement> elements, FirewallVendorProfile profile) =>
            [.. elements.Select(element => profile.NormalizeName(FirewallElementValues.ServiceName(element))).Distinct()];
    }
}
