namespace FWO.Data.Middleware
{
    public class GroupGetParameters : PagingParameters
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

    public class GroupMemberGetParameters : PagingParameters
    {
        public string GroupDn { get; set; } = "";
    }

    public class GroupMembershipGetParameters : PagingParameters
    {
        public string UserDn { get; set; } = "";
        public string UserName { get; set; } = "";
    }

    public class GroupResolveParameters : PagingParameters
    {
        public List<string> Dns { get; set; } = new();
    }
}
