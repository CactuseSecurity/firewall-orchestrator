using FWO.Basics.Enums;
using FWO.Config.Api;
using FWO.Data.Enums;

namespace FWO.Ui.Data.Extensions
{
    public static class EnumExtensions
    {
        extension(PreferredCollapseState state)
        {
            /// <summary>
            /// Returns the localized label for the preferred collapse state.
            /// </summary>
            public string ToString(UserConfig userConfig)
            {
                ArgumentNullException.ThrowIfNull(userConfig);

                return state switch
                {
                    PreferredCollapseState.Collapsed => userConfig.GetText("PreferredCollapseState_Collapsed"),
                    PreferredCollapseState.Expanded => userConfig.GetText("PreferredCollapseState_Expanded"),
                    PreferredCollapseState.Intermediate => userConfig.GetText("PreferredCollapseState_Intermediate"),
                    _ => state.ToString(),
                };
            }
        }

        extension(TokenLifetimeUnit unit)
        {
            public string ToString(UserConfig userConfig)
            {
                ArgumentNullException.ThrowIfNull(userConfig);

                return unit switch
                {
                    TokenLifetimeUnit.Minutes => userConfig.GetText("Minutes2"),
                    TokenLifetimeUnit.Hours => userConfig.GetText("Hours"),
                    TokenLifetimeUnit.Days => userConfig.GetText("Days"),
                    _ => unit.ToString(),
                };
            }
        }
    }
}
