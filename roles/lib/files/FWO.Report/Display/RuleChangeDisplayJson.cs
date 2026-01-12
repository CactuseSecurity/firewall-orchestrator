using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Report;
using FWO.Report.Filter;
using System.Text;

namespace FWO.Ui.Display
{
    public class RuleChangeDisplayJson : RuleDisplayJson
    {
        public RuleChangeDisplayJson(UserConfig userConfig) : base(userConfig)
        { }

        public string DisplayChangeTime(RuleChange ruleChange)
        {
            return DisplayJsonString("change time", ruleChange.ChangeImport.Time.ToString());
        }
        public string DisplayChangeTime(ObjectChange objectChange)
        {
            return DisplayJsonString("change time", objectChange.ChangeImport.Time.ToString());
        }
        public string DisplayChangeTime(ServiceChange serviceChange)
        {
            return DisplayJsonString("change time", serviceChange.ChangeImport.Time.ToString());
        }
        public string DisplayChangeTime(UserChange userChange)
        {
            return DisplayJsonString("change time", userChange.ChangeImport.Time.ToString());
        }

        public string DisplayChangeAction(RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'I': return DisplayJsonString("change action", userConfig.GetText("rule_added"));
                case 'D': return DisplayJsonString("change action", userConfig.GetText("rule_deleted"));
                case 'C': return DisplayJsonString("change action", userConfig.GetText("rule_modified"));
                default: return "";
            }
        }
        public string DisplayChangeAction(ObjectChange objectChange)
        {
            switch (objectChange.ChangeAction)
            {
                case 'I': return DisplayJsonString("change action", userConfig.GetText("network_object_added"));
                case 'D': return DisplayJsonString("change action", userConfig.GetText("network_object_deleted"));
                case 'C': return DisplayJsonString("change action", userConfig.GetText("network_object_modified"));
                default: return "";
            }
        }
        public string DisplayChangeAction(ServiceChange serviceChange)
        {
            switch (serviceChange.ChangeAction)
            {
                case 'I': return DisplayJsonString("change action", userConfig.GetText("service_added"));
                case 'D': return DisplayJsonString("change action", userConfig.GetText("service_deleted"));
                case 'C': return DisplayJsonString("change action", userConfig.GetText("service_modified"));
                default: return "";
            }
        }
        public string DisplayChangeAction(UserChange userChange)
        {
            switch (userChange.ChangeAction)
            {
                case 'I': return DisplayJsonString("change action", userConfig.GetText("user_added"));
                case 'D': return DisplayJsonString("change action", userConfig.GetText("user_deleted"));
                case 'C': return DisplayJsonString("change action", userConfig.GetText("user_modified"));
                default: return "";
            }
        }

