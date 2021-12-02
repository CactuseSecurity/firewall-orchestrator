namespace FWO.Middleware.RequestParameters
{
    public class GroupGetParameters
    {
        public string LdapHostname { get; set; } = "";
        public string SearchPattern { get; set; } = "";
    }

    public class GroupAddDeleteParameters
    {
        public string GroupDn { get; set; } = "";
    }

    public class GroupEditParameters
    {
        public string OldGroupDn { get; set; } = "";
        public string NewGroupDn { get; set; } = "";
    }

    public class GroupAddDeleteUserParameters
    {
        public string UserDn { get; set; } = "";
        public string GroupDn { get; set; } = "";
    }
}
