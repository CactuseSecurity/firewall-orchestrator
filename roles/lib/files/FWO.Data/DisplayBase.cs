using System.Text;
using NetTools;
using FWO.Logging;
using System.Net;
using FWO.Basics;

namespace FWO.Data
{
    public static class DisplayBase
    {
        public static StringBuilder DisplayGateway(Device gateway, bool isTechReport, string? gatewayName = null)
        {
            StringBuilder result = new();
            result.Append($" {gateway.Name}");
            return result;
        }
        /// <summary>
        /// Displays a service with its source and destination port restrictions.
        /// </summary>
        public static StringBuilder DisplayService(NetworkService service, bool isTechReport, string? serviceName = null)
        {
            StringBuilder result = new();
            if (isTechReport)
            {
                result.Append(DisplayTechnicalService(service));
            }
            else
            {
                result.Append($"{serviceName ?? service.Name}");
                result.Append(DisplayServiceDetails(service));
            }
            return result;
        }

        /// <summary>
        /// Formats source-to-destination ports, retaining the previous format for unrestricted sources.
        /// </summary>
        public static string DisplayServicePorts(NetworkService service, bool inBrackets = false)
        {
            string ports = DisplayPort(service.DestinationPort, service.DestinationPortEnd);
            if (service.SourcePort != null)
            {
                string destination = ports.Length == 0 ? "*" : ports;
                ports = $"{DisplayPort(service.SourcePort, service.SourcePortEnd)} → {destination}";
            }
            return inBrackets && ports.Length > 0 ? $" ({ports})" : ports;
        }

        /// <summary>
        /// Formats ports with protocol for selection lists, prefixing a source port restriction when present.
        /// </summary>
        public static string DisplayServicePortsWithProtocol(NetworkService service, string? protocolName)
        {
            string destination = DisplayPort(service.DestinationPort, service.DestinationPortEnd, false, protocolName);
            if (service.SourcePort == null)
            {
                return string.IsNullOrWhiteSpace(destination) ? protocolName ?? "" : destination;
            }
            if (string.IsNullOrWhiteSpace(destination))
            {
                destination = protocolName == null ? "*" : $"*/{protocolName}";
            }
            return $"{DisplayPort(service.SourcePort, service.SourcePortEnd)} → {destination}";
        }

        /// <summary>
        /// Builds the port and protocol part that follows the service name in a non-technical display,
        /// e.g. " (443/tcp)". Html output prepends the encoded name or a link to it.
        /// </summary>
        /// <param name="service">The service to describe.</param>
        /// <returns>The plain text suffix, empty if neither ports nor protocol are shown.</returns>
        public static string DisplayServiceDetails(NetworkService service)
        {
            if (ShouldDisplayServicePorts(service))
            {
                string ports = DisplayServicePorts(service);
                return $" ({ports}/{service.Protocol?.Name})";
            }
            if (ShouldDisplayProtocolName(service))
            {
                return $" ({service.Protocol?.Name})";
            }
            return "";
        }

        /// <summary>Formats technical service output without a service name when ports or a protocol are available.</summary>
        private static string DisplayTechnicalService(NetworkService service)
        {
            if (ShouldDisplayServicePorts(service))
            {
                string ports = DisplayServicePorts(service);
                return $"{ports}/{service.Protocol?.Name}";
            }
            return ShouldDisplayProtocolName(service) ? $"{service.Protocol?.Name}" : $"{service.Name}";
        }

        /// <summary>Checks whether the protocol supports ports and either direction has a restriction.</summary>
        private static bool ShouldDisplayServicePorts(NetworkService service)
        {
            return IpProtocol.HasPorts(service.Protocol?.Id) && (service.DestinationPort != null || service.SourcePort != null);
        }

        public static string DisplayService(Workflow.NwServiceElement svcElem, List<IpProtocol> IpProtos)
        {
            IpProtocol? ipProt = IpProtos.FirstOrDefault(p => p.Id == svcElem.ProtoId);
            NetworkService svc = new()
            {
                DestinationPort = svcElem.Port,
                DestinationPortEnd = svcElem.PortEnd,
                Protocol = ipProt != null ? new NetworkProtocol(ipProt) : new NetworkProtocol()
            };
            return DisplayService(svc, true).ToString();
        }

        public static List<IpProtocol> CustomSortProtocols(List<IpProtocol> ListIn)
        {
            return ProtocolDisplayHelper.CustomSortProtocols(ListIn, null, false);
        }

