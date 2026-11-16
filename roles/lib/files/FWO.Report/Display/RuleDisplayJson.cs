using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Report;
using FWO.Report.Filter;
using Newtonsoft.Json;
using System.Text;
using System.Globalization;

namespace FWO.Ui.Display
{
    public class RuleDisplayJson(UserConfig userConfig) : RuleDisplayBase(userConfig)
    {
        private string DisplayJsonPlain(string tag, string? value)
        {
            return (value != null ? $"\"{tag}\": {value}," : "");
        }

        protected string DisplayJsonString(string tag, string? value)
        {
            return (value != null ? $"\"{tag}\": {JsonConvert.ToString(value)}," : "");
        }

        protected string DisplayJsonArray(string tag, string? value)
        {
            return (value != null ? $"\"{tag}\": [{value}]," : "");
        }


        private string DisplayNumber(Rule rule)
        {
            return DisplayJsonPlain("number", rule.OrderNumber.ToString(CultureInfo.InvariantCulture));
        }

        protected string DisplayName(string? name)
        {
            return DisplayJsonString("name", name);
        }

        protected string DisplayRuleSourceZones(FirewallZone[] firewallZones)
        {
            return DisplayJsonArray("source zones", ListFirewallZones(firewallZones));
        }

        protected string DisplaySourceNegated(bool sourceNegated)
        {
            return DisplayJsonPlain("source negated", sourceNegated.ToString().ToLower());
        }

        protected string DisplaySource(Rule rule, ReportType reportType)
        {
            return DisplayJsonArray("source", ListNetworkLocations(rule, reportType, true));
        }

        protected string DisplayRuleDestinationZones(FirewallZone[] firewallZones)
        {
            return DisplayJsonArray("destination zones", ListFirewallZones(firewallZones));
        }

        protected string DisplayDestinationNegated(bool destinationNegated)
        {
            return DisplayJsonPlain("destination negated", destinationNegated.ToString().ToLower());
        }

        protected string DisplayDestination(Rule rule, ReportType reportType)
        {
            return DisplayJsonArray("destination", ListNetworkLocations(rule, reportType, false));
        }

        protected string DisplayServiceNegated(bool serviceNegated)
        {
            return DisplayJsonPlain("service negated", serviceNegated.ToString().ToLower());
        }

        protected string DisplayServices(Rule rule, ReportType reportType)
        {
            return DisplayJsonArray("service", ListServices(rule, reportType));
        }

        protected string DisplayAction(string? action)
        {
            return DisplayJsonString("action", action);
        }

        protected string DisplayTrack(string? track)
        {
            return DisplayJsonString("tracking", track);
        }

        protected string DisplayEnforcingGateways(IEnumerable<DeviceWrapper> gateways)
        {
            return DisplayJsonArray("Enforcing Gateway", ListEnforcingGateways(gateways));
        }

        protected string DisplayUid(string? uid)
        {
            return DisplayJsonString("rule uid", uid);
        }

        protected string DisplayEnabled(bool disabled)
        {
            return DisplayJsonPlain("disabled", disabled.ToString().ToLower());
        }

        protected string DisplayComment(string? comment)
        {
            return DisplayJsonString("comment", comment);
        }

        protected string DisplayObjectType(string? objectType)
        {
            return DisplayJsonString("ObjectType", objectType);
        }


        protected string DisplayServiceProtocol(string? serviceProtocol)
        {
            return DisplayJsonString("Protocol", serviceProtocol);
        }

        protected string DisplayServicePort(string? servicePort)
        {
            return DisplayJsonString("Port", servicePort);
        }

        protected string DisplayObjectIP(string? objectIP)
        {
            return DisplayJsonString("Object Type", objectIP);
        }

        protected string DisplayobjectMemberNames(string? objectMemberNames)
        {
            return DisplayJsonString("Member Names", objectMemberNames);
        }


