using FWO.Basics;
using FWO.Logging;
using GraphQL.Client.Http;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Authentication;
using System.Security.Claims;

namespace FWO.Api.Client
{
    public partial class GraphQlApiConnection
    {
        private readonly AsyncLocal<List<string>?> roleStack = new();
        private string defaultRole = "";
        private List<string> allowedRoles = [];
        private AmbientRoleSource? ambientRoleSource;
        private string forcedExecutionMode = "";
        private bool restrictElevatedRoleSwitches = false;

        /// <summary>
        /// Applies the JWT and its role state to the query and subscription clients.
        /// </summary>
        /// <param name="jwt">The JWT used to authenticate API requests.</param>
        public override void SetAuthHeader(string jwt)
        {
            ObjectDisposedException.ThrowIf(graphQlClient is null, graphQlClient);
            ObjectDisposedException.ThrowIf(graphQlSubscriptionClient is null, graphQlSubscriptionClient);

            UpdateJwtRoleState(jwt);
            ApplyAuthHeader(graphQlClient, jwt);
            ApplyAuthHeader(graphQlSubscriptionClient, jwt);

            InvokeOnAuthHeaderChanged(this, jwt);
        }

        private void ApplyAuthHeader(GraphQLHttpClient client, string jwt)
        {
            client.HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
            client.Options.ConfigureWebSocketConnectionInitPayload = _ => CreateWebSocketConnectionInitPayload(client);
        }

        /// <summary>
        /// Selects an explicit application role for the current async flow.
        /// </summary>
        /// <param name="role">The requested application role.</param>
        public override void SetRole(string role)
        {
            if (restrictElevatedRoleSwitches && IsForcedExecutionMode(role))
            {
                throw new AuthenticationException($"Execution mode '{GlobalConst.kUserRolesSelection}' does not allow switching to role: {role}");
            }

            PushRole(IsForcedExecutionMode(forcedExecutionMode) ? forcedExecutionMode : role);
        }

        private void ApplyExecutionMode(string role, bool restrictElevatedRoles)
        {
            forcedExecutionMode = IsForcedExecutionMode(role) ? role : "";
            restrictElevatedRoleSwitches = restrictElevatedRoles;
            ambientRoleSource = null;
            roleStack.Value = null;
        }

        /// <summary>
        /// Sets the execution mode allowed by the user's JWT roles.
        /// </summary>
        /// <param name="user">The authenticated user and their roles.</param>
        /// <param name="role">The requested execution mode.</param>
        public override void SetExecutionMode(ClaimsPrincipal user, string role)
        {
            if (IsForcedExecutionMode(role) && !HasAllowedRole(user, role))
            {
                throw new AuthenticationException($"User is not allowed to use execution mode: {role}");
            }

            List<string> userRoles = ExecutionModeHelper.GetUserRoles(user);
            string selectedExecutionMode = ExecutionModeHelper.NormalizeExecutionMode(userRoles, role);
            string normalizedRole = selectedExecutionMode.Equals(GlobalConst.kUserRolesSelection, StringComparison.OrdinalIgnoreCase) ? "" : selectedExecutionMode;
            ApplyExecutionMode(normalizedRole, normalizedRole == "" && HasSelectableUserRole(user));
            InvokeOnExecutionModeChanged(this, GetExecutionMode());
        }

        /// <summary>
        /// Chooses the role to use when the request does not set one explicitly.
        /// </summary>
        /// <param name="user">The authenticated user and their roles.</param>
        /// <param name="targetRoleList">Roles accepted by the requested operation.</param>
        public override void SetAmbientRole(ClaimsPrincipal user, List<string> targetRoleList)
        {
            List<string> fixedTargetRoleList = [.. targetRoleList];
            SetAmbientRoleProvider(user, () => fixedTargetRoleList);
        }

