using FWO.Data.Middleware;

namespace FWO.Test.DataGenerators
{
    /// <summary>
    /// Builder pattern for creating test authentication data
    /// </summary>
    public class TokenTestDataBuilder
    {
        private string username = "testuser";
        private string password = "testpassword";
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
            string adminUser = "admin",
            string adminPass = "adminpassword")
        {
            return new AuthenticationTokenGetForUserParameters
            {
                AdminUsername = adminUser,
                AdminPassword = adminPass,
                TargetUserName = targetUserName ?? username,
                Lifetime = lifetime ?? TimeSpan.FromHours(24)
            };
        }
    }
}
