using FWO.Config.Api;
using Microsoft.AspNetCore.Components;


namespace FWO.Ui.Services
{
    public static class DisplayService
    {
        public static MarkupString DisplayButton(UserConfig userConfig, string text, string icon, string iconText = "", string objIcon = "")
        {
            string tooltip = userConfig.ModIconify ? $"data-toggle=\"tooltip\" title=\"{@userConfig.PureLine(text)}\"" : "";
            string iconToDisplay = $"<span class=\"{icon}\" {@tooltip}/>";
            string iconTextPart = iconText != "" ? " <span class=\"stdtext\">" + userConfig.GetText(iconText) + "</span>" : "";
            string objIconToDisplay = objIcon != "" ? $" <span class=\"{objIcon}\"/>" : "";
            return (MarkupString)(userConfig.ModIconify ? iconToDisplay + iconTextPart + objIconToDisplay : userConfig.GetText(text));
        }

        public static MarkupString DisplayButtonWithTooltip(UserConfig userConfig, string text, string icon, string tooltipText, string iconText = "", string objIcon = "")
        {
            string tooltip = $"data-toggle=\"tooltip\" title=\"{(userConfig.ModIconify ? @userConfig.PureLine(text) + " - " : "")} {tooltipText}\"";
            string iconToDisplay = $"<span class=\"{icon}\" {@tooltip}/>";
            string iconTextPart = iconText != "" ? " <span class=\"stdtext\">" + userConfig.GetText(iconText) + "</span>" : "";
            string objIconToDisplay = objIcon != "" ? $" <span class=\"{objIcon}\"/>" : "";
            string uniconified = $"<span {@tooltip}/>{@userConfig.GetText(text)}</span>";
            return (MarkupString)(userConfig.ModIconify ? iconToDisplay + iconTextPart + objIconToDisplay : uniconified);
        }
    }
}
