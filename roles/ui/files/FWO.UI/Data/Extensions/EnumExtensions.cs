using FWO.Basics.Enums;
using FWO.Config.Api;

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
    }
}