        /// <summary>
        /// Builds a string representing a JSON object that includes all properties of the supplied <paramref name="rule"/>.
        /// The output comprises formatted JSON fields for the rule, structured for reporting use.
        /// If the rule has a SectionHeader, only the section header field is added; otherwise, all rule details are serialized as individual JSON properties.
        /// </summary>
        /// <param name="rule">
        /// The <see cref="Rule"/> instance whose data will be serialized into a JSON object string.
        /// </param>
        /// <param name="reportType">
        /// The <see cref="ReportType"/> used to control formatting or additional field inclusion within the output JSON.
        /// </param>
        /// <returns>
        /// A string containing a syntactically valid JSON object with the specified rule’s properties and values, intended for direct use as part of a JSON array or document.
        /// </returns>
        public string DisplayRuleJsonObject(Rule rule, ReportType reportType)
        {
            StringBuilder stringBuilder = new StringBuilder();

            stringBuilder.Append('{');
            if (string.IsNullOrEmpty(rule.SectionHeader))
            {
                stringBuilder.Append(DisplayNumber(rule));
                stringBuilder.Append(DisplayName(rule.Name));
                stringBuilder.Append(DisplayRuleSourceZones(rule.RuleFromZones
                    .Select(zoneWrapper => zoneWrapper.Content).ToArray()));
                stringBuilder.Append(DisplaySourceNegated(rule.SourceNegated));
                stringBuilder.Append(DisplaySource(rule, reportType));
                stringBuilder.Append(DisplayRuleDestinationZones(rule.RuleToZones
                    .Select(zoneWrapper => zoneWrapper.Content).ToArray()));
                stringBuilder.Append(DisplayDestinationNegated(rule.DestinationNegated));
                stringBuilder.Append(DisplayDestination(rule, reportType));
                stringBuilder.Append(DisplayServiceNegated(rule.ServiceNegated));
                stringBuilder.Append(DisplayServices(rule, reportType));
                stringBuilder.Append(DisplayAction(rule.Action));
                stringBuilder.Append(DisplayTrack(rule.Track));
                stringBuilder.Append(DisplayEnabled(rule.Disabled));
                stringBuilder.Append(DisplayUid(rule.Uid));
                stringBuilder.Append(DisplayComment(rule.Comment));
                RemoveLastChars(stringBuilder, 1).ToString();
            }
            else
            {
                stringBuilder.AppendLine("\"section header\": " + JsonConvert.ToString(rule.SectionHeader));
            }
            stringBuilder.Append("},");
            return stringBuilder.ToString();
        }

        protected string ListNetworkLocations(Rule rule, ReportType reportType, bool isSource)
        {
            return ListNetworkLocations(isSource ? rule.Froms : rule.Tos, reportType);
        }

        /// <summary>
        /// Lists network locations as comma separated JSON strings. Resolved reports list the group members,
        /// NAT change reports list the objects as referenced in the rule, all other reports list nothing.
        /// </summary>
        /// <param name="networkLocations">Network locations of a rule (or of its translated part).</param>
        /// <param name="reportType">Report type defining the level of resolution.</param>
        /// <returns>Comma separated JSON strings, empty if the report type lists no network locations.</returns>
        protected static string ListNetworkLocations(NetworkLocation[] networkLocations, ReportType reportType)
        {
            List<NetworkLocation> listedLocations;
            if (reportType.IsResolvedReport())
            {
                listedLocations = GetResolvedNetworkLocations(networkLocations);
            }
            else if (reportType == ReportType.NatChanges)
            {
                listedLocations = [.. networkLocations];
            }
            else
            {
                return "";
            }
            List<string> displayedLocations = [];
            foreach (NetworkLocation networkLocation in listedLocations)
            {
                displayedLocations.Add(JsonConvert.ToString(DisplayNetworkLocation(networkLocation, reportType).ToString()));
            }
            return string.Join(",", displayedLocations);
        }

        protected string ListServices(Rule rule, ReportType reportType)
        {
            return ListServices(rule.Services, reportType);
        }

        /// <summary>
        /// Lists services as comma separated JSON strings. Resolved reports list the group members,
        /// NAT change reports list the services as referenced in the rule, all other reports list nothing.
        /// </summary>
        /// <param name="services">Services of a rule (or of its translated part).</param>
        /// <param name="reportType">Report type defining the level of resolution.</param>
        /// <returns>Comma separated JSON strings, empty if the report type lists no services.</returns>
        protected static string ListServices(ServiceWrapper[] services, ReportType reportType)
        {
            List<NetworkService> listedServices;
            if (reportType.IsResolvedReport())
            {
                listedServices = GetNetworkServices(services);
            }
            else if (reportType == ReportType.NatChanges)
            {
                listedServices = [.. services.Select(service => service.Content)];
            }
            else
            {
                return "";
            }
            List<string> displayedServices = [];
            foreach (NetworkService service in listedServices)
            {
                displayedServices.Add(JsonConvert.ToString(DisplayService(service, reportType).ToString()));
            }
            return string.Join(",", displayedServices);
        }

        protected string ListEnforcingGateways(IEnumerable<DeviceWrapper> gateways)
        {
            return string.Join(",",
                    gateways
                        .Where(gw => gw?.Content?.Name != null)
                        .Select(gw => JsonConvert.ToString(gw.Content.Name))
                );
        }

        protected static string ListFirewallZones(FirewallZone[] firewallZones)
        {
            List<string> displayedZones = new List<string>();
            foreach (FirewallZone networkZone in firewallZones)
            {
                displayedZones.Add(JsonConvert.ToString(networkZone.Name));
            }
            return string.Join(",", displayedZones);
        }
    }
}
