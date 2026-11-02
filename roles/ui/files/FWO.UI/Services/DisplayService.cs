using FWO.Basics;
using FWO.Config.Api;
using FWO.Data.Workflow;
using Microsoft.AspNetCore.Components;
using System.Net;


namespace FWO.Ui.Services
{
    public static class DisplayService
    {
        private const int AutomaticStateId = -1;
        private const int ConditionalStateId = -2;

        public static MarkupString DisplayButton(UserConfig userConfig, string text, string icon, string iconText = "", string objIcon = "")
        {
            string tooltip = userConfig.ModIconify ? $"data-toggle=\"tooltip\" title=\"{EncodeTooltip(userConfig.PureLine(text))}\"" : "";
            string iconToDisplay = $"<span class=\"{icon}\" {@tooltip}/>";
            string iconTextPart = iconText != "" ? " <span class=\"stdtext\">" + userConfig.GetText(iconText) + "</span>" : "";
            string objIconToDisplay = objIcon != "" ? $" <span class=\"{objIcon}\"/>" : "";
            return (MarkupString)(userConfig.ModIconify ? iconToDisplay + iconTextPart + objIconToDisplay : userConfig.GetText(text));
        }

        public static MarkupString DisplayButtonWithTooltip(UserConfig userConfig, string text, string icon, string tooltipText, string iconText = "", string objIcon = "")
        {
            string tooltipValue = $"{(userConfig.ModIconify ? userConfig.PureLine(text) + " - " : "")} {tooltipText}";
            string tooltip = $"data-toggle=\"tooltip\" title=\"{EncodeTooltip(tooltipValue)}\"";
            string iconToDisplay = $"<span class=\"{icon}\" {@tooltip}/>";
            string iconTextPart = iconText != "" ? " <span class=\"stdtext\">" + userConfig.GetText(iconText) + "</span>" : "";
            string objIconToDisplay = objIcon != "" ? $" <span class=\"{objIcon}\"/>" : "";
            string uniconified = $"<span {@tooltip}/>{@userConfig.GetText(text)}</span>";
            return (MarkupString)(userConfig.ModIconify ? iconToDisplay + iconTextPart + objIconToDisplay : uniconified);
        }

        /// <summary>
        /// Displays a text that contains markup. Shipped texts come from the installer and are rendered as they are,
        /// while a text an admin customized is sanitized first, so that it keeps formatting but no active content.
        /// </summary>
        /// <param name="userConfig">Configuration providing the texts.</param>
        /// <param name="key">Text key.</param>
        /// <returns>The text as markup.</returns>
        public static MarkupString DisplayConfigurableText(UserConfig userConfig, string key)
        {
            string text = userConfig.GetText(key);
            return (MarkupString)(userConfig.Overwrite.ContainsKey(key) ? HtmlOutputEncoder.SanitizeRichText(text) : text);
        }

        /// <summary>
        /// Encodes a tooltip for a double quoted title attribute. The value can carry user, LDAP or external
        /// system data, and texts may already contain HTML entities, which are decoded first so that they are
        /// still displayed as the characters they stand for.
        /// </summary>
        /// <param name="value">Tooltip text.</param>
        /// <returns>The tooltip, safe to place between two double quotes.</returns>
        private static string EncodeTooltip(string value)
        {
            return HtmlOutputEncoder.EncodeAttribute(WebUtility.HtmlDecode(value));
        }

        public static string DisplayState(UserConfig userConfig, WfState state)
        {
            if (state.Id == AutomaticStateId)
            {
                return userConfig.GetText("automatic");
            }

            if (state.Id == ConditionalStateId)
            {
                return userConfig.GetText("Conditional");
            }

            return state.Name;
        }

        public static string DisplayStateWithId(UserConfig userConfig, WfState state)
        {
            return state.Id < 0
                ? DisplayState(userConfig, state)
                : $"{state.Name} ({state.Id})";
        }
    }
}
