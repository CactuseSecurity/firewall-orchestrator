﻿using System.Collections.Generic;

namespace FWO.Api.Data
{
    public class DistName
    {
        public string UserName { get; set; }
        public string Role { get; set; }
        public string Group { get; set; }
        public List<string> Root { get; set; }
        public List<string> Path { get; set; }

        public DistName(string dn)
        {
            UserName = "";
            Role = "";
            Group = "";
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
                        case "samaccountname":
                        case "userprincipalname":
                        case "mail":
                            UserName = Value;
                            break;
                        case "cn":
                            if(UserName == "")
                            {
                                // the first one may be the user if not delivered as uid or a role or a group
                                UserName = Value;
                                Role = Value;
                                Group = Value;
                            }
                            else
                            {
                                // following ones belong to the path
                                Path.Add(Value);
                            }
                            break;
                        case "ou":
                            Path.Add(Value);
                            break;
                        case "dc":
                            Root.Add(Value);
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
            return Root.Contains("fworch") && Root.Contains("internal");
        }

        public string getTenant (int tenantLevel = 1)
        {
            return (tenantLevel > 0 && Path.Count >= tenantLevel) ? Path[tenantLevel - 1] : "";
        }
    }
}
