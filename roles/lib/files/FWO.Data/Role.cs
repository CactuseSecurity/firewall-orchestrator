
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

        private static string LdapDnExtractName(string dn, string name)
        {
            string[] dnParts = dn.Split(',');
            foreach(string part in dnParts)
            {
                if(part.StartsWith("cn=", StringComparison.CurrentCultureIgnoreCase))
                {
                    // also replace "\\2C" with ","
                    if (part.Contains("\\2c", StringComparison.CurrentCultureIgnoreCase))
                    {
                       return $"[{part[3..].Replace("\\2c", ",", StringComparison.CurrentCultureIgnoreCase)}]";
                    }
                    else
                    {
                        return part[3..];
                    }
                }
            }
            return name;
        }

        public string UserList()
        {
            List<string> userNames = [];
            foreach(UiUser user in Users)
            {
                userNames.Add(LdapDnExtractName(user.Dn, user.Name));
            }
            return string.Join(", ", userNames);
        }
    }
}
