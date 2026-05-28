using FWO.Basics;

namespace FWO.Api.Client
{
    /// <summary>
    /// Provides API execution-mode rules for users with elevated and scoped roles.
    /// </summary>
    public static class ExecutionModeHelper
    {
        public const string UserRolesSelection = "user_roles";

        /// <summary>
        /// Returns non-technical roles that may be selected by the current user.
        /// </summary>
        public static List<string> GetSelectableRoles(IEnumerable<string> roles)
        {
            return roles
                .Where(role => !string.IsNullOrWhiteSpace(role))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(role => !RoleGroups.IsTechnicalOrAnonymous(role))
                .OrderBy(role => role, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// Returns the execution modes offered to users with elevated roles.
        /// </summary>
        public static List<string> GetSelectableExecutionModes(IEnumerable<string> roles)
        {
            List<string> selectableRoles = GetSelectableRoles(roles);
            List<string> selectableModes = [UserRolesSelection];

            if (selectableRoles.Contains(Roles.Admin, StringComparer.OrdinalIgnoreCase))
            {
                selectableModes.Add(Roles.Admin);
            }
            if (selectableRoles.Contains(Roles.Auditor, StringComparer.OrdinalIgnoreCase))
            {
                selectableModes.Add(Roles.Auditor);
            }

            return selectableModes;
        }

        /// <summary>
        /// Determines whether the user settings page should offer execution-mode selection.
        /// </summary>
        public static bool ShouldShowExecutionModeSelection(IEnumerable<string> roles)
        {
            List<string> selectableRoles = GetSelectableRoles(roles);
            return selectableRoles.Count > 1
                && (selectableRoles.Contains(Roles.Admin, StringComparer.OrdinalIgnoreCase)
                    || selectableRoles.Contains(Roles.Auditor, StringComparer.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Determines whether a role should be considered available under the selected execution mode.
        /// Admin and auditor are only available in their forced execution mode when a user can also run with normal roles.
        /// </summary>
        public static bool IsRoleAvailableInExecutionMode(IEnumerable<string> roles, string executionMode, string role)
        {
            List<string> selectableRoles = GetSelectableRoles(roles);
            if (!selectableRoles.Contains(role, StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }

            if (role.Equals(Roles.Admin, StringComparison.OrdinalIgnoreCase)
                || role.Equals(Roles.Auditor, StringComparison.OrdinalIgnoreCase))
            {
                return !ShouldShowExecutionModeSelection(selectableRoles)
                    || executionMode.Equals(role, StringComparison.OrdinalIgnoreCase);
            }

            return true;
        }

        /// <summary>
        /// Determines whether any role from the supplied list is available under the selected execution mode.
        /// </summary>
        public static bool HasAnyRoleInExecutionMode(IEnumerable<string> roles, string executionMode, IEnumerable<string> targetRoles)
        {
            return targetRoles.Any(role => IsRoleAvailableInExecutionMode(roles, executionMode, role));
        }

        /// <summary>
        /// Selects a valid execution mode, preferring a current admin or auditor override.
        /// </summary>
        public static string GetSelectedExecutionMode(IEnumerable<string> roles, string currentRole)
        {
            List<string> selectableModes = GetSelectableExecutionModes(roles);
            if ((currentRole.Equals(Roles.Admin, StringComparison.OrdinalIgnoreCase)
                    || currentRole.Equals(Roles.Auditor, StringComparison.OrdinalIgnoreCase))
                && selectableModes.Contains(currentRole, StringComparer.OrdinalIgnoreCase))
            {
                return selectableModes.First(role => role.Equals(currentRole, StringComparison.OrdinalIgnoreCase));
            }
            return UserRolesSelection;
        }
    }
}
