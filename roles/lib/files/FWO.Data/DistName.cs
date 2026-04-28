using FWO.Basics;
using System.Text;

namespace FWO.Data
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
                    int indexPrefixDelim = dn.IndexOf('=');
                    if (indexPrefixDelim > 0)
                    {
                        string name = dn[..indexPrefixDelim];
                        (string value, string remainingDn) = ReadDnValue(dn[(indexPrefixDelim + 1)..]);
                        dn = remainingDn;
                        lastValue = dn.Length == 0;

                        switch (name.ToLower())
                        {
                            case "uid":
                            case "samaccountname":
                            case "userprincipalname":
                            case "mail":
                                UserName = value;
                                break;
                            case "cn":
                                if (UserName == "")
                                {
                                    // the first one may be the user if not delivered as uid or a role or a group
                                    UserName = value;
                                    Role = value;
                                    Group = value;
                                }
                                else
                                {
                                    // following ones belong to the path
                                    Path.Add(value);
                                }
                                break;
                            case "ou":
                            case "o":
                            case "l":
                            case "st":
                            case "street":
                                Path.Add(value);
                                break;
                            case "dc":
                            case "c":
                                Root.Add(value);
                                Path.Add(value);
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

        private static (string value, string remainingDn) ReadDnValue(string dn)
        {
            StringBuilder value = new();
            int index = 0;

            while (index < dn.Length)
            {
                char currentChar = dn[index];

                if (currentChar == ',')
                {
                    return (value.ToString(), dn[(index + 1)..]);
                }

                if (currentChar == '\\')
                {
                    if (TryReadHexEscapedValue(dn, index, out string hexEscapedValue, out int nextIndex))
                    {
                        value.Append(hexEscapedValue);
                        index = nextIndex;
                        continue;
                    }

                    if (index + 1 < dn.Length)
                    {
                        value.Append(dn[index + 1]);
                        index += 2;
                        continue;
                    }
                }

                value.Append(currentChar);
                index++;
            }

            return (value.ToString(), "");
        }

        /// <summary>
        /// Normalizes a distinguished name for safe membership comparisons across equivalent escaped forms.
        /// </summary>
        /// <param name="dn">Distinguished name to normalize.</param>
        /// <returns>Normalized distinguished name.</returns>
        public static string NormalizeDnForComparison(string? dn)
        {
            if (string.IsNullOrWhiteSpace(dn))
            {
                return "";
            }

            StringBuilder normalizedDn = new();
            int index = 0;

            while (index < dn.Length)
            {
                if (TryReadHexEscapedValue(dn, index, out string decodedValue, out int nextIndex))
                {
                    normalizedDn.Append(decodedValue);
                    index = nextIndex;
                    continue;
                }

                if (dn[index] == '\\' && index + 1 < dn.Length)
                {
                    normalizedDn.Append(dn[index + 1]);
                    index += 2;
                    continue;
                }

                normalizedDn.Append(dn[index]);
                index++;
            }

            return normalizedDn.ToString().ToLowerInvariant();
        }

        private static bool TryReadHexEscapedValue(string dn, int currentIndex, out string decodedValue, out int nextIndex)
        {
            decodedValue = "";
            nextIndex = currentIndex;
            if (currentIndex + 2 >= dn.Length || !IsHexPair(dn, currentIndex + 1))
            {
                return false;
            }

            List<byte> escapedBytes = [];
            int scanIndex = currentIndex;

            do
            {
                escapedBytes.Add(Convert.ToByte(dn.Substring(scanIndex + 1, 2), 16));
                scanIndex += 3;
            }
            while (scanIndex + 2 < dn.Length && dn[scanIndex] == '\\' && IsHexPair(dn, scanIndex + 1));

            decodedValue = Encoding.UTF8.GetString([.. escapedBytes]);
            nextIndex = scanIndex;
            return true;
        }

        private static bool IsHexPair(string value, int startIndex)
        {
            return startIndex + 1 < value.Length
                && IsHexCharacter(value[startIndex])
                && IsHexCharacter(value[startIndex + 1]);
        }

        private static bool IsHexCharacter(char character)
        {
            return (character >= '0' && character <= '9')
                || (character >= 'a' && character <= 'f')
                || (character >= 'A' && character <= 'F');
        }

        public bool IsInternal()
        {
            return Root.Contains(GlobalConst.kFwoProdName) && Root.Contains("internal");
        }

        public string GetTenantNameViaLdapTenantLevel(int tenantLevel = 1)
        {
            return (tenantLevel > 0 && Path.Count >= tenantLevel) ? Path[^tenantLevel] : "";
        }
    }
}
