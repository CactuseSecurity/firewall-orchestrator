namespace FWO.Api.Data
{
    public class Role
    {
        public string Name { get; set; } = "";
        public string Dn { get; set; } = "";
        public string Description { get; set; } = "";
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
            Users = new List<UiUser>(role.Users);
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
