using FWO.Api.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FWO.Ui.Display
{
    public static class RuleChangeDisplay
    {
        private static StringBuilder result;


        public static string DisplayChangeTime(this RuleChange ruleChange)
        {
            return ruleChange.ChangeImport.Time.ToString();
        }
        public static string DisplayChangeAction(this RuleChange ruleChange)
        {
            string result = "";
            switch (ruleChange.ChangeAction)
            {
                case 'I':
                    result = "rule created";
                    break;
                case 'D':
                    result = "rule deleted";
                    break;
                case 'C':
                    result = "rule modified";
                    break;
            }
            return result;
        }
        public static string DisplayChangeRuleDiff(this RuleChange ruleChange)
        {
            string result = "";
            switch (ruleChange.ChangeAction)
            {
                case 'I':
                    // display new rule
                    break;
                case 'D':
                    // display old rule as deleted
                    break;
                case 'C':
                    // display rule diff new - old rule
                    break;
            }
            return result;
        }
    }
}
