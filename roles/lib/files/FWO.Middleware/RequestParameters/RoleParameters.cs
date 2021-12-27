namespace FWO.Middleware.RequestParameters
{
    public class RoleAddDeleteUserParameters
    {
        public string Role { get; set; } = "";
        public string UserDn { get; set; } = "";
    }

    public class RoleGetReturnParameters
    {
        public string Role { get; set; } = "";
        public List<RoleAttribute> Attributes { get; set; } = new List<RoleAttribute>();
    }

    public class RoleAttribute
    {
        public string Key { get; set; } = "";
        public string Value { get; set; } = "";
    }
}
