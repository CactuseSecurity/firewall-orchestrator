using System.Text;
using FWO.Basics;
using FWO.Data;
using FWO.Config.Api;
using FWO.Report;
using FWO.Report.Filter;

namespace FWO.Ui.Display
{
    public class RuleDisplayBase(UserConfig userConfig)
    {
        protected UserConfig userConfig = userConfig;

        private readonly CustomFieldKeyCache changeIdKeyCache = new();

        /// <summary>
        /// Resolves the change identifier from the first configured matching rule custom field.
        /// </summary>
        /// <param name="rule">Rule containing the serialized custom fields.</param>
        /// <returns>The configured custom field value, an error description, or an empty string.</returns>
        public string DisplayChangeId(Rule rule)
        {
            string keysJson = userConfig.GlobalConfig?.CustomFieldChangeIdKey ?? GlobalConst.kDefaultChangeIdKeys;
            string? value = CustomFieldResolver.ExtractCustomFieldValue<string>(rule, changeIdKeyCache.GetKeys(keysJson),
                out string? errorMessage, CustomFieldKeyMatching.IgnoreCase);
            return value ?? errorMessage ?? "";
        }

        public static string DisplayHierarchicalNumber(Rule rule)
        {
            return rule.DisplayOrderNumberString;
        }

        public static string DisplayEnabled(Rule rule, OutputLocation location)
        {
            if (location == OutputLocation.export)
            {
                return $"<b>{(rule.Disabled ? "N" : "Y")}</b>";
            }
            else
            {
                return $"<div class=\"{(rule.Disabled ? Icons.Close : Icons.Check)}\"></div>";
            }
        }

        public static string DisplayLastModified(Rule rule)
        {
            DateTime? lastModified = rule.LastModified ?? rule.Metadata?.Created;
            return lastModified.HasValue
                ? DateOnly.FromDateTime(lastModified.Value).ToString("yyyy-MM-dd")
                : "";
        }

        public static string DisplayIsCompliant(Rule rule, OutputLocation location)
        {
            if (rule.Compliance != ComplianceViolationType.NotAssessable)
            {
                bool isCompliant = true;

                if (rule.Compliance != ComplianceViolationType.None)
                {
                    isCompliant = false;
                }

                if (location == OutputLocation.export)
                {
                    return $"<b>{(isCompliant ? "Y" : "N")}</b>";
                }
                else
                {
                    return $"<div class=\"oi {(isCompliant ? "oi-check" : "oi-x")}\"></div>";
                }
            }
            else
            {
                return "Not Evaluable";
            }
        }

        public static string DisplayViolationDetails(Rule rule)
        {
            string violationDetails = "<p>";
            bool notFirst = false;

            foreach (ComplianceViolation violation in rule.Violations)
            {
                if (notFirst)
                {
                    violationDetails += "<br>";
                }

                violationDetails += HtmlOutputEncoder.EncodeText(violation.Details);
                notFirst = true;
            }

            violationDetails += "</p>";

            return violationDetails;
        }

        public static string DisplayName(Rule rule)
        {
            return rule.Name ?? "";
        }

        public static string DisplayName(NetworkObject nto)
        {
            return nto.Name ?? "";
        }

        public static string DisplayName(NetworkService nts)
        {
            return nts.Name ?? "";
        }

        public static string DisplayName(NetworkUser ntu)
        {
            return ntu.Name ?? "";
        }

        public static string DisplaySourceZones(Rule rule)
        {
            if (rule.RuleFromZones.Length != 0)
            {
                string ruleZones = "";
                bool notFirst = false;

                foreach (ZoneWrapper zoneWrapper in rule.RuleFromZones)
                {
                    if (notFirst)
                    {
                        ruleZones += "<br>";
                    }

                    ruleZones += HtmlOutputEncoder.EncodeText(zoneWrapper.Content.Name);
                    notFirst = true;
                }
                return ruleZones;
            }
            else
            {
                return "";
            }
        }

