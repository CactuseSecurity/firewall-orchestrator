﻿namespace FWO.Api.Data
{
    public class UserGroup
    {
        public string Name = "";
        public string Dn = "";
        public List<UiUser> Users { get; set; } = new List<UiUser>();
        public List<string> Roles { get; set; } = new List<string>();

        public UserGroup()
        {}

        public UserGroup(UserGroup group)
        {
            Name = group.Name;
            Dn = group.Dn;
            Users = new List<UiUser>(group.Users);
            Roles = new List<string>(group.Roles);
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

        public void Sanitize()
        {
            Name = Sanitizer.SanitizeMand(Name);
        }
    }
}