        public string DisplayName(RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return DisplayName(ruleChange.OldRule.Name);
                case 'I': return DisplayName(ruleChange.NewRule.Name);
                case 'C': return DisplayName(DisplayDiff(ruleChange.OldRule.Name, ruleChange.NewRule.Name));
                default: return "";
            }
        }
        public string DisplayName(ObjectChange objectChange)
        {
            switch (objectChange.ChangeAction)
            {
                case 'D': return DisplayName(objectChange.OldObject.Name);
                case 'I': return DisplayName(objectChange.NewObject.Name);
                case 'C': return DisplayName(DisplayDiff(objectChange.OldObject.Name, objectChange.NewObject.Name));
                default: return "";
            }
        }
        public string DisplayName(ServiceChange serviceChange)
        {
            switch (serviceChange.ChangeAction)
            {
                case 'D': return DisplayName(serviceChange.OldService.Name);
                case 'I': return DisplayName(serviceChange.NewService.Name);
                case 'C': return DisplayName(DisplayDiff(serviceChange.OldService.Name, serviceChange.NewService.Name));
                default: return "";
            }
        }
        public string DisplayName(UserChange userChange)
        {
            switch (userChange.ChangeAction)
            {
                case 'D': return DisplayName(userChange.OldUser.Name);
                case 'I': return DisplayName(userChange.NewUser.Name);
                case 'C': return DisplayName(DisplayDiff(userChange.OldUser.Name, userChange.NewUser.Name));
                default: return "";
            }
        }

        public string DisplaySourceZones(RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return DisplayRuleSourceZones(ruleChange.OldRule.RuleFromZones.Select(z => z.Content).ToArray());
                case 'I': return DisplayRuleSourceZones(ruleChange.NewRule.RuleFromZones.Select(z => z.Content).ToArray());
                case 'C': return DisplayJsonArray("source zones", DisplayArrayDiff(ListNetworkZones(ruleChange.OldRule.RuleFromZones.Select(z => z.Content).ToArray()), ListNetworkZones(ruleChange.NewRule.RuleFromZones.Select(z => z.Content).ToArray())));
                default: return "";
            }
        }

        public string DisplaySourceNegated(RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return DisplaySourceNegated(ruleChange.OldRule.SourceNegated);
                case 'I': return DisplaySourceNegated(ruleChange.NewRule.SourceNegated);
                case 'C': return ruleChange.OldRule.SourceNegated == ruleChange.NewRule.SourceNegated ?
                    DisplaySourceNegated(ruleChange.NewRule.SourceNegated) :
                    DisplayJsonString("source negated", DisplayDiff(ruleChange.OldRule.SourceNegated.ToString().ToLower(), ruleChange.NewRule.SourceNegated.ToString().ToLower()));
                default: return "";
            }
        }

        public string DisplaySource(RuleChange ruleChange, ReportType reportType)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return DisplaySource(ruleChange.OldRule, reportType);
                case 'I': return DisplaySource(ruleChange.NewRule, reportType);
                case 'C': return DisplayJsonArray("source", DisplayArrayDiff(ListNetworkLocations(ruleChange.OldRule, reportType, true), 
                    ListNetworkLocations(ruleChange.NewRule, reportType, true)));
                default: return "";
            }
        }

        public string DisplayDestinationZones(RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return DisplayRuleDestinationZones(ruleChange.OldRule.RuleToZones.Select(z => z.Content).ToArray());
                case 'I': return DisplayRuleDestinationZones(ruleChange.NewRule.RuleToZones.Select(z => z.Content).ToArray());
                case 'C': return DisplayJsonArray("destination zones", DisplayArrayDiff(ListNetworkZones(ruleChange.OldRule.RuleToZones.Select(z => z.Content).ToArray()), ListNetworkZones(ruleChange.NewRule.RuleToZones.Select(z => z.Content).ToArray())));
                default: return "";
            }
        }

        public string DisplayDestinationNegated(RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return DisplayDestinationNegated(ruleChange.OldRule.DestinationNegated);
                case 'I': return DisplayDestinationNegated(ruleChange.NewRule.DestinationNegated);
                case 'C': return ruleChange.OldRule.DestinationNegated == ruleChange.NewRule.DestinationNegated ?
                    DisplayDestinationNegated(ruleChange.NewRule.DestinationNegated) :
                    DisplayJsonString("destination negated", DisplayDiff(ruleChange.OldRule.DestinationNegated.ToString().ToLower(), ruleChange.NewRule.DestinationNegated.ToString().ToLower()));
                default: return "";
            }
        }

        public string DisplayDestination(RuleChange ruleChange, ReportType reportType)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return DisplayDestination(ruleChange.OldRule, reportType);
                case 'I': return DisplayDestination(ruleChange.NewRule, reportType);
                case 'C': return DisplayJsonArray("destination", DisplayArrayDiff(ListNetworkLocations(ruleChange.OldRule, reportType, false),
                    ListNetworkLocations(ruleChange.NewRule, reportType, false)));
                default: return "";
            }
        }

        public string DisplayServiceNegated(RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return DisplayServiceNegated(ruleChange.OldRule.ServiceNegated);
                case 'I': return DisplayServiceNegated(ruleChange.NewRule.ServiceNegated);
                case 'C': return ruleChange.OldRule.ServiceNegated == ruleChange.NewRule.ServiceNegated ?
                    DisplayServiceNegated(ruleChange.NewRule.ServiceNegated) :
                    DisplayJsonString("service negated", DisplayDiff(ruleChange.OldRule.ServiceNegated.ToString().ToLower(), ruleChange.NewRule.ServiceNegated.ToString().ToLower()));
                default: return "";
            }
        }

        public string DisplayServices(RuleChange ruleChange, ReportType reportType)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return DisplayServices(ruleChange.OldRule, reportType);
                case 'I': return DisplayServices(ruleChange.NewRule, reportType);
                case 'C': return DisplayJsonArray("service", DisplayArrayDiff(ListServices(ruleChange.OldRule, reportType), 
                    ListServices(ruleChange.NewRule, reportType)));
                default: return "";
            }
        }

        public string DisplayAction(RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return DisplayAction(ruleChange.OldRule.Action);
                case 'I': return DisplayAction(ruleChange.NewRule.Action);
                case 'C': return DisplayAction(DisplayDiff(ruleChange.OldRule.Action, ruleChange.NewRule.Action));
                default: return "";
            }
        }

        public string DisplayTrack(RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return DisplayTrack(ruleChange.OldRule.Track);
                case 'I': return DisplayTrack(ruleChange.NewRule.Track);
                case 'C': return DisplayTrack(DisplayDiff(ruleChange.OldRule.Track, ruleChange.NewRule.Track));
                default: return "";
            }
        }

        public string DisplayEnabled(RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return DisplayEnabled(ruleChange.OldRule.Disabled);
                case 'I': return DisplayEnabled(ruleChange.NewRule.Disabled);
                case 'C': return ruleChange.OldRule.Disabled == ruleChange.NewRule.Disabled ?
                    DisplayEnabled(ruleChange.NewRule.Disabled) :
                    DisplayJsonString("disabled", DisplayDiff(ruleChange.OldRule.Disabled.ToString().ToLower(), ruleChange.NewRule.Disabled.ToString().ToLower()));
                default: return "";
            }
        }
      
        public string DisplayEnforcingGateways(RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return DisplayEnforcingGateways(ruleChange.OldRule.EnforcingGateways);
                case 'I': return DisplayEnforcingGateways(ruleChange.NewRule.EnforcingGateways);
                case 'C': return DisplayJsonArray("EnforcingGateways", DisplayArrayDiff(ListEnforcingGateways(ruleChange.OldRule.EnforcingGateways),
                    ListEnforcingGateways(ruleChange.NewRule.EnforcingGateways)));
                default: return "";
            }
        }

        public string DisplayUid(RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return DisplayUid(ruleChange.OldRule.Uid);
                case 'I': return DisplayUid(ruleChange.NewRule.Uid);
                case 'C': return DisplayUid(DisplayDiff(ruleChange.OldRule.Uid, ruleChange.NewRule.Uid));
                default: return "";
            }
        }
        public string DisplayUid(ObjectChange objectChange)
        {
            switch (objectChange.ChangeAction)
            {
                case 'D': return DisplayUid(objectChange.OldObject.Uid);
                case 'I': return DisplayUid(objectChange.NewObject.Uid);
                case 'C': return DisplayUid(DisplayDiff(objectChange.OldObject.Uid, objectChange.NewObject.Uid));
                default: return "";
            }
        }
        public string DisplayUid(ServiceChange serviceChange)
        {
            switch (serviceChange.ChangeAction)
            {
                case 'D': return DisplayUid(serviceChange.OldService.Uid);
                case 'I': return DisplayUid(serviceChange.NewService.Uid);
                case 'C': return DisplayUid(DisplayDiff(serviceChange.OldService.Uid, serviceChange.NewService.Uid));
                default: return "";
            }
        }

        public string DisplayComment(RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return DisplayComment(ruleChange.OldRule.Comment);
                case 'I': return DisplayComment(ruleChange.NewRule.Comment);
                case 'C': return DisplayComment(DisplayDiff(ruleChange.OldRule.Comment, ruleChange.NewRule.Comment));
                default: return "";
            }
        }
        public string DisplayComment(ObjectChange objectChange)
        {
            switch (objectChange.ChangeAction)
            {
                case 'D': return DisplayComment(objectChange.OldObject.Comment);
                case 'I': return DisplayComment(objectChange.NewObject.Comment);
                case 'C': return DisplayComment(DisplayDiff(objectChange.OldObject.Comment, objectChange.NewObject.Comment));
                default: return "";
            }
        }
        public string DisplayComment(ServiceChange serviceChange)
        {
            switch (serviceChange.ChangeAction)
            {
                case 'D': return DisplayComment(serviceChange.OldService.Comment);
                case 'I': return DisplayComment(serviceChange.NewService.Comment);
                case 'C': return DisplayComment(DisplayDiff(serviceChange.OldService.Comment, serviceChange.NewService.Comment));
                default: return "";
            }
        }
        public string DisplayComment(UserChange userChange)
        {
            switch (userChange.ChangeAction)
            {
                case 'D': return DisplayComment(userChange.OldUser.Comment);
                case 'I': return DisplayComment(userChange.NewUser.Comment);
                case 'C': return DisplayComment(DisplayDiff(userChange.OldUser.Comment, userChange.NewUser.Comment));
                default: return "";
            }
        }

        public string DisplayObjectType(ObjectChange objectChange)
        {
            switch (objectChange.ChangeAction)
            {
                case 'D': return DisplayObjectType(objectChange.OldObject.Type.Name);
                case 'I': return DisplayObjectType(objectChange.NewObject.Type.Name);
                case 'C': return DisplayObjectType(DisplayDiff(objectChange.OldObject.Type.Name, objectChange.NewObject.Type.Name));
                default: return "";
            }
        }
        public string DisplayObjectType(ServiceChange serviceChange)
        {
            switch (serviceChange.ChangeAction)
            {
                case 'D': return DisplayObjectType(serviceChange.OldService.Type.Name);
                case 'I': return DisplayObjectType(serviceChange.NewService.Type.Name);
                case 'C': return DisplayObjectType(DisplayDiff(serviceChange.OldService.Type.Name, serviceChange.NewService.Type.Name));
                default: return "";
            }
        }

        public string DisplayObjectIP(ObjectChange objectChange)
        {
            switch (objectChange.ChangeAction)
            {
                case 'D': return DisplayObjectIP(NwObjDisplay.DisplayIp(objectChange.OldObject.IP, objectChange.OldObject.IpEnd, true));
                case 'I': return DisplayObjectIP(NwObjDisplay.DisplayIp(objectChange.NewObject.IP, objectChange.NewObject.IpEnd, true));
                case 'C': return DisplayObjectIP(DisplayDiff(NwObjDisplay.DisplayIp(objectChange.OldObject.IP, objectChange.OldObject.IpEnd, true), NwObjDisplay.DisplayIp(objectChange.NewObject.IP, objectChange.NewObject.IpEnd, true)));
                default: return "";
            }
        }

        public string DisplayServiceProtocol(ServiceChange serviceChange)
        {
            switch (serviceChange.ChangeAction)
            {
                case 'D': return DisplayServiceProtocol(serviceChange.OldService.Protocol!.Name);
                case 'I': return DisplayServiceProtocol(serviceChange.NewService.Protocol!.Name);
                case 'C': return DisplayServiceProtocol(DisplayDiff(serviceChange.OldService.Protocol!.Name, serviceChange.NewService.Protocol!.Name));
                default: return "";
            }
        }
        public string DisplayServicePort(ServiceChange serviceChange)
        {
            switch (serviceChange.ChangeAction)
            {
                case 'D': return DisplayServicePort(DisplayBase.DisplayPort(serviceChange.OldService.DestinationPort, serviceChange.OldService.DestinationPortEnd, true));
                case 'I': return DisplayServicePort(DisplayBase.DisplayPort(serviceChange.NewService.DestinationPort, serviceChange.NewService.DestinationPortEnd, true));
                case 'C': return DisplayServicePort(DisplayDiff(DisplayBase.DisplayPort(serviceChange.OldService.DestinationPort, serviceChange.OldService.DestinationPortEnd, true), DisplayBase.DisplayPort(serviceChange.NewService.DestinationPort, serviceChange.NewService.DestinationPortEnd, true)));
                default: return "";
            }
        }

        public string DisplayObjectMemberNames(ObjectChange objectChange)
        {
            switch (objectChange.ChangeAction)
            {
                case 'D': return DisplayobjectMemberNames(objectChange.OldObject.MemberNamesAsJson());
                case 'I': return DisplayobjectMemberNames(objectChange.NewObject.MemberNamesAsJson());
                case 'C': return DisplayobjectMemberNames(DisplayDiff(objectChange.OldObject.MemberNamesAsJson(), objectChange.NewObject.MemberNamesAsJson()));
                default: return "";
            }
        }
        public string DisplayObjectMemberNames(ServiceChange serviceChange)
        {
            switch (serviceChange.ChangeAction)
            {
                case 'D': return DisplayobjectMemberNames(serviceChange.OldService.MemberNamesAsJson());
                case 'I': return DisplayobjectMemberNames(serviceChange.NewService.MemberNamesAsJson());
                case 'C': return DisplayobjectMemberNames(DisplayDiff(serviceChange.OldService.MemberNamesAsJson(), serviceChange.NewService.MemberNamesAsJson()));
                default: return "";
            }
        }

        private string? DisplayDiff(string? oldElement, string? newElement)
        {
            if (oldElement == newElement)
            {
                return oldElement;
            }
            else
            {
                return (oldElement != null && oldElement.Length > 0 ? $"{userConfig.GetText("deleted")}: {oldElement}{(newElement != null && newElement.Length > 0 ? ", " : "")}" : "")
                    + (newElement != null && newElement.Length > 0 ?$"{userConfig.GetText("added")}: {newElement}" : "");
            }
        }

        private string DisplayArrayDiff(string oldElement, string newElement)
        {
            if (oldElement == newElement)
            {
                return oldElement;
            }
            else
            {
                List<string> unchanged = new List<string>();
                List<string> added = new List<string>();
                List<string> deleted = new List<string>();

                oldElement = oldElement.Replace("\"", "");
                newElement = newElement.Replace("\"", "");
                AnalyzeElements(oldElement, newElement, ref unchanged, ref deleted, ref added);

                return string.Join(",", Array.ConvertAll(unchanged.ToArray(), elem => Quote(elem))) + (unchanged.Count > 0 && (deleted.Count > 0 || added.Count > 0 ) ? "," : "")
                    + (deleted.Count > 0 ? string.Join(",", Array.ConvertAll(deleted.ToArray(), elem => Quote($"{userConfig.GetText("deleted")}: {elem}"))) : "") + (deleted.Count > 0 && added.Count > 0 ? "," : "")
                    + (added.Count > 0 ? string.Join(",", Array.ConvertAll(added.ToArray(), elem => Quote($"{userConfig.GetText("added")}: {elem}"))) : "");
            }
        }
    }
}
