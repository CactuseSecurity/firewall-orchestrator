using FWO.Basics;

namespace FWO.Data
{
    public class UserGroup
    {
        public string Name = "";
        public string Dn = "";
        public bool OwnerGroup = false;
        public List<UiUser> Users { get; set; } = [];
        public List<string> Roles { get; set; } = [];

        public UserGroup()
        { }

        public UserGroup(UserGroup group)
        {
            Name = group.Name;
            Dn = group.Dn;
            OwnerGroup = group.OwnerGroup;
            Users = [.. group.Users];
            Roles = [.. group.Roles];
        }

        public string UserList()
        {
            List<string> userNames = [];
            foreach (UiUser user in Users)
            {
                userNames.Add(user.Name);
            }
            return string.Join(", ", userNames);
        }

        public string RoleList()
        {
            return string.Join(", ", Roles);
        }

        public bool Sanitize()
        {
            bool shortened = false;
            Name = Name.SanitizeLdapNameMand(ref shortened);
            return shortened;
        }
    }
}