        /// <summary>
        /// Chooses the role to use when the request does not set one explicitly, from the roles the provider
        /// accepts at the time of each request. Resolving per request keeps the role in step with its context
        /// (e.g. the current page) without having to be notified before that context changes.
        /// </summary>
        /// <param name="user">The authenticated user and their roles.</param>
        /// <param name="targetRoleListProvider">Returns the roles accepted for the current context.</param>
        public override void SetAmbientRoleProvider(ClaimsPrincipal user, Func<List<string>> targetRoleListProvider)
        {
            ambientRoleSource = new AmbientRoleSource(user, targetRoleListProvider);
        }

        /// <summary>
        /// Resolves the ambient role from the current source. A failing provider must not break the request
        /// itself, so it is logged and treated as "no ambient role".
        /// </summary>
        /// <returns>The ambient role, or an empty string when none applies.</returns>
        private string GetAmbientRole()
        {
            AmbientRoleSource? source = ambientRoleSource;
            if (source == null)
            {
                return "";
            }

            List<string> targetRoleList;
            try
            {
                targetRoleList = source.TargetRoleListProvider();
            }
            catch (Exception exception)
            {
                Log.WriteWarning("Ambient Role", $"Could not determine the ambient role: {exception.Message}");
                return "";
            }
            if (targetRoleList.Count == 0)
            {
                return "";
            }

            bool includeElevatedRoles = !HasSelectableUserRole(source.User);
            return IsForcedExecutionMode(source.User)
                ? forcedExecutionMode
                : GetFirstAllowedRole(source.User, targetRoleList, includeElevatedRoles)
                    ?? "";
        }

        /// <summary>
        /// Gets the selected execution mode.
        /// </summary>
        /// <returns>The forced role or the selectable-role marker.</returns>
        public override string GetExecutionMode()
        {
            return forcedExecutionMode == "" ? GlobalConst.kUserRolesSelection : forcedExecutionMode;
        }

        /// <summary>
        /// Determines whether a role is active for the current request.
        /// </summary>
        /// <param name="role">The role to compare.</param>
        /// <returns>True when the role is active.</returns>
        public bool IsActRole(string role)
        {
            return role == GetActRole();
        }

        /// <summary>
        /// Gets the effective role for the current request.
        /// </summary>
        /// <returns>The explicit, ambient, or baseline role.</returns>
        public override string GetActRole()
        {
            return GetActRole(GetAmbientRole());
        }

        /// <summary>
        /// Gets the effective role from an ambient role that the caller has already resolved, so that one
        /// request resolves it only once.
        /// </summary>
        /// <param name="ambientRole">The resolved ambient role, empty when none applies.</param>
        /// <returns>The explicit, ambient, or baseline role.</returns>
        private string GetActRole(string ambientRole)
        {
            ObjectDisposedException.ThrowIf(graphQlClient is null, graphQlClient);

            List<string>? roles = roleStack.Value;
            if (roles != null && roles.Count > 0)
            {
                return roles[^1];
            }
            if (!string.IsNullOrWhiteSpace(ambientRole))
            {
                return ambientRole;
            }
            return GetBaselineRole();
        }

        /// <summary>
        /// Selects the first role accepted by the requested operation.
        /// </summary>
        /// <param name="user">The authenticated user and their roles.</param>
        /// <param name="targetRoleList">Roles accepted by the requested operation.</param>
        public override void SetBestRole(ClaimsPrincipal user, List<string> targetRoleList)
        {
            bool includeElevatedRoles = !HasSelectableUserRole(user);
            string targetRole = IsForcedExecutionMode(user)
                ? forcedExecutionMode
                : GetFirstAllowedRole(user, targetRoleList, includeElevatedRoles)
                    ?? throw new AuthenticationException($"User has none of the required roles: {string.Join(", ", targetRoleList)}");
            PushRole(targetRole);
        }

        private static string? GetFirstAllowedRole(ClaimsPrincipal user, List<string> targetRoleList, bool includeElevatedRoles)
        {
            foreach (string targetRole in targetRoleList)
            {
                if ((includeElevatedRoles || !IsForcedExecutionMode(targetRole)) && HasAllowedRole(user, targetRole))
                {
                    return targetRole;
                }
            }
            return null;
        }

