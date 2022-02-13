using System;
using System.Linq;
using System.Text.RegularExpressions;
using FWO.Logging;
using FWO.Config.Api;

namespace FWO.Ui.Services
{
    public class PasswordPolicy
    {
        private static int minLength = GlobalConfig.kDefaultInitMinPwdLength;
        private static bool upperCaseRequired = false;
        private static bool lowerCaseRequired = false;
        private static bool numberRequired = false;
        private static bool specialCharactersRequired = false;

        public static bool CheckPolicy(string pw, UserConfig userConfig, out string errorMsg)
        {
            GetSettings(userConfig);
            errorMsg = "";
            if(pw.Length < minLength)
            {
                errorMsg = userConfig.GetText("E5411") + minLength;
                return false;
            }
            if(upperCaseRequired && !pw.Any(char.IsUpper))
            {
                errorMsg = userConfig.GetText("E5412");
                return false;
            }
            if(lowerCaseRequired && !pw.Any(char.IsLower))
            {
                errorMsg = userConfig.GetText("E5413");
                return false;
            }
            if(numberRequired && !pw.Any(char.IsDigit))
            {
                errorMsg = userConfig.GetText("E5414");
                return false;
            }
            if(specialCharactersRequired)
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

        private static void GetSettings(UserConfig userConfig)
        {
            try
            {
                string settingsValue = userConfig.GetConfigValue(GlobalConfig.kPwMinLength);
                if (settingsValue != "")
                {
                    minLength = int.Parse(settingsValue);
                }

                settingsValue = userConfig.GetConfigValue(GlobalConfig.kPwUpperCaseRequired);
                upperCaseRequired = (settingsValue == "True" ? true : false);

                settingsValue = userConfig.GetConfigValue(GlobalConfig.kPwLowerCaseRequired);
                lowerCaseRequired = (settingsValue == "True" ? true : false);

                settingsValue = userConfig.GetConfigValue(GlobalConfig.kPwNumberRequired);
                numberRequired = (settingsValue == "True" ? true : false);

                settingsValue = userConfig.GetConfigValue(GlobalConfig.kPwSpecialCharactersRequired);
                specialCharactersRequired = (settingsValue == "True" ? true : false);
            }
            catch (Exception exception)
            {
                Log.WriteError("Read Config", $"Error reading config value", exception);
            }
        }
    }
}
