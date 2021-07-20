using System.Collections.Generic;

namespace FWO.Api.Data
{
    public class UserGroup
    {
        public string Name;
        public string Dn;
        public List<UiUser> Users { get; set; }
        public List<string> Roles { get; set; }

        public UserGroup()
        { 
            Users = new List<UiUser>();
        }

        public UserGroup(UserGroup group)
        {
            Name = group.Name;
            Dn = group.Dn;
            if (group.Users != null)
            {
                Users = new List<UiUser>(group.Users);
            }
            if (group.Roles != null)
            {
                Roles = group.Roles;
            }

        }

        public string UserList()
        {
            List<string> userNames = new List<string>();
            foreach(UiUser user in Users)
            {
                userNames.Add(user.Name);
            }
            return string.Join(", ", userNames);
        }
    }
}
