using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Logging;
using FWO.Report;
using Microsoft.AspNetCore.Components;
using System.Data;
using System.Data.Common;

namespace FWO.Ui.Display
{
    public class RuleChangeDisplayHtml : RuleDisplayHtml
    {
        public RuleChangeDisplayHtml(UserConfig userConfig) : base(userConfig)
        { }

        public string DisplayChangeTime(RuleChange ruleChange)
        {
            return ruleChange.ChangeImport.Time.ToString();
        }

        public string DisplayChangeTime(ObjectChange objectChange)
        {
            return objectChange.ChangeImport.Time.ToString();
        }

        public string DisplayChangeTime(ServiceChange serviceChange)
        {
            return serviceChange.ChangeImport.Time.ToString();
        }
        public string DisplayChangeTime(UserChange userChange)
        {
            return userChange.ChangeImport.Time.ToString();
        }

        public string DisplayChangeAction(RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'I': return userConfig.GetText("rule_added");
                case 'D': return userConfig.GetText("rule_deleted");
                case 'C': return userConfig.GetText("rule_modified");
                default: ThrowErrorUnknowChangeAction(ruleChange.ChangeAction); return "";
            }
        }
        public string DisplayChangeAction(ObjectChange objectChange)
        {
            switch (objectChange.ChangeAction)
            {
                case 'I': return userConfig.GetText("network_object_added");
                case 'D': return userConfig.GetText("network_object_deleted");
                case 'C': return userConfig.GetText("network_object_modified");
                default: ThrowErrorUnknowChangeAction(objectChange.ChangeAction); return "";
            }
        }
        public string DisplayChangeAction(ServiceChange serviceChange)
        {
            switch (serviceChange.ChangeAction)
            {
                case 'I': return userConfig.GetText("service_added");
                case 'D': return userConfig.GetText("service_deleted");
                case 'C': return userConfig.GetText("service_modified");
                default: ThrowErrorUnknowChangeAction(serviceChange.ChangeAction); return "";
            }
        }
        public string DisplayChangeAction(UserChange userChange)
        {
            switch (userChange.ChangeAction)
            {
                case 'I': return userConfig.GetText("user_added");
                case 'D': return userConfig.GetText("user_deleted");
                case 'C': return userConfig.GetText("user_modified");
                default: ThrowErrorUnknowChangeAction(userChange.ChangeAction); return "";
            }
        }

