using System;
using System.Linq;
using System.Text.RegularExpressions;
using FWO.Logging;
using FWO.Config.Api;

namespace FWO.Ui.Services
{
    public class PasswordPolicy
    {
        public static bool CheckPolicy(string pw, GlobalConfig globalConfig, UserConfig userConfig, out string errorMsg)
        {
            errorMsg = "";
            if(pw.Length < globalConfig.PwMinLength)
            {
                errorMsg = userConfig.GetText("E5411") + globalConfig.PwMinLength;
                return false;
            }
            if(globalConfig.PwUpperCaseRequired && !pw.Any(char.IsUpper))
            {
                errorMsg = userConfig.GetText("E5412");
                return false;
            }
            if(globalConfig.PwLowerCaseRequired && !pw.Any(char.IsLower))
            {
                errorMsg = userConfig.GetText("E5413");
                return false;
            }
            if(globalConfig.PwNumberRequired && !pw.Any(char.IsDigit))
            {
                errorMsg = userConfig.GetText("E5414");
                return false;
            }
            if(globalConfig.PwSpecialCharactersRequired)
            {
                Regex rgx = new Regex("[!?(){}=~$%&#*-+.,_]");
                if(!rgx.IsMatch(pw))
                {
                    errorMsg = userConfig.GetText("E5415");
                    return false;
                }
            }
            return true;
        }
    }
}