        public static string DisplayDestinationZones(Rule rule)
        {
            if (rule.RuleToZones.Length != 0)
            {
                string ruleZones = "";
                bool notFirst = false;

                foreach (ZoneWrapper zoneWrapper in rule.RuleToZones)
                {
                    if (notFirst)
                    {
                        ruleZones += "<br>";
                    }

                    ruleZones += HtmlOutputEncoder.EncodeText(zoneWrapper.Content.Name);
                    notFirst = true;
                }
                return ruleZones;
            }
            else
            {
                return "";
            }
        }

        public static string DisplayAction(Rule rule)
        {
            return rule.Action;
        }

        public static string DisplayTrack(Rule rule)
        {
            return rule.Track;
        }

        public static string DisplayUid(Rule rule)
        {
            return rule.Uid ?? "";
        }
        public static string DisplayUid(NetworkObject nwo)
        {
            return nwo.Uid ?? "";
        }
        public static string DisplayUid(NetworkService nws)
        {
            return nws.Uid ?? "";
        }

        public static string DisplayComment(Rule rule)
        {
            return rule.Comment ?? "";
        }

        public static StringBuilder DisplayNetworkLocation(NetworkLocation userNetworkObject, ReportType reportType, string? userName = null, string? objName = null)
        {
            StringBuilder result = new();

            if (userNetworkObject.User != null && userNetworkObject.User.Id > 0)
            {
                result.Append($"{userName ?? userNetworkObject.User.Name}@");
            }

            if (!reportType.IsTechReport())
            {
                result.Append($"{objName ?? userNetworkObject.Object.Name}");
            }
            if (userNetworkObject.Object.Type.Name != ObjectType.Group)
            {
                bool showIpinBrackets = !reportType.IsTechReport();
                result.Append(NwObjDisplay.DisplayIp(
                    userNetworkObject.Object.IP,
                    userNetworkObject.Object.IpEnd,
                    userNetworkObject.Object.Type.Name,
                    showIpinBrackets));
            }
            return reportType == ReportType.VarianceAnalysis ? DisplayWithIcon(result, ObjCategory.nobj, userNetworkObject.Object.Type.Name) : result;
        }

        public static StringBuilder DisplayService(NetworkService service, ReportType reportType, string? serviceName = null)
        {
            StringBuilder result = DisplayBase.DisplayService(service, reportType.IsTechReport(), serviceName);
            return reportType == ReportType.VarianceAnalysis ? DisplayWithIcon(result, ObjCategory.nsrv, service.Type.Name) : result;
        }

        public static StringBuilder DisplayGateway(Device gateway, ReportType reportType, string? gatewayName = null)
        {
            return DisplayBase.DisplayGateway(gateway, reportType.IsTechReport(), gatewayName);
        }

        public static StringBuilder RemoveLastChars(StringBuilder s, int count)
        {
            string x = s.ToString();
            x = x.Remove(x.ToString().Length - count, count).ToString();
            return s.Remove(s.ToString().Length - count, count);
        }

        public static string Quote(string? input)
        {
            return $"\"{input ?? ""}\"";
        }

        public static List<NetworkLocation> GetResolvedNetworkLocations(NetworkLocation[] locationArray)
        {
            HashSet<NetworkLocation> collectedUserNetworkObjects = [];
            foreach (NetworkLocation networkObject in locationArray)
            {
                if (networkObject.Object.Type.Name == ObjectType.Group)
                {
                    foreach (GroupFlat<NetworkObject> nwObject in networkObject.Object.ObjectGroupFlats)
                    {
                        if (nwObject.Object != null && nwObject.Object.Type.Name != ObjectType.Group || networkObject.Object.ObjectGroupFlats.Count() == 1)    // leave out group level altogether, except for empty groups
                        {
                            collectedUserNetworkObjects.Add(new NetworkLocation(networkObject.User, nwObject.Object));
                        }
                    }
                }
                else
                {
                    collectedUserNetworkObjects.Add(networkObject);
                }
            }
            List<NetworkLocation> userNwObjectList = [.. collectedUserNetworkObjects];
            userNwObjectList.Sort();
            return userNwObjectList;
        }

