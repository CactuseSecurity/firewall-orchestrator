namespace FWO.Middleware.RequestParameters
{
    public class GroupGetParameters
    {
        public int LdapId { get; set; }
        public string SearchPattern { get; set; } = "";
    }

    public class GroupGetReturnParameters
    {
        public string GroupDn { get; set; } = "";
        public bool OwnerGroup { get; set; } = false;
        public List<string> Members { get; set; } = new List<string>();
    }

    public class GroupAddDeleteParameters
    {
        public string GroupName { get; set; } = "";
        public bool OwnerGroup { get; set; } = false;
    }

    public class GroupEditParameters
    {
        public string OldGroupName { get; set; } = "";
        public string NewGroupName { get; set; } = "";
    }

    public class GroupAddDeleteUserParameters
    {
        public string UserDn { get; set; } = "";
        public string GroupDn { get; set; } = "";
    }
}
