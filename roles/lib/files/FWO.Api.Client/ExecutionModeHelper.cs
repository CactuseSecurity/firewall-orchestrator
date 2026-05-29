using System.Security.Claims;
using System.Text.Json;
using FWO.Basics;

namespace FWO.Api.Client
{
    /// <summary>
    /// Provides API execution-mode rules for users with elevated and scoped roles.
    /// </summary>
    public static class ExecutionModeHelper
    {
        /// <summary>
        /// Extracts application roles from role claims and Hasura allowed-role claims.
        /// </summary>
        public static List<string> GetUserRoles(ClaimsPrincipal user)
        {
            List<string> roles = [];
            foreach (ClaimsIdentity identity in user.Identities)
            {
                roles.AddRange(identity.Claims
                    .Where(claim => claim.Type.Equals(identity.RoleClaimType, StringComparison.OrdinalIgnoreCase))
                    .Select(claim => claim.Value));
            }
            foreach (Claim claim in user.Claims.Where(currentClaim => IsHasuraAllowedRolesClaim(currentClaim.Type)))
            {
                if (TryParseAllowedRoles(claim.Value, out List<string> parsedRoles)
                    && parsedRoles.Count > 0)
                {
                    roles.AddRange(parsedRoles);
                }
                else
                {
                    roles.Add(claim.Value);
                }
            }
            return roles
                .Where(role => !string.IsNullOrWhiteSpace(role))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

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
            List<string> selectableModes = [GlobalConst.kUserRolesSelection];

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
            string effectiveExecutionMode = NormalizeExecutionMode(selectableRoles, executionMode);
            if (!selectableRoles.Contains(role, StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }

            if (IsElevatedExecutionMode(effectiveExecutionMode))
            {
                return effectiveExecutionMode.Equals(role, StringComparison.OrdinalIgnoreCase);
            }

            if (role.Equals(Roles.Admin, StringComparison.OrdinalIgnoreCase)
                || role.Equals(Roles.Auditor, StringComparison.OrdinalIgnoreCase))
            {
                return !ShouldShowExecutionModeSelection(selectableRoles);
            }

            return true;
        }

        /// <summary>
        /// Normalizes an untrusted execution-mode value to a mode available for the supplied roles.
        /// </summary>
        public static string NormalizeExecutionMode(IEnumerable<string> roles, string executionMode)
        {
            List<string> selectableModes = GetSelectableExecutionModes(roles);
            if (!string.IsNullOrWhiteSpace(executionMode)
                && selectableModes.Contains(executionMode, StringComparer.OrdinalIgnoreCase))
            {
                return selectableModes.First(mode => mode.Equals(executionMode, StringComparison.OrdinalIgnoreCase));
            }

            return GlobalConst.kUserRolesSelection;
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
            return GlobalConst.kUserRolesSelection;
        }

        private static bool IsElevatedExecutionMode(string executionMode)
        {
            return executionMode.Equals(Roles.Admin, StringComparison.OrdinalIgnoreCase)
                || executionMode.Equals(Roles.Auditor, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsHasuraAllowedRolesClaim(string claimType)
        {
            if (claimType.Equals("x-hasura-allowed-roles", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return claimType.EndsWith("/x-hasura-allowed-roles", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryParseAllowedRoles(string claimValue, out List<string> parsedRoles)
        {
            parsedRoles = [];
            if (string.IsNullOrWhiteSpace(claimValue))
            {
                return false;
            }

            try
            {
                string[]? roleArray = JsonSerializer.Deserialize<string[]>(claimValue);
                if (roleArray == null)
                {
                    return false;
                }
                parsedRoles = roleArray.Where(role => !string.IsNullOrWhiteSpace(role)).ToList();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
