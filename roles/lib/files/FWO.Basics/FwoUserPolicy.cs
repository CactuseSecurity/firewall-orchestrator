using Microsoft.AspNetCore.Authorization;

namespace FWO.Basics
{
    /// <summary>
    /// Authorization policy of the services that accept the JWTs of the middleware server for FWO users only,
    /// such as the AI MCP server and the AI harness.
    /// </summary>
    public static class FwoUserPolicy
    {
        /// <summary>
        /// Creates a policy builder that requires an authenticated user holding at least one role
        /// (<see cref="JwtClaimTypes.kRole"/>) that is neither technical nor anonymous. The middleware server also signs
        /// anonymous bootstrap tokens (issued without credentials) and tokens of technical users, which must not give
        /// access to these services. A service may add requirements of its own before building the policy.
        /// </summary>
        public static AuthorizationPolicyBuilder CreateBuilder()
        {
            return new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .RequireAssertion(context => context.User.FindAll(JwtClaimTypes.kRole).Any(role => !RoleGroups.IsTechnicalOrAnonymous(role.Value)));
        }
    }
}
