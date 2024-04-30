using FWO.GlobalConstants;

namespace FWO.Api.Data
{
	public class DistName
	{
		public string UserName { get; set; }
		public string Role { get; set; }
		public string Group { get; set; }
		public List<string> Root { get; set; }
		public List<string> Path { get; set; }

		public DistName(string? dn)
		{
			//Regex r = new Regex("(?:^|,\\s?)(?:(?<name>[A-Z]+)=(?<val>\"(?:[^\"]| \"\")+\"|(?:\\,|[^,])+))+");
			//GroupCollection groups = r.Match(dn ?? "").Groups;
			//foreach (string group in r.GetGroupNames())
			//{
			//    groups[group];
			//}

			UserName = "";
			Role = "";
			Group = "";
			Root = [];
			Path = [];
			bool lastValue = false;
			if (dn != null)
			{
				while (lastValue == false)
				{
					int IndexPrefixDelim = dn.IndexOf("=");
					if(IndexPrefixDelim > 0)
					{
						string Name = dn.Substring(0, IndexPrefixDelim);
						string Value;
						dn = dn.Substring (IndexPrefixDelim + 1);
						int IndexValueDelim = dn.IndexOf(",");
						if(IndexValueDelim > 0)
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
							case "o":
							case "l":
							case "st":
							case "street":
								Path.Add(Value);
								break;
							case "dc":
							case "c":
								Root.Add(Value);
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
		}

		public bool IsInternal()
		{
			return Root.Contains(GlobalConst.kFwoProdName) && Root.Contains("internal");
		}

		public string GetTenantNameViaLdapTenantLevel (int tenantLevel = 1)
		{
			return (tenantLevel > 0 && Path.Count >= tenantLevel) ? Path[Path.Count - tenantLevel] : "";
		}

	}
}
