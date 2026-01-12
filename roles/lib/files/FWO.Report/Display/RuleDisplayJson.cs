using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Report;
using FWO.Report.Filter;
using System.Text;

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
            return (value != null ? $"\"{tag}\": \"{value}\"," : "");
        }

        protected string DisplayJsonArray(string tag, string? value)
        {
            return (value != null ? $"\"{tag}\": [{value}]," : "");
        }


        private new string DisplayNumber(Rule rule)
        {
            return DisplayJsonPlain("number", rule.DisplayOrderNumber.ToString());
        }

        protected string DisplayName(string? name)
        {
            return DisplayJsonString("name", name);
        }

        protected string DisplayRuleSourceZones(NetworkZone[] networkZones)
        {
            return DisplayJsonArray("source zones", ListNetworkZones(networkZones));
        }

        protected string DisplaySourceNegated(bool sourceNegated)
        {
            return DisplayJsonPlain("source negated", sourceNegated.ToString().ToLower());
        }

        protected string DisplaySource(Rule rule, ReportType reportType)
        {
            return DisplayJsonArray("source", ListNetworkLocations(rule, reportType, true));
        }

        protected string DisplayRuleDestinationZones(NetworkZone[] networkZones)
        {
            return DisplayJsonArray("destination zones", ListNetworkZones(networkZones));
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
                stringBuilder.AppendLine("\"section header\": \"" + rule.SectionHeader + "\"");
            }
            stringBuilder.Append("},");
            return stringBuilder.ToString();
        }

        protected string ListNetworkLocations(Rule rule, ReportType reportType, bool isSource)
        {
            if (reportType.IsResolvedReport())
            {
                List<string> displayedLocations = new List<string>();
                foreach (NetworkLocation networkLocation in GetResolvedNetworkLocations(isSource ? rule.Froms : rule.Tos))
                {
                    displayedLocations.Add(Quote(DisplayNetworkLocation(networkLocation, reportType).ToString()));
                }
                return string.Join(",", displayedLocations);
            }
            return "";
        }

        protected string ListServices(Rule rule, ReportType reportType)
        {
            if (reportType.IsResolvedReport())
            {
                List<string> displayedServices = new List<string>();
                foreach (NetworkService service in GetNetworkServices(rule.Services))
                {
                    displayedServices.Add(Quote(DisplayService(service, reportType).ToString()));
                }
                return(string.Join(",", displayedServices));
            }
            return "";
        }

        protected string ListEnforcingGateways(IEnumerable<DeviceWrapper> gateways)
        {
            return string.Join(",",
                    gateways
                        .Where(gw => gw?.Content?.Name != null)
                        .Select(gw => Quote(gw.Content.Name))
                );
        }

        protected string ListNetworkZones(NetworkZone[] networkZones)
        {
            List<string> displayedZones = new List<string>();
            foreach (NetworkZone networkZone in networkZones)
            {
                displayedZones.Add(Quote(networkZone.Name));
            }
            return string.Join(",", displayedZones);
        }
    }
}
