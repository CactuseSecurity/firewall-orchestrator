using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Report;
using System.Net;

namespace FWO.Ui.Display
{
    public static class OwnerRecertDisplay
    {
        private const string DateFormat = "dd.MM.yyyy";

        public static string FormatNextRecertDate(FwoOwner owner, UserConfig userConfig)
        {
            return owner.GetEffectiveNextRecertDate(userConfig.RecertificationPeriod)?.ToString(DateFormat) ?? "";
        }

        public static string FormatLastRecertified(FwoOwner owner, UserConfig userConfig)
        {
            string lastRecertified = owner.GetEffectiveLastRecertified()?.ToString(DateFormat) ?? "";
            return owner.UsesCreationDateFallback() && lastRecertified != ""
                ? $"{lastRecertified} ({userConfig.GetText("created")})"
                : lastRecertified;
        }

        public static string FormatMainResponsibles(FwoOwner owner, string separator = ", ")
        {
            return FormatResponsibles(owner, GlobalConst.kOwnerResponsibleTypeMain, separator);
        }

        public static string FormatAdditionalInfoValue(FwoOwner owner, string key)
        {
            return TryGetAdditionalInfoValue(owner, key, out string? value)
                ? value ?? ""
                : "";
        }

        public static string FormatAdditionalInfoValueHtml(FwoOwner owner, string key)
        {
            string value = FormatAdditionalInfoValue(owner, key);
            return TryParseBooleanValue(value, out bool boolValue)
                ? boolValue.ShowAsHtml().ToString()
                : WebUtility.HtmlEncode(value);
        }

        /// <summary>
        /// Checks whether the owner matches the selected additional-info filter semantics.
        /// </summary>
        public static bool MatchesAdditionalInfoFilter(FwoOwner owner, AddInfoFilter filter)
        {
            if (string.IsNullOrWhiteSpace(filter.Name) || filter.Mode == AddInfoFilterMode.display_only)
            {
                return true;
            }

            bool hasAdditionalInfoKey = TryGetAdditionalInfoValue(owner, filter.Name, out string? value);
            return filter.Mode switch
            {
                // Keep the workflow semantics here: key presence is enough for "existing".
                AddInfoFilterMode.existing => hasAdditionalInfoKey,
                AddInfoFilterMode.not_existing => !hasAdditionalInfoKey,
                AddInfoFilterMode.value => hasAdditionalInfoKey && string.Equals(value, filter.Value, StringComparison.Ordinal),
                _ => true
            };
        }

        public static bool TryParseBooleanValue(string value, out bool boolValue)
        {
            return bool.TryParse(value.Trim(), out boolValue);
        }

        private static bool TryGetAdditionalInfoValue(FwoOwner owner, string key, out string? value)
        {
            value = null;
            if (owner.AdditionalInfo == null || string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            if (owner.AdditionalInfo.TryGetValue(key, out value))
            {
                return true;
            }

            foreach (KeyValuePair<string, string> entry in owner.AdditionalInfo)
            {
                if (string.Equals(entry.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    value = entry.Value;
                    return true;
                }
            }

            return false;
        }

        public static string FormatResponsibles(FwoOwner owner, int responsibleTypeId, string separator)
        {
            return string.Join(separator, owner.GetOwnerResponsiblesByType(responsibleTypeId)
                .Where(dn => !string.IsNullOrWhiteSpace(dn))
                .OrderBy(dn => dn)
                .Select(FormatResponsible));
        }

        public static string FormatResponsible(string dn)
        {
            DistName distName = new(dn);
            string display = !string.IsNullOrWhiteSpace(distName.UserName) ? distName.UserName : distName.Group;
            return string.IsNullOrWhiteSpace(display) ? dn : display;
        }
    }
}
