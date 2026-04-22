using FWO.Basics.Enums;
using FWO.Config.Api;

namespace FWO.Ui.Data.Extensions
{
    public static class EnumExtensions
    {
        extension(Enum @enum)
        {
            public string ToString(UserConfig userConfig)
            {
                bool success = Enum.TryParse(@enum.ToString(), out PreferredCollapseState state);

                if(!success)
                {
                    return @enum.ToString();
                }

                try
                {
                    return state switch
                    {
                        PreferredCollapseState.Collapsed => userConfig.GetText("PreferredCollapseState_Collapsed"),
                        PreferredCollapseState.Expanded => userConfig.GetText("PreferredCollapseState_Expanded"),
                        _ => "",
                    };
                }
                catch (Exception)
                {
                    return @enum.ToString();
                }
            }

            public IEnumerable<T> GetFlags<T>(Enum? ignore = null)
            {
                foreach (T value in Enum.GetValues(@enum.GetType()))
                {
                    Enum enumVal = (Enum)Convert.ChangeType(value, typeof(Enum));
                    if (!enumVal.Equals(ignore) && @enum.HasFlag(enumVal))
                    {
                        yield return value;
                    }
                }
            }
        }

    }
}