        public static List<NetworkService> GetNetworkServices(ServiceWrapper[] serviceArray)
        {
            HashSet<NetworkService> collectedServices = [];
            foreach (ServiceWrapper service in serviceArray)
            {
                foreach (GroupFlat<NetworkService> nwService in service.Content.ServiceGroupFlats)
                {
                    if (nwService.Object != null && nwService.Object.Type.Name != ObjectType.Group)
                    {
                        collectedServices.Add(nwService.Object);
                    }
                }

                if (!service.Content.ServiceGroupFlats.Any())
                {
                    collectedServices.Add(service.Content);
                }

            }
            List<NetworkService> serviceList = [.. collectedServices];
            serviceList.Sort(delegate (NetworkService x, NetworkService y) { return x.Name.CompareTo(y.Name); });
            return serviceList;
        }

        protected static void AnalyzeElements(string oldElement, string newElement, ref List<string> unchanged, ref List<string> deleted, ref List<string> added)
        {
            string[] separatingStrings = [","];
            string[] oldAr = oldElement.Split(separatingStrings, StringSplitOptions.RemoveEmptyEntries);
            string[] newAr = newElement.Split(separatingStrings, StringSplitOptions.RemoveEmptyEntries);

            foreach (var item in oldAr)
            {
                if (newAr.Contains(item))
                {
                    unchanged.Add(item);
                }
                else
                {
                    string deletedItem = item;
                    deleted.Add(deletedItem);
                }
            }
            foreach (var item in newAr)
            {
                if (!oldAr.Contains(item))
                {
                    string newItem = item;
                    added.Add(newItem);
                }
            }
        }

        /// <summary>
        /// Renders a network location as html. Names are encoded, unless the caller passes already rendered
        /// html for them, such as a link built by <see cref="ReportBase.ConstructLink"/>.
        /// </summary>
        /// <param name="networkLocation">The user and object to display.</param>
        /// <param name="reportType">Report type deciding which parts are shown.</param>
        /// <param name="userHtml">Rendered html for the user, or null to show the encoded user name.</param>
        /// <param name="objectHtml">Rendered html for the object, or null to show the encoded object name.</param>
        /// <returns>Html that is safe to place between tags.</returns>
        protected static string DisplayNetworkLocationHtml(NetworkLocation networkLocation, ReportType reportType, string? userHtml = null, string? objectHtml = null)
        {
            return DisplayNetworkLocation(networkLocation, reportType,
                userHtml ?? HtmlOutputEncoder.EncodeText(networkLocation.User?.Name),
                objectHtml ?? HtmlOutputEncoder.EncodeText(networkLocation.Object.Name)).ToString();
        }

        /// <summary>
        /// Renders a service as html. The name is encoded, unless the caller passes already rendered html for it.
        /// </summary>
        /// <param name="service">The service to display.</param>
        /// <param name="reportType">Report type deciding which parts are shown.</param>
        /// <param name="serviceNameHtml">Rendered html for the service name, or null to show the encoded name.</param>
        /// <returns>Html that is safe to place between tags.</returns>
        protected static string DisplayServiceHtml(NetworkService service, ReportType reportType, string? serviceNameHtml = null)
        {
            StringBuilder result = new();
            if (reportType.IsTechReport())
            {
                result.Append(HtmlOutputEncoder.EncodeText(DisplayBase.DisplayService(service, true).ToString()));
            }
            else
            {
                result.Append(serviceNameHtml ?? HtmlOutputEncoder.EncodeText(service.Name));
                result.Append(HtmlOutputEncoder.EncodeText(DisplayBase.DisplayServiceDetails(service)));
            }
            return (reportType == ReportType.VarianceAnalysis ? DisplayWithIcon(result, ObjCategory.nsrv, service.Type.Name) : result).ToString();
        }

        private static StringBuilder DisplayWithIcon(StringBuilder outputString, ObjCategory? objCategory, string? objType)
        {
            string symbol = ReportBase.GetIconClass(objCategory, objType);
            return new($"<span class=\"{symbol}\">{outputString}</span>");
        }
    }
}
