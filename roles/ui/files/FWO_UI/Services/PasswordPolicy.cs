using System;
using System.Linq;
using System.Text.RegularExpressions;
using FWO.Logging;
using FWO.ApiConfig;

namespace FWO.Ui.Services
{
    public class PasswordPolicy
    {
        int minLength = 10;
        bool upperCaseRequired = false;
        bool lowerCaseRequired = false;
        bool numberRequired = false;
        bool specialCharactersRequired = false;

        public bool checkPolicy(string pw, UserConfig userConfig, out string errorMsg)
        {
            GetSettings(userConfig);
            errorMsg = "";
            if(pw.Length < minLength)
            {
                errorMsg = $"password must be at least {minLength} digits long";
                return false;
            }
            if(upperCaseRequired && !pw.Any(char.IsUpper))
            {
                errorMsg = $"password must contain at least one upper case character";
                return false;
            }
            if(lowerCaseRequired && !pw.Any(char.IsLower))
            {
                errorMsg = $"password must contain at least one lower case character";
                return false;
            }
            if(numberRequired && !pw.Any(char.IsDigit))
            {
                errorMsg = $"password must contain at least one number";
                return false;
            }
            if(specialCharactersRequired)
            {
                Regex rgx = new Regex("[!?(){}=~$%&#*-+.,_]");
                if(!rgx.IsMatch(pw))
                {
                    errorMsg = "password must contain at least one special character (!?(){}=~$%&#*-+.,_)";
                    return false;
                }
            }
            return true;
        }

        private void GetSettings(UserConfig userConfig)
        {
            try
            {
                string settingsValue = userConfig.GetConfigValue(GlobalConfig.kPwMinLength);
                if (settingsValue != "")
                {
                    minLength = Int32.Parse(settingsValue);
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
