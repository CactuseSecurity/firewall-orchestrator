
namespace FWO.Data
{
    public class Role
    {
        public string Name { get; set; } = "";
        public string Dn { get; set; } = "";
        public string Description { get; set; } = "";
        public List<UiUser> Users { get; set; }

        public Role()
        { 
            Users = [];
        }

        public Role(Role role)
        {
            Name = role.Name;
            Dn = role.Dn;
            Description = role.Description;
            Users = new (role.Users);
        }

        private static string DisplayUserName(string name)
        {
            // replace encoded comma with real comma for displaying
            // also put name in square brackets if it contains commas
            if (name.Contains("\\2c", StringComparison.OrdinalIgnoreCase))
            {
                return $"[{name.Replace("\\2c", ",", StringComparison.OrdinalIgnoreCase)}]";
            }
            return name;
        }

        public string UserList()
        {
            List<string> userNames = [];
            foreach(UiUser user in Users)
            {
                userNames.Add(DisplayUserName(new DistName(user.Dn).UserName));
            }
            return string.Join(", ", userNames);
        }
    }
}