        private bool IsForcedExecutionMode(ClaimsPrincipal user)
        {
            return IsForcedExecutionMode(forcedExecutionMode) && HasAllowedRole(user, forcedExecutionMode);
        }

        private static bool IsForcedExecutionMode(string role)
        {
            return role.Equals(FWO.Basics.Roles.Admin, StringComparison.OrdinalIgnoreCase)
                || role.Equals(FWO.Basics.Roles.Auditor, StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasSelectableUserRole(ClaimsPrincipal user)
        {
            return ExecutionModeHelper.GetUserRoles(user).Any(role => !IsForcedExecutionMode(role) && !FWO.Basics.RoleGroups.IsTechnicalOrAnonymous(role));
        }

        private string GetBaselineRole()
        {
            if (IsForcedExecutionMode(forcedExecutionMode))
            {
                return forcedExecutionMode;
            }
            if (restrictElevatedRoleSwitches && IsForcedExecutionMode(defaultRole))
            {
                return "";
            }
            return defaultRole;
        }

        private string GetRequestRole()
        {
            string ambientRole = GetAmbientRole();
            string role = GetActRole(ambientRole);
            if (!string.IsNullOrWhiteSpace(role) && HasExplicitRole())
            {
                return role;
            }
            if (IsForcedExecutionMode(forcedExecutionMode))
            {
                return role;
            }
            if (!string.IsNullOrWhiteSpace(ambientRole))
            {
                return ambientRole;
            }
            if (!string.IsNullOrWhiteSpace(role))
            {
                return role;
            }
            if (RequiresExplicitRole())
            {
                throw new AuthenticationException("GraphQL API call requires an explicit role for users with multiple application roles. Use RunWithBestRole or RunWithRole.");
            }
            return role;
        }

        private bool HasExplicitRole()
        {
            List<string>? roles = roleStack.Value;
            return roles != null && roles.Any(role => !string.IsNullOrWhiteSpace(role));
        }

        private bool RequiresExplicitRole()
        {
            return allowedRoles
                .Where(role => !FWO.Basics.RoleGroups.IsTechnicalOrAnonymous(role))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count() > 1;
        }

        /// <summary>
        /// Removes the most recently selected explicit role.
        /// </summary>
        public override void SwitchBack()
        {
            List<string>? roles = roleStack.Value;
            if (roles == null || roles.Count == 0)
            {
                return;
            }

            List<string> newRoles = [.. roles];
            newRoles.RemoveAt(newRoles.Count - 1);
            roleStack.Value = newRoles;
        }

        private void PushRole(string role)
        {
            List<string>? roles = roleStack.Value;
            List<string> newRoles = roles == null ? [] : [.. roles];
            newRoles.Add(role);
            roleStack.Value = newRoles;
        }

        private static bool HasAllowedRole(ClaimsPrincipal user, string role)
        {
            return ExecutionModeHelper.GetUserRoles(user).Contains(role, StringComparer.OrdinalIgnoreCase);
        }

        private void UpdateJwtRoleState(string jwt)
        {
            defaultRole = "";
            allowedRoles = [];
            try
            {
                JwtSecurityToken token = new JwtSecurityTokenHandler().ReadJwtToken(jwt);
                defaultRole = token.Claims.FirstOrDefault(claim => claim.Type == "x-hasura-default-role")?.Value ?? "";
                allowedRoles = JwtClaimParser.ExtractStringClaimValues(token.Claims, "x-hasura-allowed-roles");
            }
            catch
            {
                defaultRole = "";
                allowedRoles = [];
            }
        }

        /// <summary>
        /// The user and the provider of accepted roles the ambient role is resolved from. Kept in one
        /// immutable object so that a request never combines the user of one source with the roles of another.
        /// </summary>
        /// <param name="User">The authenticated user and their roles.</param>
        /// <param name="TargetRoleListProvider">Returns the roles accepted for the current context.</param>
        private sealed record AmbientRoleSource(ClaimsPrincipal User, Func<List<string>> TargetRoleListProvider);
    }
}
