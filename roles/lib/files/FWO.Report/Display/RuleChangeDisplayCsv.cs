using FWO.Basics;
using FWO.Data;
using FWO.Config.Api;
using FWO.Report.Filter;

namespace FWO.Ui.Display
{
    public class RuleChangeDisplayCsv : RuleDisplayCsv
    {
        public RuleChangeDisplayCsv(UserConfig userConfig) : base(userConfig)
        { }

        public string DisplayChangeTime(RuleChange ruleChange)
        {
            return OutputCsv(ruleChange.ChangeImport.Time.ToString());
        }

        public string DisplayChangeTime(ObjectChange objectChange)
        {
            return OutputCsv(objectChange.ChangeImport.Time.ToString());
        }
        public string DisplayChangeTime(ServiceChange serviceChange)
        {
            return OutputCsv(serviceChange.ChangeImport.Time.ToString());
        }
        public string DisplayChangeTime(UserChange userChange)
        {
            return OutputCsv(userChange.ChangeImport.Time.ToString());
        }

        public string DisplayChangeAction(RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'I': return OutputCsv(userConfig.GetText("rule_added"));
                case 'D': return OutputCsv(userConfig.GetText("rule_deleted"));
                case 'C': return OutputCsv(userConfig.GetText("rule_modified"));
                default: return ",";
            }
        }
        public string DisplayChangeAction(ObjectChange objectChange)
        {
            switch (objectChange.ChangeAction)
            {
                case 'I': return OutputCsv(userConfig.GetText("network_object_added"));
                case 'D': return OutputCsv(userConfig.GetText("network_object_deleted"));
                case 'C': return OutputCsv(userConfig.GetText("network_object_modified"));
                default: return ",";
            }
        }

        public string DisplayChangeAction(ServiceChange serviceChange)
        {
            switch (serviceChange.ChangeAction)
            {
                case 'I': return OutputCsv(userConfig.GetText("service_added"));
                case 'D': return OutputCsv(userConfig.GetText("service_deleted"));
                case 'C': return OutputCsv(userConfig.GetText("service_modified"));
                default: return ",";
            }
        }

        public string DisplayChangeAction(UserChange userChange)
        {
            switch (userChange.ChangeAction)
            {
                case 'I': return OutputCsv(userConfig.GetText("user_added"));
                case 'D': return OutputCsv(userConfig.GetText("user_deleted"));
                case 'C': return OutputCsv(userConfig.GetText("user_modified"));
                default: return ",";
            }
        }

