using System.Collections.Generic;

namespace FWO.Api.Data
{
    public class DistName
    {
        public string UserName { get; set; }
        public string Role { get; set; }
        public List<string> Root { get; set; }
        public List<string> Path { get; set; }

        public DistName(string dn)
        {
            UserName = "";
            Role = "";
            Root = new List<string>();
            Path = new List<string>();
            bool lastValue = false;
            while(lastValue == false)
            {
                int IndexPrefixDelim = dn.IndexOf("=");
                if(IndexPrefixDelim > 0)
                {
                    string Name = dn.Substring(0, IndexPrefixDelim);
                    string Value;
                    dn = dn.Substring (IndexPrefixDelim + 1);
                    int IndexValueDelim = dn.IndexOf(",");
                    if(IndexValueDelim > 0)
                    {
                        Value = dn.Substring(0, IndexValueDelim);
                        dn = dn.Substring (IndexValueDelim + 1);
                    }
                    else
                    {
                        Value = dn;
                        lastValue = true;
                    }
                    switch (Name.ToLower())
                    {
                        case "uid": 
                            UserName = Value;
                            break;
                        case "cn": 
                            Role = Value;
                            break;
                        case "dc":
                            Root.Add(Value);
                            break;
                        case "ou":
                            Path.Add(Value);
                            break;
                        default: 
                            break;
                    }
                }
                else
                {
                    lastValue = true;
                }
            }
        }

        public bool IsInternal()
        {
            return (Root.Contains("fworch") && Root.Contains("internal"));
        }

        public string getTenant (int tenantLevel = 1)
        {
            return (Path.Count >= tenantLevel ? Path[tenantLevel - 1] : "");
        }
    }

}
