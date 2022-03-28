namespace FWO.Middleware.RequestParameters
{
    public class LdapAddParameters
    {
        public string? Name { get; set; }
        public string Address { get; set; } = "";
        public int Port { get; set; } = 636;
        public int Type { get; set; } = 0;
        public int PatternLength { get; set; } = 0;
        public string? SearchUser { get; set; }
        public bool Tls { get; set; }
        public int TenantLevel { get; set; }
        public string? SearchUserPwd { get; set; }
        public string? SearchpathForUsers { get; set; }
        public string? SearchpathForRoles { get; set; }
        public string? SearchpathForGroups { get; set; }
        public string? WriteUser { get; set; }
        public string? WriteUserPwd { get; set; }
        public int? TenantId { get; set; }
        public string? GlobalTenantName { get; set; }
        public bool Active { get; set; }

        public LdapAddParameters()
        {}

        public LdapAddParameters(LdapAddParameters ldapAddParameters)
        {
            Name = ldapAddParameters.Name;
            Address = ldapAddParameters.Address;
            Port = ldapAddParameters.Port;
            Type = ldapAddParameters.Type;
            PatternLength = ldapAddParameters.PatternLength;
            SearchUser = ldapAddParameters.SearchUser;
            Tls = ldapAddParameters.Tls;
            TenantLevel = ldapAddParameters.TenantLevel;
            SearchUserPwd = ldapAddParameters.SearchUserPwd;
            SearchpathForUsers = ldapAddParameters.SearchpathForUsers;
            SearchpathForRoles = ldapAddParameters.SearchpathForRoles;
            SearchpathForGroups = ldapAddParameters.SearchpathForGroups;
            WriteUser = ldapAddParameters.WriteUser;
            WriteUserPwd = ldapAddParameters.WriteUserPwd;
            TenantId = ldapAddParameters.TenantId;
            GlobalTenantName = ldapAddParameters.GlobalTenantName;
            Active = ldapAddParameters.Active;
        }
    }

    public class LdapGetUpdateParameters : LdapAddParameters
    {
        public int Id { get; set; }

        public LdapGetUpdateParameters()
        {}

        public LdapGetUpdateParameters(LdapAddParameters ldapAddParameters, int id) : base (ldapAddParameters)
        {
            Id = id;
        }
    }

    public class LdapDeleteParameters
    {
        public int Id { get; set; }
    }
}
