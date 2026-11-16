using FWO.Config.Api;

namespace FWO.Ui.Services
{
    public static class PasswordPolicy
    {
        /// <summary>
        /// Checks a password against the configured policy, with error texts in the user's language.
        /// </summary>
        public static bool CheckPolicy(string pw, GlobalConfig globalConfig, UserConfig userConfig, out string errorMsg)
        {
            return PasswordPolicyValidator.CheckPolicy(pw, globalConfig, userConfig, out errorMsg);
        }
    }
}
