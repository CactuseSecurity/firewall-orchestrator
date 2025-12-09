using FWO.Data.Middleware;

namespace FWO.Test.DataGenerators
{
    /// <summary>
    /// Builder pattern for creating test authentication data
    /// </summary>
    public class TokenTestDataBuilder
    {
        private string? username;
        private string? password;
        private string? targetUserName;
        private TimeSpan? lifetime;

        public TokenTestDataBuilder WithUsername(string user)
        {
            username = user;
            return this;
        }

        public TokenTestDataBuilder WithPassword(string pass)
        {
            password = pass;
            return this;
        }

        public TokenTestDataBuilder WithTargetUser(string target)
        {
            targetUserName = target;
            return this;
        }

        public TokenTestDataBuilder WithLifetime(TimeSpan time)
        {
            lifetime = time;
            return this;
        }

        public AuthenticationTokenGetParameters BuildGetParameters()
        {
            return new AuthenticationTokenGetParameters
            {
                Username = username,
                Password = password
            };
        }

        public AuthenticationTokenGetForUserParameters BuildGetForUserParameters(
            string? adminUser = null,
            string? adminPass = null)
        {
            if ((adminUser ?? username) is null)
            {
                throw new InvalidOperationException("AdminUsername must not be null.");
            }

            if ((adminPass ?? password) is null)
            {
                throw new InvalidOperationException("AdminPassword must not be null.");
            }

            if ((targetUserName ?? username) is null)
            { 
                throw new InvalidOperationException("TargetUserName must not be null.");
            }

            return new AuthenticationTokenGetForUserParameters
            {
                AdminUsername = adminUser ?? username!,
                AdminPassword = adminPass ?? password!,
                TargetUserName = targetUserName ?? username!,
                Lifetime = lifetime ?? TimeSpan.FromHours(24)
            };
        }
    }
}
