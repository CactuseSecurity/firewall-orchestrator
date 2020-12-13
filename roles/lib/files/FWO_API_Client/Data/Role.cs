using System.Collections.Generic;

namespace FWO.Api.Data
{
    public class Role
    {
        public string Name;
        public string Dn;
        public string Description;
        public List<UiUser> Users { get; set; }

        public Role()
        { 
            Users = new List<UiUser>();
        }

        public Role(Role role)
        {
            Name = role.Name;
            Dn = role.Dn;
            Description = role.Description;
            if (role.Users != null)
            {
                Users = new List<UiUser>(role.Users);
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