        public string DisplayName(RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return OutputHtmlDeleted(DisplayName(ruleChange.OldRule));
                case 'I': return OutputHtmlAdded(DisplayName(ruleChange.NewRule));
                case 'C': return DisplayDiff(DisplayName(ruleChange.OldRule), DisplayName(ruleChange.NewRule));
                default: ThrowErrorUnknowChangeAction(ruleChange.ChangeAction); return "";
            }
        }
        public string DisplayName(ObjectChange objectChange)
        {
            switch (objectChange.ChangeAction)
            {
                case 'D': return OutputHtmlDeleted(DisplayName(objectChange.OldObject));
                case 'I': return OutputHtmlAdded(DisplayName(objectChange.NewObject));
                case 'C': return DisplayDiff(DisplayName(objectChange.OldObject), DisplayName(objectChange.NewObject));
                default: ThrowErrorUnknowChangeAction(objectChange.ChangeAction); return "";
            }
        }
        public string DisplayName(ServiceChange serviceChange)
        {
            switch (serviceChange.ChangeAction)
            {
                case 'D': return OutputHtmlDeleted(DisplayName(serviceChange.OldService));
                case 'I': return OutputHtmlAdded(DisplayName(serviceChange.NewService));
                case 'C': return DisplayDiff(DisplayName(serviceChange.OldService), DisplayName(serviceChange.NewService));
                default: ThrowErrorUnknowChangeAction(serviceChange.ChangeAction); return "";
            }
        }
        public string DisplayName(UserChange userChange)
        {
            switch (userChange.ChangeAction)
            {
                case 'D': return OutputHtmlDeleted(DisplayName(userChange.NewUser));
                case 'I': return OutputHtmlAdded(DisplayName(userChange.NewUser));
                case 'C': return DisplayDiff(DisplayName(userChange.OldUser), DisplayName(userChange.NewUser));
                default: ThrowErrorUnknowChangeAction(userChange.ChangeAction); return "";
            }
        }

        public string DisplaySourceZone(RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return OutputHtmlDeleted(DisplaySourceZones(ruleChange.OldRule));
                case 'I': return OutputHtmlAdded(DisplaySourceZones(ruleChange.NewRule));
                case 'C': return DisplayDiff(DisplaySourceZones(ruleChange.OldRule), DisplaySourceZones(ruleChange.NewRule));
                default: ThrowErrorUnknowChangeAction(ruleChange.ChangeAction); return "";
            }
        }

        public string DisplaySource(RuleChange ruleChange, OutputLocation location, ReportType reportType)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return OutputHtmlDeleted(DisplaySource(ruleChange.OldRule, location, reportType, 0, GlobalConst.kStyleDeleted));
                case 'I': return OutputHtmlAdded(DisplaySource(ruleChange.NewRule, location, reportType, 0, GlobalConst.kStyleAdded));
                case 'C': return DisplayArrayDiff(DisplaySource(ruleChange.OldRule, location, reportType),
                                                  DisplaySource(ruleChange.NewRule, location, reportType),
                                                  ruleChange.OldRule.SourceNegated, ruleChange.NewRule.SourceNegated);
                default: ThrowErrorUnknowChangeAction(ruleChange.ChangeAction); return "";
            }
        }

        public string DisplayDestinationZone(RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return OutputHtmlDeleted(DisplayDestinationZones(ruleChange.OldRule));
                case 'I': return OutputHtmlAdded(DisplayDestinationZones(ruleChange.NewRule));
                case 'C': return DisplayDiff(DisplayDestinationZones(ruleChange.OldRule), DisplayDestinationZones(ruleChange.NewRule));
                default: ThrowErrorUnknowChangeAction(ruleChange.ChangeAction); return "";
            }
        }

        public string DisplayDestination(RuleChange ruleChange, OutputLocation location, ReportType reportType)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return OutputHtmlDeleted(DisplayDestination(ruleChange.OldRule, location, reportType, 0, GlobalConst.kStyleDeleted));
                case 'I': return OutputHtmlAdded(DisplayDestination(ruleChange.NewRule, location, reportType, 0, GlobalConst.kStyleAdded));
                case 'C': return DisplayArrayDiff(DisplayDestination(ruleChange.OldRule, location, reportType),
                                                  DisplayDestination(ruleChange.NewRule, location, reportType),
                                                  ruleChange.OldRule.DestinationNegated, ruleChange.NewRule.DestinationNegated);
                default: ThrowErrorUnknowChangeAction(ruleChange.ChangeAction); return "";
            }
        }

        public string DisplayServices(RuleChange ruleChange, OutputLocation location, ReportType reportType)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return OutputHtmlDeleted(DisplayServices(ruleChange.OldRule, location, reportType, 0, GlobalConst.kStyleDeleted));
                case 'I': return OutputHtmlAdded(DisplayServices(ruleChange.NewRule, location, reportType, 0, GlobalConst.kStyleAdded));
                case 'C': return DisplayArrayDiff(DisplayServices(ruleChange.OldRule, location, reportType),
                                                  DisplayServices(ruleChange.NewRule, location, reportType),
                                                  ruleChange.OldRule.ServiceNegated, ruleChange.NewRule.ServiceNegated);
                default: ThrowErrorUnknowChangeAction(ruleChange.ChangeAction); return "";
            }
        }

        public string DisplayAction(RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return OutputHtmlDeleted(DisplayAction(ruleChange.OldRule));
                case 'I': return OutputHtmlAdded(DisplayAction(ruleChange.NewRule));
                case 'C': return DisplayDiff(DisplayAction(ruleChange.OldRule), DisplayAction(ruleChange.NewRule));
                default: ThrowErrorUnknowChangeAction(ruleChange.ChangeAction); return "";
            }
        }

        public string DisplayTrack(RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return OutputHtmlDeleted(DisplayTrack(ruleChange.OldRule));
                case 'I': return OutputHtmlAdded(DisplayTrack(ruleChange.NewRule));
                case 'C': return DisplayDiff(DisplayTrack(ruleChange.OldRule), DisplayTrack(ruleChange.NewRule));
                default: ThrowErrorUnknowChangeAction(ruleChange.ChangeAction); return "";
            }
        }

        public string DisplayEnabled(RuleChange ruleChange, OutputLocation location)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return OutputHtmlDeleted(DisplayEnabled(ruleChange.OldRule, location));
                case 'I': return OutputHtmlAdded(DisplayEnabled(ruleChange.NewRule, location));
                case 'C': return DisplayDiff(DisplayEnabled(ruleChange.OldRule, location), DisplayEnabled(ruleChange.NewRule, location));
                default: ThrowErrorUnknowChangeAction(ruleChange.ChangeAction); return "";
            }
        }

        public string DisplayUid(RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return OutputHtmlDeleted(DisplayUid(ruleChange.OldRule));
                case 'I': return OutputHtmlAdded(DisplayUid(ruleChange.NewRule));
                case 'C': return DisplayDiff(DisplayUid(ruleChange.OldRule), DisplayUid(ruleChange.NewRule));
                default: ThrowErrorUnknowChangeAction(ruleChange.ChangeAction); return "";
            }
        }
        public string DisplayUid(ObjectChange objectChange)
        {
            switch (objectChange.ChangeAction)
            {
                case 'D': return OutputHtmlDeleted(objectChange.OldObject.Uid);
                case 'I': return OutputHtmlAdded(objectChange.NewObject.Uid);
                case 'C': return DisplayDiff(objectChange.OldObject.Uid, objectChange.NewObject.Uid);
                default: ThrowErrorUnknowChangeAction(objectChange.ChangeAction); return "";
            }
        }
        public string DisplayUid(ServiceChange serviceChange)
        {
            switch (serviceChange.ChangeAction)
            {
                case 'D': return OutputHtmlDeleted(serviceChange.OldService.Uid);
                case 'I': return OutputHtmlAdded(serviceChange.NewService.Uid);
                case 'C': return DisplayDiff(serviceChange.OldService.Uid, serviceChange.NewService.Uid);
                default: ThrowErrorUnknowChangeAction(serviceChange.ChangeAction); return "";
            }
        }

        public string DisplayEnforcingGateways(RuleChange ruleChange, OutputLocation location, ReportType reportType)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return OutputHtmlDeleted(DisplayEnforcingGateways(ruleChange.OldRule, location, reportType));
                case 'I': return OutputHtmlAdded(DisplayEnforcingGateways(ruleChange.NewRule, location, reportType));
                case 'C': return DisplayDiff(DisplayEnforcingGateways(ruleChange.OldRule, location, reportType), DisplayEnforcingGateways(ruleChange.NewRule, location, reportType));
                default: ThrowErrorUnknowChangeAction(ruleChange.ChangeAction); return "";
            }
        }

        public string DisplayComment(RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return OutputHtmlDeleted(DisplayComment(ruleChange.OldRule));
                case 'I': return OutputHtmlAdded(DisplayComment(ruleChange.NewRule));
                case 'C': return DisplayDiff(DisplayComment(ruleChange.OldRule), DisplayComment(ruleChange.NewRule));
                default: ThrowErrorUnknowChangeAction(ruleChange.ChangeAction); return "";
            }
        }
        public string DisplayComment(ObjectChange objectChange)
        {
            switch (objectChange.ChangeAction)
            {
                case 'D': return OutputHtmlDeleted(objectChange.OldObject.Comment);
                case 'I': return OutputHtmlAdded(objectChange.NewObject.Comment);
                case 'C': return DisplayDiff(objectChange.OldObject.Comment, objectChange.NewObject.Comment);
                default: ThrowErrorUnknowChangeAction(objectChange.ChangeAction); return "";
            }
        }
        public string DisplayComment(ServiceChange serviceChange)
        {
            switch (serviceChange.ChangeAction)
            {
                case 'D': return OutputHtmlDeleted(serviceChange.OldService.Comment);
                case 'I': return OutputHtmlAdded(serviceChange.NewService.Comment);
                case 'C': return DisplayDiff(serviceChange.OldService.Comment, serviceChange.NewService.Comment);
                default: ThrowErrorUnknowChangeAction(serviceChange.ChangeAction); return "";
            }
        }
        public string DisplayComment(UserChange userChange)
        {
            switch (userChange.ChangeAction)
            {
                case 'D': return OutputHtmlDeleted(userChange.OldUser.Comment);
                case 'I': return OutputHtmlAdded(userChange.NewUser.Comment);
                case 'C': return DisplayDiff(userChange.OldUser.Comment, userChange.NewUser.Comment);
                default: ThrowErrorUnknowChangeAction(userChange.ChangeAction); return "";
            }
        }

        public string DisplayStyle(RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return GlobalConst.kStyleDeleted;
                case 'I': return GlobalConst.kStyleAdded;
                case 'C': return "";
                default: ThrowErrorUnknowChangeAction(ruleChange.ChangeAction); return "";
            }
        }
        public string DisplayStyle(ObjectChange objectChange)
        {
            switch (objectChange.ChangeAction)
            {
                case 'D': return GlobalConst.kStyleDeleted;
                case 'I': return GlobalConst.kStyleAdded;
                case 'C': return "";
                default: ThrowErrorUnknowChangeAction(objectChange.ChangeAction); return "";
            }
        }
        public string DisplayStyle(ServiceChange serviceChange)
        {
            switch (serviceChange.ChangeAction)
            {
                case 'D': return GlobalConst.kStyleDeleted;
                case 'I': return GlobalConst.kStyleAdded;
                case 'C': return "";
                default: ThrowErrorUnknowChangeAction(serviceChange.ChangeAction); return "";
            }
        }
        public string DisplayStyle(UserChange userChange)
        {
            switch (userChange.ChangeAction)
            {
                case 'D': return GlobalConst.kStyleDeleted;
                case 'I': return GlobalConst.kStyleAdded;
                case 'C': return "";
                default: ThrowErrorUnknowChangeAction(userChange.ChangeAction); return "";
            }
        }

        public string DisplayObjectIP(ObjectChange objectChange)
        {
            switch (objectChange.ChangeAction)
            {
                case 'D': return OutputHtmlDeleted(NwObjDisplay.DisplayIp(objectChange.OldObject.IP,objectChange.OldObject.IpEnd,true));
                case 'I': return OutputHtmlAdded(NwObjDisplay.DisplayIp(objectChange.NewObject.IP, objectChange.NewObject.IpEnd, true));
                case 'C': return DisplayDiff(NwObjDisplay.DisplayIp(objectChange.OldObject.IP, objectChange.OldObject.IpEnd, true), NwObjDisplay.DisplayIp(objectChange.NewObject.IP, objectChange.NewObject.IpEnd, true));
                default: ThrowErrorUnknowChangeAction(objectChange.ChangeAction); return "";
            }
        }
        public string DisplayObjectType(ObjectChange objectChange)
        {
            switch (objectChange.ChangeAction)
            {
                case 'D': return OutputHtmlDeleted(objectChange.OldObject.Type.Name);
                case 'I': return OutputHtmlAdded(objectChange.NewObject.Type.Name);
                case 'C': return DisplayDiff(objectChange.OldObject.Type.Name, objectChange.NewObject.Type.Name);
                default: ThrowErrorUnknowChangeAction(objectChange.ChangeAction); return "";
            }
        }
        public string DisplayServiceType(ServiceChange serviceChange)
        {
            switch (serviceChange.ChangeAction)
            {
                case 'D': return OutputHtmlDeleted(serviceChange.OldService.Type.Name);
                case 'I': return OutputHtmlAdded(serviceChange.NewService.Type.Name);
                case 'C': return DisplayDiff(serviceChange.OldService.Type.Name, serviceChange.NewService.Type.Name);
                default: ThrowErrorUnknowChangeAction(serviceChange.ChangeAction); return "";
            }
        }

        public string DisplayObjectMemberNames(ObjectChange objectChange)
        {
            switch (objectChange.ChangeAction)
            {
                case 'D': return OutputHtmlDeleted(objectChange.OldObject.MemberNamesAsHtml());
                case 'I': return OutputHtmlAdded(objectChange.NewObject.MemberNamesAsHtml());
                case 'C': return DisplayDiff(objectChange.OldObject.MemberNamesAsHtml(), objectChange.NewObject.MemberNamesAsHtml());
                default: ThrowErrorUnknowChangeAction(objectChange.ChangeAction); return "";
            }
        }
        public string DisplayServiceMemberNames(ServiceChange serviceChange)
        {
            switch (serviceChange.ChangeAction)
            {
                case 'D': return OutputHtmlDeleted(serviceChange.OldService.MemberNamesAsHtml());
                case 'I': return OutputHtmlAdded(serviceChange.NewService.MemberNamesAsHtml());
                case 'C': return DisplayDiff(serviceChange.OldService.MemberNamesAsHtml(), serviceChange.NewService.MemberNamesAsHtml());
                default: ThrowErrorUnknowChangeAction(serviceChange.ChangeAction); return "";
            }
        }
        public string DisplayServiceProtocol(ServiceChange serviceChange)
        {
            switch (serviceChange.ChangeAction)
            {
                case 'D': return OutputHtmlDeleted(serviceChange.OldService.Protocol!.Name);
                case 'I': return OutputHtmlAdded(serviceChange.NewService.Protocol!.Name);
                case 'C': return DisplayDiff(serviceChange.OldService.Protocol!.Name, serviceChange.NewService.Protocol!.Name);
                default: ThrowErrorUnknowChangeAction(serviceChange.ChangeAction); return "";
            }
        }

        public string DisplayServicePort(ServiceChange serviceChange)
        {
            switch (serviceChange.ChangeAction)
            {
                case 'D': return OutputHtmlDeleted(DisplayBase.DisplayPort(serviceChange.OldService.DestinationPort, serviceChange.OldService.DestinationPortEnd,true));
                case 'I': return OutputHtmlAdded(DisplayBase.DisplayPort(serviceChange.NewService.DestinationPort, serviceChange.NewService.DestinationPortEnd, true));
                case 'C': return DisplayDiff(DisplayBase.DisplayPort(serviceChange.OldService.DestinationPort, serviceChange.OldService.DestinationPortEnd, true), (DisplayBase.DisplayPort(serviceChange.NewService.DestinationPort, serviceChange.NewService.DestinationPortEnd, true)));
                default: ThrowErrorUnknowChangeAction(serviceChange.ChangeAction); return "";
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
                return (oldElement.Length > 0 ? $"{userConfig.GetText("deleted")}: <p style=\"{GlobalConst.kStyleDeleted}\">{oldElement}<br></p>" : "")
                    + (newElement.Length > 0 ? $"{userConfig.GetText("added")}: <p style=\"{GlobalConst.kStyleAdded}\">{newElement}</p>" : "");
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
                oldElement = oldElement.Replace("<p>", "");
                oldElement = oldElement.Replace("</p>", "");
                oldElement = oldElement.Replace("\r\n", "");
                newElement = newElement.Replace("<p>", "");
                newElement = newElement.Replace("</p>", "");
                newElement = newElement.Replace("\r\n", "");
                List<string> unchanged = [];
                List<string> added = [];
                List<string> deleted = [];

                if(oldNegated != newNegated)
                {
                    deleted.Add(SetStyle(oldElement, GlobalConst.kStyleDeleted));
                    added.Add(SetStyle(newElement, GlobalConst.kStyleAdded));
                }
                else
                {
                    string[] separatingStrings = ["<br>"];
                    string[] oldAr = oldElement.Split(separatingStrings, System.StringSplitOptions.RemoveEmptyEntries);
                    string[] newAr = newElement.Split(separatingStrings, System.StringSplitOptions.RemoveEmptyEntries);

                    foreach (var item in oldAr)
                    {
                        if (newAr.Contains(item))
                        {
                            unchanged.Add(item);
                        }
                        else
                        {
                            deleted.Add(SetStyle(item, GlobalConst.kStyleDeleted));
                        }
                    }
                    foreach (var item in newAr)
                    {
                        if (!oldAr.Contains(item))
                        {
                            added.Add(SetStyle(item, GlobalConst.kStyleAdded));
                        }
                    }
                }

                return (unchanged.Count > 0 ? $"<p>{string.Join("<br>", unchanged)}<br></p>" : "")
                       + (deleted.Count > 0 ? $"{userConfig.GetText("deleted")}: <p style=\"{GlobalConst.kStyleDeleted}\">{string.Join("<br>", deleted)}<br></p>" : "")
                       + (added.Count > 0 ? $"{userConfig.GetText("added")}: <p style=\"{GlobalConst.kStyleAdded}\">{string.Join("<br>", added)}</p>" : "");
            }
        }
        
        private static string OutputHtmlDeleted(string? input)
        {
            return  input != null && input != "" ? $"<p style=\"{GlobalConst.kStyleDeleted}\">{input}</p>" : "";
        }

        private static string OutputHtmlAdded(string? input)
        {
            return  input != null && input != "" ? $"<p style=\"{GlobalConst.kStyleAdded}\">{input}</p>" : "";
        }

        private static string SetStyle(string input, string style)
        {
            return input.Replace("style=\"\"", $"style=\"{style}\"");
        }

        private static void ThrowErrorUnknowChangeAction(char action)
        {
            Log.WriteError("Unknown Change Action", $"found an unexpected change action [{action}]");
        }
    }
}
