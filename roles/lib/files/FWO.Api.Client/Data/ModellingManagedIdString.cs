namespace FWO.Api.Data
{
    public class ModellingManagedIdString
    {
        private string IdString = "";
        private const string separator = "-";

        public ModellingNamingConvention NamingConvention { get; set; } = new();


        public ModellingManagedIdString()
        {}

        public ModellingManagedIdString(string idstring)
        {
            IdString = idstring;
            NamingConvention = new();
        }

        public ModellingManagedIdString(ModellingManagedIdString managedIdstring)
        {
            IdString = managedIdstring.IdString;
            NamingConvention = managedIdstring.NamingConvention;
        }

        public string Whole
        {
            get
            {
                return IdString;
            }
            set
            {
                IdString = value;
            }
        }

        public string FixedPart
        {
            get
            {
                return IdString.Length >= NamingConvention.FixedPartLength ? IdString.Substring(0, NamingConvention.FixedPartLength) : IdString;
            }
            set
            {
                string valueToInsert = value.Length > NamingConvention.FixedPartLength ? value.Substring(0, NamingConvention.FixedPartLength) : value;
                valueToInsert = FillFixedIfNecessary(valueToInsert, "?");
                if (IdString.Length >= NamingConvention.FixedPartLength)
                {
                    IdString = valueToInsert + IdString.Substring(NamingConvention.FixedPartLength);
                }
                else
                {
                    IdString = valueToInsert;
                }
            }
        }

        public string AppPart
        {
            get
            {
                return NamingConvention.UseAppPart ? (AppPartExisting() ? IdString.Substring(NamingConvention.FixedPartLength, AppPartEnd() - NamingConvention.FixedPartLength + 1): "") : "";
            }
            set
            {
                if(NamingConvention.UseAppPart)
                {
                    IdString = FillFixedIfNecessary(IdString);
                    IdString = IdString.Substring(0, NamingConvention.FixedPartLength) + value + FreePart;
                }
            }
        }

        public string CombinedFixPart
        {
            get
            {
                return FixedPart + (AppPart.EndsWith(separator) ? AppPart.Substring(0, AppPart.Length - 1) : AppPart);
            }
            set
            {
                IdString = value + FreePart;
            }
        }

        public string Separator
        {
            get
            {
                return NamingConvention.UseAppPart && AppPart.EndsWith(separator) ? separator : "";
            }
            set
            {
                if(NamingConvention.UseAppPart)
                {
                    AppPart += value;
                }
            }
        }

        public string FreePart
        {
            get
            {
                return NamingConvention.UseAppPart && AppPartExisting() ? IdString.Substring(AppPartEnd() + 1) : IdString.Substring(NamingConvention.FixedPartLength);
            }
            set
            {
                IdString = FillFixedIfNecessary(IdString);
                IdString = IdString.Substring(0, AppPartExisting() ? AppPartEnd() + 1 : NamingConvention.FixedPartLength) + value;
            }
        }

        public void SetAppPartFromExtId(string extAppId)
        {
            string zoneType = extAppId.StartsWith("APP") ? "0" : (extAppId.StartsWith("COM") ? "1" :  "?");
            int idx = extAppId.IndexOf(separator);
            string appNumber = idx > 0 ? extAppId.Substring(idx + 1, extAppId.Length - idx - 1) : "";
            AppPart = zoneType + appNumber + separator;
        }

        public void ConvertAreaToAppRoleFixedPart (string areaIdString)
        {
            FixedPart = ConvertAreaToAppRole(areaIdString, NamingConvention);
        }

        public static string ConvertAreaToAppRole (string areaIdString, ModellingNamingConvention namingConvention)
        {
            if(areaIdString.Length >= namingConvention.FixedPartLength)
            {
                return areaIdString.Substring(0, namingConvention.FixedPartLength).Remove(0, namingConvention.NetworkAreaPattern.Length).Insert(0, namingConvention.AppRolePattern);
            }
            return areaIdString;
        }

        public static string ConvertAppRoleToArea (string appRoleIdString, ModellingNamingConvention namingConvention)
        {
            int convLength = namingConvention.AppRolePattern.Length > namingConvention.FixedPartLength ? namingConvention.FixedPartLength : namingConvention.AppRolePattern.Length;
            if(appRoleIdString.Length >= namingConvention.FixedPartLength)
            {
                return appRoleIdString.Substring(0, namingConvention.FixedPartLength).Remove(0, convLength).Insert(0, namingConvention.NetworkAreaPattern);
            }
            return "";
        }


        private int AppPartEnd() 
        {
            return IdString.IndexOf(separator);
        }

        private bool AppPartExisting()
        {
            return AppPartEnd() > NamingConvention.FixedPartLength && IdString.Length >= AppPartEnd();
        }

        private string FillFixedIfNecessary(string idString, string filler = " ")
        {
            if (idString.Length < NamingConvention.FixedPartLength)
            {
                int positionsToFill = NamingConvention.FixedPartLength - idString.Length;
                for (int i = 0; i < positionsToFill; i++)
                {
                    idString += filler;
                }
            }
            return idString;
        }
    }
}
