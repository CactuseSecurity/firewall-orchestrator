using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;

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
            return owner.AdditionalInfo != null && owner.AdditionalInfo.TryGetValue(key, out string? value)
                ? value
                : "";
        }

        public static bool TryParseBooleanValue(string value, out bool boolValue)
        {
            return bool.TryParse(value.Trim(), out boolValue);
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
