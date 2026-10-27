using FWO.Data.Workflow;

namespace FWO.ExternalSystems.FirewallWrite
{
    /// <summary>
    /// Fills the source, destination and service placeholders of a rule step from the elements of an implementation task.
    /// The full lists hold all elements not being deleted; an empty full list becomes the vendor's "any" object.
    /// </summary>
    public static class FirewallRuleValues
    {
        private const string kCsvSeparator = ",";
        private const string kAnyProtocol = "Any";

        private static readonly List<string> SourceFields = [nameof(ElemFieldType.source), nameof(ElemFieldType.modelled_source)];
        private static readonly List<string> DestinationFields = [nameof(ElemFieldType.destination), nameof(ElemFieldType.modelled_destination)];
        private static readonly List<string> AddActions = [nameof(RequestAction.create), nameof(RequestAction.addAfterCreation)];

        /// <summary>Sets all rule element placeholders of the step.</summary>
        public static void SetRuleElements(FirewallWriteStep step, WfImplTask implTask, FirewallVendorProfile profile)
        {
            List<WfImplElement> sources = [.. implTask.ImplElements.Where(element => SourceFields.Contains(element.Field))];
            List<WfImplElement> destinations = [.. implTask.ImplElements.Where(element => DestinationFields.Contains(element.Field))];
            List<WfImplElement> services = [.. implTask.ImplElements.Where(FirewallElementValues.IsServiceElement)];

            SetNetworkList(step, sources, profile, new(FirewallWritePlaceholders.RuleSources, FirewallWritePlaceholders.RuleSourcesToAdd,
                FirewallWritePlaceholders.RuleSourcesToRemove, FirewallWritePlaceholders.RuleSourcesNameObjects,
                FirewallWritePlaceholders.RuleSourcesCsv, FirewallWritePlaceholders.RuleSourceAddresses));
            SetNetworkList(step, destinations, profile, new(FirewallWritePlaceholders.RuleDestinations, FirewallWritePlaceholders.RuleDestinationsToAdd,
                FirewallWritePlaceholders.RuleDestinationsToRemove, FirewallWritePlaceholders.RuleDestinationsNameObjects,
                FirewallWritePlaceholders.RuleDestinationsCsv, FirewallWritePlaceholders.RuleDestinationAddresses));
            SetServiceList(step, services, profile);
        }

        private sealed record NetworkListPlaceholders(string Full, string ToAdd, string ToRemove, string NameObjects, string Csv, string Addresses);

        private static void SetNetworkList(FirewallWriteStep step, List<WfImplElement> elements, FirewallVendorProfile profile,
            NetworkListPlaceholders placeholders)
        {
            List<WfImplElement> current = Current(elements);
            List<string> names = AnyIfEmpty(Names(current, profile), profile);
            List<string> currentAddresses = profile.ReferencesObjectsByAddress
                ? [.. current.Select(FirewallElementValues.AddressNotation).Distinct()]
                : [];
            List<string> addresses = profile.ReferencesObjectsByAddress ? AnyIfEmpty(currentAddresses, profile) : currentAddresses;
            step.SetStringList(placeholders.Full, names)
                .SetStringList(placeholders.ToAdd, Names(Added(elements), profile))
                .SetStringList(placeholders.ToRemove, Names(Removed(elements), profile))
                .SetNameObjectList(placeholders.NameObjects, names)
                .SetText(placeholders.Csv, string.Join(kCsvSeparator, names))
                .SetStringList(placeholders.Addresses, addresses);
        }

        private static void SetServiceList(FirewallWriteStep step, List<WfImplElement> elements, FirewallVendorProfile profile)
        {
            List<WfImplElement> current = Current(elements);
            List<string> serviceNames = ServiceNames(current, profile);
            List<string> names = serviceNames.Count == 0 ? [profile.AnyServiceName] : serviceNames;
            List<string> ports = [.. current.Select(FirewallElementValues.PortRange).Where(range => range.Length > 0).Distinct()];
            List<string> protocols = [.. current.Select(element => FirewallElementValues.ProtocolName(element.ProtoId).ToUpperInvariant()).Distinct()];
            List<string> renderedPorts = ports.Count == 0 ? [profile.AnyServiceName] : ports;
            List<string> renderedProtocols = protocols.Count == 0 ? [kAnyProtocol] : protocols;
            step.SetStringList(FirewallWritePlaceholders.RuleServices, names)
                .SetText(FirewallWritePlaceholders.RuleServicesCsv, string.Join(kCsvSeparator, serviceNames))
                .SetText(FirewallWritePlaceholders.RuleProtocol, protocols.Count == 1 ? protocols[0] : kAnyProtocol.ToLowerInvariant())
                .SetStringList(FirewallWritePlaceholders.RuleServicesToAdd, ServiceNames(Added(elements), profile))
                .SetStringList(FirewallWritePlaceholders.RuleServicesToRemove, ServiceNames(Removed(elements), profile))
                .SetNameObjectList(FirewallWritePlaceholders.RuleServicesNameObjects, names)
                .SetStringList(FirewallWritePlaceholders.RuleServicePorts, renderedPorts)
                .SetStringList(FirewallWritePlaceholders.RuleServiceProtocols, renderedProtocols);
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

        private static List<string> AnyIfEmpty(List<string> values, FirewallVendorProfile profile) =>
            values.Count == 0 ? [profile.AnyName] : values;
    }
}
