using FWO.Config.Api.Data;
using System.Text.RegularExpressions;

namespace FWO.Config.Api
{
    /// <summary>
    /// Checks a new password against the configured password policy. The UI checks it to answer quickly, the
    /// middleware enforces it, because its password endpoints can be called without the UI.
    /// </summary>
    public static class PasswordPolicyValidator
    {
        private static readonly Regex kSpecialCharacters = new("[!?(){}=~$%&#*-+.,_]", RegexOptions.None, TimeSpan.FromSeconds(1));

        /// <summary>
        /// Checks a password against the policy.
        /// </summary>
        /// <param name="password">The new password.</param>
        /// <param name="policy">Configuration holding the password policy settings.</param>
        /// <param name="textSource">Configuration providing the error texts in the wanted language.</param>
        /// <param name="errorMsg">Text of the first violated rule, empty if the password complies.</param>
        /// <returns>True if the password complies with the policy.</returns>
        public static bool CheckPolicy(string password, ConfigData policy, Config textSource, out string errorMsg)
        {
            errorMsg = "";
            if (password.Length < policy.PwMinLength)
            {
                errorMsg = textSource.GetText("E5411") + policy.PwMinLength;
            }
            else if (policy.PwUpperCaseRequired && !password.Any(char.IsUpper))
            {
                errorMsg = textSource.GetText("E5412");
            }
            else if (policy.PwLowerCaseRequired && !password.Any(char.IsLower))
            {
                errorMsg = textSource.GetText("E5413");
            }
            else if (policy.PwNumberRequired && !password.Any(char.IsDigit))
            {
                errorMsg = textSource.GetText("E5414");
            }
            else if (policy.PwSpecialCharactersRequired && !kSpecialCharacters.IsMatch(password))
            {
                errorMsg = textSource.GetText("E5415");
            }
            return errorMsg == "";
        }
    }
}
