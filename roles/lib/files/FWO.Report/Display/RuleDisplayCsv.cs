﻿using FWO.Api.Data;
using FWO.Config.Api;
using System.Text;
using FWO.Report.Filter;

namespace FWO.Ui.Display
{
    public class RuleDisplayCsv : RuleDisplayBase
    {

        public RuleDisplayCsv(UserConfig userConfig) : base(userConfig)
        { }
        public new string DisplayNumber(Rule rule, Rule[] rules)
        {
            return $"{rule.DisplayOrderNumber.ToString()},";
        }
        public new string DisplayName(Rule rule)
        {
            return (rule.Name != null ? $"\"{rule.Name}\"," : ",");
        }
        public new string DisplaySourceZone(Rule rule)
        {
            return (rule.SourceZone != null ? $"\"{rule.SourceZone.Name}\"," : ",");
        }

        public new string DisplayDestinationZone(Rule rule)
        {
            return (rule.DestinationZone != null ? $"\"{rule.DestinationZone.Name}\"," : ",");
        }

        public new string DisplayAction(Rule rule)
        {
            return $"\"{rule.Action}\",";
        }

        public new string DisplayTrack(Rule rule)
        {
            return $"\"{rule.Track}\",";
        }

        public new string DisplayUid(Rule rule)
        {
            return (rule.Uid != null ? $"\"{rule.Uid}\"," : ",");
        }

        public new string DisplayComment(Rule rule)
        {
            return (rule.Comment != null ? $"\"{rule.Comment}\"," : ",");
        }

        public string DisplaySourceOrDestination(Rule rule, string style = "", string location = "report", ReportType reportType = ReportType.Rules, string side = "source")
        {
            result = new StringBuilder("");
            if (side=="source")
                result.Append($"\"{((rule.SourceNegated)? "source negated" : "")}\",");
            else if (side=="destination")
                result.Append($"\"{((rule.DestinationNegated)? "destination negated" : "")}\",");

            if (reportType == ReportType.ResolvedRules || reportType == ReportType.ResolvedRulesTech)
            {
                HashSet<NetworkObject> collectedNetworkObjects = new HashSet<NetworkObject>();
                HashSet<NetworkLocation> collectedUserNetworkObjects = new HashSet<NetworkLocation>();
                if (side == "source")
                {
                    foreach (NetworkLocation networkObject in rule.Froms)
                    {
                        foreach (GroupFlat<NetworkObject> nwObject in networkObject.Object.ObjectGroupFlats)
                            if (nwObject.Object != null && nwObject.Object.Type.Name != "group")    // leave out group level altogether
                                collectedUserNetworkObjects.Add(new NetworkLocation(networkObject.User, nwObject.Object));
                    }
                }
                else if (side == "destination")
                {
                    foreach (NetworkLocation networkObject in rule.Tos)
                    {
                        foreach (GroupFlat<NetworkObject> nwObject in networkObject.Object.ObjectGroupFlats)
                            if (nwObject.Object != null && nwObject.Object.Type.Name != "group")    // leave out group level altogether
                                collectedUserNetworkObjects.Add(new NetworkLocation(networkObject.User, nwObject.Object));
                    }
                }

                List<NetworkLocation> userNwObjectList = collectedUserNetworkObjects.ToList<NetworkLocation>();
                userNwObjectList.Sort();

                StringBuilder cell = new StringBuilder();
                foreach (NetworkLocation networkLocation in userNwObjectList)
                {
                    cell.Append(NetworkLocationToCsv(networkLocation, rule.MgmtId, location, style, reportType=reportType).ToString());
                }
                cell.Remove(cell.ToString().Length - 2, 2);  // get rid of final line break
                result.Append($"\"{cell}\",");
            }
            return result.ToString();
        }

        public string DisplaySource(Rule rule, string style = "", string location = "report", ReportType reportType = ReportType.Rules)
        {
            return DisplaySourceOrDestination(rule, style, location, reportType, side: "source");
        }

        public string DisplayDestination(Rule rule, string style = "", string location = "report", ReportType reportType = ReportType.Rules)
        {
            return DisplaySourceOrDestination(rule, style, location, reportType, side: "destination");
        }

        private StringBuilder NetworkLocationToCsv(NetworkLocation userNetworkObject, int mgmtId, string location = "", string style = "", ReportType reportType = ReportType.Rules)
        {
            StringBuilder result = new StringBuilder();

            if (userNetworkObject.User?.Id != null)
            {
                result.Append($"{userNetworkObject.User.Name}@");
            }

            if (reportType!=ReportType.ResolvedRulesTech)
            {
                result.Append($"{userNetworkObject.Object.Name}");
                result.Append(" (");
            }
            result.Append(DisplayIpRange(userNetworkObject.Object.IP, userNetworkObject.Object.IpEnd));
            if (reportType!=ReportType.ResolvedRulesTech)
            {
                result.Append(")");
            }
            result.Append("\\n");
            return result;
        }

        public string DisplayService(Rule rule, string style = "", string location = "report", ReportType reportType = ReportType.Rules)
        {
            result = new StringBuilder();
            result.Append($"\"{((rule.ServiceNegated)? "negated" : "")}\",");

            switch (reportType)
            {
                case ReportType.Rules:
                    break;
                case ReportType.ResolvedRules:
                case ReportType.ResolvedRulesTech:
                    HashSet<NetworkService> collectedServices = new HashSet<NetworkService>();
                    foreach (ServiceWrapper service in rule.Services)
                        foreach (GroupFlat<NetworkService> nwService in service.Content.ServiceGroupFlats)
                            if (nwService.Object != null && nwService.Object.Type.Name != "group")
                                collectedServices.Add(nwService.Object);

                    List<NetworkService> serviceList = collectedServices.ToList<NetworkService>();
                    serviceList.Sort(delegate (NetworkService x, NetworkService y) { return x.Name.CompareTo(y.Name); });

                    StringBuilder cell = new StringBuilder();
                    foreach (NetworkService service in serviceList)
                        cell.Append(ServiceToCsv(service, rule.MgmtId, location, style, reportType=reportType).ToString());
                    
                    cell.Remove(cell.ToString().Length - 2, 2);  // get rid of final line break
                    result.Append($"\"{cell}\",");
                    break;
            }
            return result.ToString();
        }
        private StringBuilder ServiceToCsv(NetworkService service, int mgmtId, string location = "", string style = "", ReportType reportType = ReportType.Rules)
        {
            StringBuilder result = new StringBuilder();
            if (reportType != ReportType.ResolvedRulesTech)
            {
                result.Append($"{service.Name}");
                if (service.DestinationPort != null)
                    result.Append(service.DestinationPort == service.DestinationPortEnd ? $" ({service.DestinationPort}/{service.Protocol?.Name})"
                        : $" ({service.DestinationPort}-{service.DestinationPortEnd}/{service.Protocol?.Name})");
            }
            else 
            {
                if (service.DestinationPort == null)
                    result.Append($"{service.Name}");
                else
                    result.Append(service.DestinationPort == service.DestinationPortEnd ? $"{service.DestinationPort}/{service.Protocol?.Name}"
                        : $"{service.DestinationPort}-{service.DestinationPortEnd}/{service.Protocol?.Name}");
            }
            result.Append("\\n");
            return result;
        }

        public string DisplayEnabled(Rule rule, bool export = false)
        {
            return $"\"{((rule.Disabled)?"disabled":"enabled")}\",";
        }
    }
}
