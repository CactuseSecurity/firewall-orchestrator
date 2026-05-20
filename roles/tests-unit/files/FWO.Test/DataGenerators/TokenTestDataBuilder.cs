using FWO.Data.Middleware;

namespace FWO.Test.DataGenerators
{
    /// <summary>
    /// Builder pattern for creating test authentication data
    /// </summary>
    public class TokenTestDataBuilder
    {
        public string? Username { get; private set; }
        public string? Password { get; private set; }
        public string? TargetUserName { get; private set; }
        public TimeSpan? Lifetime { get; private set; }

        public TokenTestDataBuilder WithUsername(string user)
        {
            Username = user;
            return this;
        }

        public TokenTestDataBuilder WithPassword(string pass)
        {
            Password = pass;
            return this;
        }

        public TokenTestDataBuilder WithTargetUser(string target)
        {
            TargetUserName = target;
            return this;
        }

        public TokenTestDataBuilder WithLifetime(TimeSpan time)
        {
            Lifetime = time;
            return this;
        }

        public AuthenticationTokenGetParameters BuildGetParameters()
        {
            return new AuthenticationTokenGetParameters
            {
                Username = Username,
                Password = Password
            };
        }

        public AuthenticationTokenGetForUserParameters BuildGetForUserParameters()
        {
            return new AuthenticationTokenGetForUserParameters
            {
                AdminUsername = Username!,
                AdminPassword = Password!,
                TargetUserName = TargetUserName!,
                Lifetime = Lifetime ?? TimeSpan.FromHours(24)
            };
        }
    }
}
