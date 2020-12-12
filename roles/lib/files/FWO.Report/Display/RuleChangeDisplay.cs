using FWO.Api.Data;
using FWO.Logging;
using System.Text.Json.Serialization;

namespace FWO.Ui.Display
{
    public static class RuleChangeDisplay
    {
        // private static StringBuilder result;

        public static string DisplayChangeTime(this RuleChange ruleChange)
        {
            return ruleChange.ChangeImport.Time.ToString();
        }
        public static string DisplayChangeAction(this RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'I':
                    return "rule created";
                case 'D':
                    return "rule deleted";
            }
            return "rule modified";
        }

        public static string DisplayName(this RuleChange ruleChange)
        {
           switch (ruleChange.ChangeAction)
            {
                case 'D': return ruleChange.OldRule.DisplayName();
                case 'I': return ruleChange.NewRule.DisplayName();
                case 'C': return DisplayDiff(ruleChange.OldRule.DisplayName(), ruleChange.NewRule.DisplayName());
                default:
                    Log.WriteWarning("Unknown Change Action", $"found an unexpected change action [{ruleChange.ChangeAction}]");
                    return "";
            }
        }
        public static string DisplaySourceZone(this RuleChange ruleChange)
        {
           switch (ruleChange.ChangeAction)
            {
                case 'D': return ruleChange.OldRule.DisplaySourceZone();
                case 'I': return ruleChange.NewRule.DisplaySourceZone();
                case 'C': return DisplayDiff(ruleChange.OldRule.DisplaySourceZone(), ruleChange.NewRule.DisplaySourceZone());
                default:
                    Log.WriteWarning("Unknown Change Action", $"found an unexpected change action [{ruleChange.ChangeAction}]");
                    return "";
            }
        }

        //          DisplaySource()
        public static string DisplaySource(this RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return ruleChange.OldRule.DisplaySource();
                case 'I': return ruleChange.NewRule.DisplaySource();
                case 'C': return DisplayDiff(ruleChange.OldRule.DisplaySource(), ruleChange.NewRule.DisplaySource());
                default:
                    Log.WriteWarning("Unknown Change Action", $"found an unexpected change action [{ruleChange.ChangeAction}]");
                    return "";
            }
        }

        public static string DisplayDestinationZone(this RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return ruleChange.OldRule.DisplayDestinationZone();
                case 'I': return ruleChange.NewRule.DisplayDestinationZone();
                case 'C': return DisplayDiff(ruleChange.OldRule.DisplayDestinationZone(), ruleChange.NewRule.DisplayDestinationZone());
                default:
                    Log.WriteWarning("Unknown Change Action", $"found an unexpected change action [{ruleChange.ChangeAction}]");
                    return "";
            }
        }

        public static string DisplayDestination(this RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return ruleChange.OldRule.DisplayDestination();
                case 'I': return ruleChange.NewRule.DisplayDestination();
                case 'C': return DisplayDiff(ruleChange.OldRule.DisplayDestination(), ruleChange.NewRule.DisplayDestination());
                default:
                    Log.WriteWarning("Unknown Change Action", $"found an unexpected change action [{ruleChange.ChangeAction}]");
                    return "";
            }
        }
        public static string DisplayService(this RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return ruleChange.OldRule.DisplayService();
                case 'I': return ruleChange.NewRule.DisplayService();
                case 'C': return DisplayDiff(ruleChange.OldRule.DisplayService(), ruleChange.NewRule.DisplayService());
                default:
                    Log.WriteWarning("Unknown Change Action", $"found an unexpected change action [{ruleChange.ChangeAction}]");
                    return "";
            }
        }
        public static string DisplayAction(this RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return ruleChange.OldRule.DisplayAction();
                case 'I': return ruleChange.NewRule.DisplayAction();
                case 'C': return DisplayDiff(ruleChange.OldRule.DisplayAction(), ruleChange.NewRule.DisplayAction());
                default:
                    Log.WriteWarning("Unknown Change Action", $"found an unexpected change action [{ruleChange.ChangeAction}]");
                    return "";
            }
        }
        public static string DisplayTrack(this RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return ruleChange.OldRule.DisplayTrack();
                case 'I': return ruleChange.NewRule.DisplayTrack();
                case 'C': return DisplayDiff(ruleChange.OldRule.DisplayTrack(), ruleChange.NewRule.DisplayTrack());
                default:
                    Log.WriteWarning("Unknown Change Action", $"found an unexpected change action [{ruleChange.ChangeAction}]");
                    return "";
            }
        }
        public static string DisplayDisabled(this RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return ruleChange.OldRule.DisplayDisabled();
                case 'I': return ruleChange.NewRule.DisplayDisabled();
                case 'C': return DisplayDiff(ruleChange.OldRule.DisplayDisabled(), ruleChange.NewRule.DisplayDisabled());
                default:
                    Log.WriteWarning("Unknown Change Action", $"found an unexpected change action [{ruleChange.ChangeAction}]");
                    return "";
            }
        }
        public static string DisplayUid(this RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return ruleChange.OldRule.DisplayUid();
                case 'I': return ruleChange.NewRule.DisplayUid();
                case 'C': return DisplayDiff(ruleChange.OldRule.DisplayUid(), ruleChange.NewRule.DisplayUid());
                default:
                    Log.WriteWarning("Unknown Change Action", $"found an unexpected change action [{ruleChange.ChangeAction}]");
                    return "";
            }
        }
        public static string DisplayComment(this RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return ruleChange.OldRule.DisplayComment();
                case 'I': return ruleChange.NewRule.DisplayComment();
                case 'C': return DisplayDiff(ruleChange.OldRule.DisplayComment(), ruleChange.NewRule.DisplayComment());
                default:
                    Log.WriteWarning("Unknown Change Action", $"found an unexpected change action [{ruleChange.ChangeAction}]");
                    return "";
            }
        }

        /// <summary>
        /// displays differences between two string objects
        /// </summary>
        /// <param name="oldElement">the original value of the object</param>
        /// <param name="newElement">the new (changed) value of the object</param>
        /// <returns><paramref name=""/>string diff result</returns>
        public static string DisplayDiff(string oldElement, string newElement)
        {
            return DisplayJsonDiff(oldElement, newElement);
        }

        /// <summary>
        /// displays differences between two json objects
        /// </summary>
        /// <param name="oldJsonObject">the original value of the object</param>
        /// <param name="newJsonObject">the new (changed) value of the object</param>
        /// <returns><paramref name=""/> wrapped in <c>Dictionary</c> serialized to Json.</returns>
        public static string DisplayJsonDiff(string oldJsonObject, string newJsonObject)
        {
            // todo: implement diff
            if (oldJsonObject == newJsonObject)
                return oldJsonObject;
            else
                return $"{oldJsonObject} --> {newJsonObject}";
        }
    }
}
