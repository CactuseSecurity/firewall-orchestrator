using System.Collections.Generic;

namespace FWO.Ui.Data.API
{
    public class Role
    {
        public string Name;
        public string Description;
        public List<UiUser> Users { get; set; }

        public Role()
        { }

        public Role(Role role)
        {
            Name = role.Name;
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