        public static string DisplayIpWithName(NetworkObject elem)
        {
            if (elem.Name != null && elem.Name != "")
            {
                return elem.Name + DisplayIp(elem.IP ?? "", elem.IpEnd ?? "", true);
            }
            return DisplayIp(elem.IP ?? "", elem.IpEnd ?? "");
        }

        public static string DisplayIp(string ip1, string ip2, bool inBrackets = false)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(ip1) && string.IsNullOrWhiteSpace(ip2))
                {
                    return "";
                }
                if (string.IsNullOrEmpty(ip1) && !string.IsNullOrEmpty(ip2))
                {
                    ip1 = ip2;
                }
                if (ip2 == "")
                {
                    ip2 = ip1;
                }

                string nwObjType = IpOperations.GetObjectType(ip1, ip2);
                return DisplayIp(ip1, ip2, nwObjType, inBrackets);
            }
            catch (Exception exc)
            {
                Log.WriteError("Ip displaying", $"Exception thrown: {exc.Message}");
                return "";
            }
        }

        /// <summary>Formats addresses according to their object type, converting network ranges to CIDR when possible.</summary>
        public static string DisplayIp(string ip1, string ip2, string nwObjType, bool inBrackets = false)
        {
            if (nwObjType == ObjectType.Group)
            {
                return "";
            }
            if (string.IsNullOrEmpty(ip2))
            {
                ip2 = ip1;
            }
            if (string.IsNullOrEmpty(ip1))
            {
                Log.WriteDebug("Ip displaying", $"Nessessary parameter {nameof(ip1)} is empty.");
                return "";
            }
            if (!CanDisplayIpAddresses(ip1, ip2))
            {
                return "";
            }

            string ipStart = ip1.StripOffUnnecessaryNetmask();
            string ipEnd = ip2.StripOffUnnecessaryNetmask();
            string result = inBrackets ? " (" : "";
            try
            {
                result += FormatIpAddress(ipStart, ipEnd, nwObjType);
            }
            catch (Exception exc)
            {
                Log.WriteWarning("Ip displaying", $"Wrong ip format {ipStart} - {ipEnd}\nMessage: {exc.Message}");
                result += $"{ipStart}-{ipEnd}";
            }
            result += inBrackets ? ")" : "";
            return result;
        }

        /// <summary>Formats a network as CIDR or preserves the endpoints of a host or address range.</summary>
        private static string FormatIpAddress(string ipStart, string ipEnd, string objectType)
        {
            if (objectType == ObjectType.Network)
            {
                return FormatNetworkAddress(ipStart, ipEnd);
            }
            return objectType == ObjectType.IPRange ? $"{ipStart}-{ipEnd}" : ipStart;
        }

        /// <summary>Converts network endpoints to CIDR, retaining non-CIDR ranges as text.</summary>
        private static string FormatNetworkAddress(string ipStart, string ipEnd)
        {
            if (ipStart.GetNetmask() != "")
            {
                return ipStart;
            }
            if (ipStart.IsGreater(ipEnd))
            {
                Log.WriteWarning("Ip displaying", $"Wrong ip format {ipStart} - {ipEnd} - swapping values");
                string previousStart = ipStart;
                ipStart = ipEnd;
                ipEnd = previousStart;
            }
            IPAddressRange ipRange = new(IPAddress.Parse(ipStart), IPAddress.Parse(ipEnd));
            try
            {
                return ipRange.ToCidrString();
            }
            catch (Exception exc)
            {
                Log.WriteWarning("Ip displaying", $"Wrong ip format {ipStart} - {ipEnd} is not a network\nMessage: {exc.Message}");
                return $"{ipStart}-{ipEnd}";
            }
        }

        /// <summary>Formats inferred address types while retaining the endpoints of network ranges.</summary>
        public static string DisplayIpRange(string ip1, string ip2, bool inBrackets = false)
        {
            string nwObjType = IpOperations.GetObjectType(ip1, ip2);
            if (nwObjType == ObjectType.Group || !CanDisplayIpAddresses(ip1, ip2))
            {
                return "";
            }
            if (ip2 == "")
            {
                ip2 = ip1;
            }
            string ipStart = ip1.StripOffUnnecessaryNetmask();
            string ipEnd = ip2.StripOffUnnecessaryNetmask();
            string result = inBrackets ? " (" : "";
            try
            {
                result += FormatIpRange(ipStart, ipEnd, nwObjType);
                result += inBrackets ? ")" : "";
            }
            catch (Exception exc)
            {
                Log.WriteError("Ip displaying", $"Wrong ip format {ipStart} - {ipEnd}\nMessage: {exc.Message}");
            }
            return result;
        }

        /// <summary>Formats network endpoints without CIDR conversion, preserving explicit CIDR notation.</summary>
        private static string FormatIpRange(string ipStart, string ipEnd, string objectType)
        {
            if (objectType == ObjectType.Network && ipStart.GetNetmask() == "")
            {
                IPAddressRange ipRange = new(IPAddress.Parse(ipStart), IPAddress.Parse(ipEnd));
                return $"{ipRange.Begin}-{ipRange.End}";
            }
            return objectType == ObjectType.IPRange ? $"{ipStart}-{ipEnd}" : ipStart;
        }

        /// <summary>Checks the existing address-family rules and logs unsupported or mixed families.</summary>
        private static bool CanDisplayIpAddresses(string ipStart, string ipEnd)
        {
            if (!ipStart.IsV4Address() && !ipStart.IsV6Address())
            {
                Log.WriteError("Ip displaying", $"Found undefined IP family: {ipStart} - {ipEnd}");
                return false;
            }
            if (ipStart.IsV4Address() == ipEnd.IsV6Address())
            {
                Log.WriteError("Ip displaying", $"Found mixed IP family: {ipStart} - {ipEnd}");
                return false;
            }
            return true;
        }

        public static string DisplayPort(int? port, int? portEnd, bool inBrackets = false, string? proto = null)
        {
            string result = "";
            if (port != null)
            {
                result = inBrackets ? " (" : "";
                if (portEnd == null || portEnd == 0 || port == portEnd)
                {
                    result += $"{port}";
                }
                else
                {
                    result += $"{port}-{portEnd}";
                }
                result += proto != null ? $"/{proto}" : "";
                result += inBrackets ? ")" : "";
            }
            return result;
        }

        public static string MemberNamesAsJson(string MemberNames)
        {
            // Falls MemberNames leer ist, leeres Array zurückgeben
            if (string.IsNullOrEmpty(MemberNames))
                return "[]";

            // Splitten, trimmen
            var members = MemberNames
                .Split('|', StringSplitOptions.RemoveEmptyEntries)
                .Select(m => m.Trim())
                .ToArray();

            // Wenn nach Split nichts übrig bleibt, leeres Array
            if (members.Length == 0)
                return "[]";

            // Jedes Element in Anführungszeichen setzen
            var quoted = members.Select(m => $"\"{m}\"");

            // Mit Komma verbinden und in eckige Klammern setzen
            return $"[{string.Join(",", quoted)}]";
        }

        /// <summary>
        /// Renders the members of a group as the cell of a report table.
        /// The member names are imported values, so each one is encoded before the separators this method
        /// adds itself are put between them - the break is markup this code means, the name is not.
        /// </summary>
        /// <param name="MemberNames">The member names as the importer stored them, separated by '|'.</param>
        /// <returns>The table cell holding the member names.</returns>
        public static string MemberNamesAsHtml(string MemberNames)
        {
            return $"<td>{MemberNamesWithoutHtml(MemberNames)}</td>";
        }

        /// <summary>
        /// Renders the members of a group without the surrounding table cell, for callers that place them
        /// in markup of their own. The member names are encoded, only the line breaks between them are markup.
        /// </summary>
        /// <param name="MemberNames">The member names as the importer stored them, separated by '|'.</param>
        /// <returns>The member names, one per line.</returns>
        public static string MemberNamesWithoutHtml(string MemberNames)
        {
            if (MemberNames == null)
            {
                return "";
            }

            return string.Join("<br>", MemberNames.Split('|').Select(HtmlOutputEncoder.EncodeText));
        }

        public static string MemberNamesAsCSV(string MemberNames)
        {
            if (MemberNames != null && MemberNames.Contains('|'))
            {
                return $"{string.Join(",", MemberNames.Split('|'))}";
            }
            else
            {
                return $"{MemberNames}";
            }
        }

        private static bool ShouldDisplayProtocolName(NetworkService service)
        {
            return service.Protocol?.Name != null &&
                   !(service.Protocol.Id == 0 &&
                     (service.Name.Equals("Any") || service.Name.Equals("ALL")));
        }
    }
}