        public string DisplayName(RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return OutputCsv(DisplayName(ruleChange.OldRule));
                case 'I': return OutputCsv(DisplayName(ruleChange.NewRule));
                case 'C': return OutputCsv(DisplayDiff(DisplayName(ruleChange.OldRule), DisplayName(ruleChange.NewRule)));
                default: return ",";
            }
        }

        public string DisplayName(ObjectChange objectChange)
        {
            switch (objectChange.ChangeAction)
            {
                case 'D': return OutputCsv(DisplayName(objectChange.OldObject));
                case 'I': return OutputCsv(DisplayName(objectChange.NewObject));
                case 'C': return OutputCsv(DisplayDiff(DisplayName(objectChange.OldObject), DisplayName(objectChange.NewObject)));
                default: return ",";
            }
        }
        public string DisplayName(ServiceChange serviceChange)
        {
            switch (serviceChange.ChangeAction)
            {
                case 'D': return OutputCsv(DisplayName(serviceChange.OldService));
                case 'I': return OutputCsv(DisplayName(serviceChange.NewService));
                case 'C': return OutputCsv(DisplayDiff(DisplayName(serviceChange.OldService), DisplayName(serviceChange.NewService)));
                default: return ",";
            }
        }
        public string DisplayName(UserChange userChange)
        {
            switch (userChange.ChangeAction)
            {
                case 'D': return OutputCsv(DisplayName(userChange.OldUser));
                case 'I': return OutputCsv(DisplayName(userChange.NewUser));
                case 'C': return OutputCsv(DisplayDiff(DisplayName(userChange.OldUser), DisplayName(userChange.NewUser)));
                default: return ",";
            }
        }

        public string DisplaySourceZone(RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return OutputCsv(ListNetworkZones(ruleChange.OldRule.RuleFromZones.Select(z => z.Content).ToArray()));
                case 'I': return OutputCsv(ListNetworkZones(ruleChange.NewRule.RuleFromZones.Select(z => z.Content).ToArray()));
                case 'C': return OutputCsv(DisplayDiff(ListNetworkZones(ruleChange.OldRule.RuleFromZones.Select(z => z.Content).ToArray()), ListNetworkZones(ruleChange.NewRule.RuleFromZones.Select(z => z.Content).ToArray())));
                default: return ",";
            }
        }

        public string DisplaySource(RuleChange ruleChange, ReportType reportType)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return OutputCsv(DisplaySource(ruleChange.OldRule, reportType));
                case 'I': return OutputCsv(DisplaySource(ruleChange.NewRule, reportType));
                case 'C': return OutputCsv(DisplayArrayDiff(DisplaySource(ruleChange.OldRule, reportType), DisplaySource(ruleChange.NewRule, reportType), ruleChange.OldRule.SourceNegated, ruleChange.NewRule.SourceNegated));
                default: return ",";
            }
        }

        public string DisplayDestinationZone(RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return OutputCsv(ListNetworkZones(ruleChange.OldRule.RuleToZones.Select(z => z.Content).ToArray()));
                case 'I': return OutputCsv(ListNetworkZones(ruleChange.NewRule.RuleToZones.Select(z => z.Content).ToArray()));
                case 'C': return OutputCsv(DisplayDiff(ListNetworkZones(ruleChange.OldRule.RuleToZones.Select(z => z.Content).ToArray()), ListNetworkZones(ruleChange.NewRule.RuleToZones.Select(z => z.Content).ToArray())));
                default: return ",";
            }
        }

        public string DisplayDestination(RuleChange ruleChange, ReportType reportType)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return OutputCsv(DisplayDestination(ruleChange.OldRule, reportType));
                case 'I': return OutputCsv(DisplayDestination(ruleChange.NewRule, reportType));
                case 'C': return OutputCsv(DisplayArrayDiff(DisplayDestination(ruleChange.OldRule, reportType), DisplayDestination(ruleChange.NewRule, reportType), ruleChange.OldRule.DestinationNegated, ruleChange.NewRule.DestinationNegated));
                default: return ",";
            }
        }

        public string DisplayServices(RuleChange ruleChange, ReportType reportType)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return OutputCsv(DisplayServices(ruleChange.OldRule, reportType));
                case 'I': return OutputCsv(DisplayServices(ruleChange.NewRule, reportType));
                case 'C': return OutputCsv(DisplayArrayDiff(DisplayServices(ruleChange.OldRule, reportType), DisplayServices(ruleChange.NewRule, reportType), ruleChange.OldRule.ServiceNegated, ruleChange.NewRule.ServiceNegated));
                default: return ",";
            }
        }

        public string DisplayAction(RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return OutputCsv(DisplayAction(ruleChange.OldRule));
                case 'I': return OutputCsv(DisplayAction(ruleChange.NewRule));
                case 'C': return OutputCsv(DisplayDiff(DisplayAction(ruleChange.OldRule), DisplayAction(ruleChange.NewRule)));
                default: return ",";
            }
        }

        public string DisplayTrack(RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return OutputCsv(DisplayTrack(ruleChange.OldRule));
                case 'I': return OutputCsv(DisplayTrack(ruleChange.NewRule));
                case 'C': return OutputCsv(DisplayDiff(DisplayTrack(ruleChange.OldRule), DisplayTrack(ruleChange.NewRule)));
                default: return ",";
            }
        }

        public string DisplayEnabled(RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return OutputCsv(DisplayEnabled(ruleChange.OldRule));
                case 'I': return OutputCsv(DisplayEnabled(ruleChange.NewRule));
                case 'C': return OutputCsv(DisplayDiff(DisplayEnabled(ruleChange.OldRule), DisplayEnabled(ruleChange.NewRule)));
                default: return ",";
            }
        }

        public string DisplayenforcingDevice(RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return OutputCsv(DisplayEnforcingGateways(ruleChange.OldRule));
                case 'I': return OutputCsv(DisplayEnforcingGateways(ruleChange.NewRule));
                case 'C': return OutputCsv(DisplayDiff(DisplayEnforcingGateways(ruleChange.OldRule), DisplayEnforcingGateways(ruleChange.NewRule)));
                default: return ",";
            }
        }

        public string DisplayUid(RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return OutputCsv(DisplayUid(ruleChange.OldRule));
                case 'I': return OutputCsv(DisplayUid(ruleChange.NewRule));
                case 'C': return OutputCsv(DisplayDiff(DisplayUid(ruleChange.OldRule), DisplayUid(ruleChange.NewRule)));
                default: return ",";
            }
        }
        public string DisplayUid(ObjectChange objectChange)
        {
            switch (objectChange.ChangeAction)
            {
                case 'D': return OutputCsv(DisplayUid(objectChange.OldObject));
                case 'I': return OutputCsv(DisplayUid(objectChange.NewObject));
                case 'C': return OutputCsv(DisplayDiff(DisplayUid(objectChange.OldObject), DisplayUid(objectChange.NewObject)));
                default: return ",";
            }
        }
        public string DisplayUid(ServiceChange serviceChange)
        {
            switch (serviceChange.ChangeAction)
            {
                case 'D': return OutputCsv(DisplayUid(serviceChange.OldService));
                case 'I': return OutputCsv(DisplayUid(serviceChange.NewService));
                case 'C': return OutputCsv(DisplayDiff(DisplayUid(serviceChange.OldService), DisplayUid(serviceChange.NewService)));
                default: return ",";
            }
        }

        public string DisplayComment(RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return OutputCsv(DisplayComment(ruleChange.OldRule));
                case 'I': return OutputCsv(DisplayComment(ruleChange.NewRule));
                case 'C': return OutputCsv(DisplayDiff(DisplayComment(ruleChange.OldRule), DisplayComment(ruleChange.NewRule)));
                default: return "";
            }
        }

        public string DisplayComment(ObjectChange objectChange)
        {
            switch (objectChange.ChangeAction)
            {
                case 'D': return OutputCsv(DisplayComment(objectChange.OldObject));
                case 'I': return OutputCsv(DisplayComment(objectChange.NewObject));
                case 'C': return OutputCsv(DisplayDiff(DisplayComment(objectChange.OldObject), DisplayComment(objectChange.NewObject)));
                default: return "";
            }
        }
        public string DisplayComment(ServiceChange serviceChange)
        {
            switch (serviceChange.ChangeAction)
            {
                case 'D': return OutputCsv(DisplayComment(serviceChange.OldService));
                case 'I': return OutputCsv(DisplayComment(serviceChange.NewService));
                case 'C': return OutputCsv(DisplayDiff(DisplayComment(serviceChange.OldService), DisplayComment(serviceChange.NewService)));
                default: return "";
            }
        }

        public string DisplayComment(UserChange userChange)
        {
            switch (userChange.ChangeAction)
            {
                case 'D': return OutputCsv(DisplayComment(userChange.OldUser));
                case 'I': return OutputCsv(DisplayComment(userChange.NewUser));
                case 'C': return OutputCsv(DisplayDiff(DisplayComment(userChange.OldUser), DisplayComment(userChange.NewUser)));
                default: return "";
            }
        }

        public string DisplayObjectType(ObjectChange objectChange)
        {
            switch (objectChange.ChangeAction)
            {
                case 'D': return OutputCsv(objectChange.OldObject.Type.Name);
                case 'I': return OutputCsv(objectChange.NewObject.Type.Name);
                case 'C': return OutputCsv(DisplayDiff(objectChange.OldObject.Type.Name, objectChange.NewObject.Type.Name));
                default: return "";
            }
        }

        public string DisplayObjectIp(ObjectChange objectChange)
        {
            switch (objectChange.ChangeAction)
            {
                case 'D': return OutputCsv(NwObjDisplay.DisplayIp(objectChange.OldObject.IP, objectChange.OldObject.IpEnd, true));
                case 'I': return OutputCsv(NwObjDisplay.DisplayIp(objectChange.NewObject.IP, objectChange.NewObject.IpEnd, true));
                case 'C': return OutputCsv(DisplayDiff(NwObjDisplay.DisplayIp(objectChange.OldObject.IP, objectChange.OldObject.IpEnd, true), NwObjDisplay.DisplayIp(objectChange.NewObject.IP, objectChange.NewObject.IpEnd, true)));
                default: return "";
            }
        }
        public string DisplayObjectMemberNames(ObjectChange objectChange)
        {
            switch (objectChange.ChangeAction)
            {
                case 'D': return OutputCsv(objectChange.OldObject.MemberNamesAsCSV());
                case 'I': return OutputCsv(objectChange.NewObject.MemberNamesAsCSV());
                case 'C': return OutputCsv(DisplayDiff(objectChange.OldObject.MemberNamesAsCSV(), objectChange.NewObject.MemberNamesAsCSV()));
                default: return "";
            }
        }

        public string DisplayServiceMemberNames(ServiceChange serviceChange)
        {
            switch (serviceChange.ChangeAction)
            {
                case 'D': return OutputCsv(serviceChange.OldService.MemberNamesAsHtml());
                case 'I': return OutputCsv(serviceChange.NewService.MemberNamesAsHtml());
                case 'C': return OutputCsv(DisplayDiff(serviceChange.OldService.MemberNamesAsCSV(), serviceChange.NewService.MemberNamesAsCSV()));
                default: return "";
            }
        }

        public string DisplayServiceType(ServiceChange serviceChange)
        {
            switch (serviceChange.ChangeAction)
            {
                case 'D': return OutputCsv(serviceChange.OldService.Type.Name);
                case 'I': return OutputCsv(serviceChange.NewService.Type.Name);
                case 'C': return OutputCsv(DisplayDiff(serviceChange.OldService.Type.Name, serviceChange.NewService.Type.Name));
                default: return "";
            }
        }

        public string DisplayServiceProtocol(ServiceChange serviceChange)
        {
            switch (serviceChange.ChangeAction)
            {
                case 'D': return OutputCsv(serviceChange.OldService.Protocol!.Name);
                case 'I': return OutputCsv(serviceChange.NewService.Protocol!.Name);
                case 'C': return OutputCsv(DisplayDiff(serviceChange.OldService.Protocol!.Name, serviceChange.NewService.Protocol!.Name));
                default: return "";
            }
        }

        public string DisplayServicePort(ServiceChange serviceChange)
        {
            switch (serviceChange.ChangeAction)
            {
                case 'D': return OutputCsv(DisplayBase.DisplayPort(serviceChange.OldService.DestinationPort, serviceChange.OldService.DestinationPortEnd, true));
                case 'I': return OutputCsv(DisplayBase.DisplayPort(serviceChange.NewService.DestinationPort, serviceChange.NewService.DestinationPortEnd, true));
                case 'C': return OutputCsv(DisplayDiff(DisplayBase.DisplayPort(serviceChange.OldService.DestinationPort, serviceChange.OldService.DestinationPortEnd, true), (DisplayBase.DisplayPort(serviceChange.NewService.DestinationPort, serviceChange.NewService.DestinationPortEnd, true))));
                default: return "";
            }
        }

        private string DisplayDiff(string oldElement, string newElement)
        {
            if (oldElement == newElement)
            {
                return oldElement;
            }
            else
            {
                return (oldElement.Length > 0 ? $" {userConfig.GetText("deleted")}: {oldElement}" : "")
                    + (newElement.Length > 0 ? $" {userConfig.GetText("added")}: {newElement}" : "");
            }
        }

        private string DisplayArrayDiff(string oldElement, string newElement, bool oldNegated, bool newNegated)
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
                if(oldNegated != newNegated)
                {
                    deleted.Add(oldElement);
                    added.Add(newElement);
                }
                else
                {
                    AnalyzeElements(oldElement, newElement, ref unchanged, ref deleted, ref added);
                }

                return string.Join(" ", unchanged) 
                    + (deleted.Count > 0 ? $" {userConfig.GetText("deleted")}: {string.Join(",", deleted)}" : "")
                    + (added.Count > 0 ? $" {userConfig.GetText("added")}: {string.Join(",", added)}" : "");
            }
        }
    }
}
