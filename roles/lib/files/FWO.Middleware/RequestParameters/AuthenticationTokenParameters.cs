namespace FWO.Middleware.RequestParameters
{
    public class AuthenticationTokenGetParameters
    {
        public string? Username { get; set; }
        public string? Password { get; set; }
    }

    public class AuthenticationTokenGetForUserParameters
    {
        public string AdminUsername { get; set; } = "";
        public string AdminPassword { get; set; } = "";
        public TimeSpan Lifetime { get; set; }
        public string TargetUserDn { get; set; } = "";
        public string TargetUserName { get; set; } = "";
    }
}
