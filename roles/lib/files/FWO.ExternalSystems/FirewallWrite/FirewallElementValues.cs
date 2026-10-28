using FWO.Basics;
using FWO.Data.DeviceSpecificSettings;
using FWO.Data.Workflow;
using NetTools;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Numerics;

namespace FWO.ExternalSystems.FirewallWrite
{
    /// <summary>
    /// Builds the steps and placeholder values of single network and service elements of an implementation task.
    /// </summary>
    public static class FirewallElementValues
    {
        private const int kIpv4Bits = 32;
        private const int kBitsPerByte = 8;
        private const int kIpProtoIcmp = 1;
        private const int kIpProtoTcp = 6;
        private const int kIpProtoUdp = 17;
        private const string kRangeSeparator = "-";
        private const char kScopeSeparator = '%';

        private static readonly Dictionary<int, string> ProtocolNames = new()
        {
            { kIpProtoIcmp, "icmp" },
            { kIpProtoTcp, "tcp" },
            { kIpProtoUdp, "udp" }
        };

        /// <summary>Whether an element describes a network object (source, destination or modelled address).</summary>
        public static bool IsNetworkElement(WfImplElement element) =>
            element.Field != nameof(ElemFieldType.service) && element.Field != nameof(ElemFieldType.rule) && element.Field != nameof(ElemFieldType.time);

        /// <summary>Whether an element describes a service.</summary>
        public static bool IsServiceElement(WfImplElement element) => element.Field == nameof(ElemFieldType.service);

        /// <summary>
        /// Creates the step that creates, modifies or deletes the network object of an element. The object type (host,
        /// network or address range) and the IP version follow from its addresses, IPv6 objects use the Ipv6 operations; an
        /// element without addresses is deleted as (IPv4) host.
        /// </summary>
        public static FirewallWriteStep NetworkObjectStep(WfImplElement element, string action, FirewallVendorProfile profile, string comment)
        {
            string name = RequireName(element, profile);
            if (string.IsNullOrWhiteSpace(element.IpString))
            {
                if (action != nameof(RequestAction.delete))
                {
                    throw new FirewallWriteException(FirewallWriteErrorKind.InvalidTaskData, $"Network object '{name}' has no IP address.");
                }
                return new FirewallWriteStep(FirewallWriteOperation.HostDelete) { TargetName = name }
                    .SetText(FirewallWritePlaceholders.ObjectName, name)
                    .SetText(FirewallWritePlaceholders.ObjectNameInUrl, profile.NameInUrl(name));
            }

            (string start, string end) = AddressRange(element.IpString, element.IpEnd);
            string objectType = IpOperations.GetObjectType(element.IpString, element.IpEnd ?? "");
            bool isIpv6 = IPAddress.Parse(start).AddressFamily == AddressFamily.InterNetworkV6;
            FirewallWriteStep step = new(NetworkOperation(objectType, action, isIpv6)) { TargetName = name, IsIpv6 = isIpv6 };
            List<string> addresses = [AddressNotation(element)];
            step.SetText(FirewallWritePlaceholders.ObjectName, name)
                .SetText(FirewallWritePlaceholders.ObjectNameInUrl, profile.NameInUrl(name))
                .SetText(FirewallWritePlaceholders.Ip, start)
                .SetText(FirewallWritePlaceholders.IpEnd, end)
                .SetText(FirewallWritePlaceholders.Comment, comment)
                .SetStringList(FirewallWritePlaceholders.ObjectAddresses, addresses);
            SetSubnetValues(step, start, end);
            return step;
        }

        /// <summary>Creates the step that creates, modifies or deletes the service of an element.</summary>
        public static FirewallWriteStep ServiceStep(WfImplElement element, string action, FirewallVendorProfile profile, string comment)
        {
            string name = ServiceName(element);
            FirewallWriteOperation operation = action switch
            {
                nameof(RequestAction.create) => FirewallWriteOperation.ServiceCreate,
                nameof(RequestAction.modify) => FirewallWriteOperation.ServiceModify,
                nameof(RequestAction.delete) => FirewallWriteOperation.ServiceDelete,
                _ => throw new FirewallWriteException(FirewallWriteErrorKind.InvalidTaskData, $"Unsupported service action '{action}'.")
            };
            int port = element.Port ?? 0;
            int portEnd = element.PortEnd ?? port;
            return new FirewallWriteStep(operation) { TargetName = name }
                .SetText(FirewallWritePlaceholders.ServiceName, name)
                .SetText(FirewallWritePlaceholders.ServiceNameInUrl, profile.NameInUrl(name))
                .SetText(FirewallWritePlaceholders.Protocol, ProtocolName(element.ProtoId))
                .SetText(FirewallWritePlaceholders.ProtocolId, (element.ProtoId ?? 0).ToString(CultureInfo.InvariantCulture))
                .SetText(FirewallWritePlaceholders.Port, port.ToString(CultureInfo.InvariantCulture))
                .SetText(FirewallWritePlaceholders.PortEnd, portEnd.ToString(CultureInfo.InvariantCulture))
                .SetText(FirewallWritePlaceholders.PortRange, PortRange(element))
                .SetText(FirewallWritePlaceholders.Comment, comment);
        }

        /// <summary>
        /// Returns the name an element is referenced by on the firewall. Fails for an element without name, so that a
        /// rule or group is never written with a silently dropped member.
        /// </summary>
        public static string RequireName(WfImplElement element, FirewallVendorProfile profile)
        {
            if (string.IsNullOrWhiteSpace(element.Name))
            {
                throw new FirewallWriteException(FirewallWriteErrorKind.InvalidTaskData, $"Element '{element.Id}' ({element.Field}) has no object name.");
            }
            return profile.NormalizeName(element.Name.Trim());
        }

        /// <summary>
        /// Returns the group entries of a member for vendors whose groups hold literal addresses (OPNsense): the canonical
        /// CIDR blocks covering its address, host or range, so that later per-entry changes match the entries written at
        /// creation. Members without an address cannot be written this way and are refused before anything is sent.
        /// </summary>
        public static List<string> GroupAddressEntries(WfImplElement element)
        {
            if (string.IsNullOrWhiteSpace(element.IpString))
            {
                throw new FirewallWriteException(FirewallWriteErrorKind.Unsupported,
                    $"Group member '{element.Name}' has no IP address. Groups of this firewall hold literal addresses only.");
            }
            string addressText = string.IsNullOrWhiteSpace(element.IpEnd)
                ? element.IpString.Trim()
                : element.IpString.StripOffNetmask().Trim() + kRangeSeparator + element.IpEnd.StripOffNetmask().Trim();
            if (addressText.Contains(kScopeSeparator) || !IPAddressRange.TryParse(addressText, out IPAddressRange? range))
            {
                throw new FirewallWriteException(FirewallWriteErrorKind.InvalidTaskData, $"Group member '{element.Name}' has an invalid address '{addressText}'.");
            }
            return CidrBlocks(range.Begin, range.End);
        }

        /// <summary>Returns the service name of an element, or a name built from protocol and ports if it has none.</summary>
        public static string ServiceName(WfImplElement element)
        {
            return string.IsNullOrWhiteSpace(element.Name)
                ? $"{ProtocolName(element.ProtoId)}_{PortRange(element)}"
                : element.Name.Trim();
        }

        /// <summary>Returns the address of a network element in CIDR notation, or "start-end" for a range.</summary>
        public static string AddressNotation(WfImplElement element)
        {
            if (string.IsNullOrWhiteSpace(element.IpString))
            {
                throw new FirewallWriteException(FirewallWriteErrorKind.InvalidTaskData, $"Element '{element.Name}' has no IP address.");
            }
            (string start, string end) = AddressRange(element.IpString, element.IpEnd);
            return IpOperations.GetObjectType(element.IpString, element.IpEnd ?? "") == ObjectType.IPRange
                ? start + kRangeSeparator + end
                : $"{start}/{PrefixLength(start, end)}";
        }

        /// <summary>Returns "port" or "port-portEnd", or "" for protocols without ports.</summary>
        public static string PortRange(WfImplElement element)
        {
            if (element.Port == null)
            {
                return "";
            }
            return element.PortEnd == null || element.PortEnd == element.Port
                ? element.Port.Value.ToString(CultureInfo.InvariantCulture)
                : $"{element.Port.Value.ToString(CultureInfo.InvariantCulture)}{kRangeSeparator}{element.PortEnd.Value.ToString(CultureInfo.InvariantCulture)}";
        }

        /// <summary>Returns the lower case protocol name for tcp, udp and icmp, the protocol number otherwise.</summary>
        public static string ProtocolName(int? protoId)
        {
            return protoId != null && ProtocolNames.TryGetValue(protoId.Value, out string? name)
                ? name
                : (protoId ?? 0).ToString(CultureInfo.InvariantCulture);
        }

        private static FirewallWriteOperation NetworkOperation(string objectType, string action, bool isIpv6)
        {
            (FirewallWriteOperation create, FirewallWriteOperation modify, FirewallWriteOperation delete) = (objectType, isIpv6) switch
            {
                (ObjectType.Network, false) => (FirewallWriteOperation.NetworkCreate, FirewallWriteOperation.NetworkModify, FirewallWriteOperation.NetworkDelete),
                (ObjectType.Network, true) => (FirewallWriteOperation.Ipv6NetworkCreate, FirewallWriteOperation.Ipv6NetworkModify, FirewallWriteOperation.Ipv6NetworkDelete),
                (ObjectType.IPRange, false) => (FirewallWriteOperation.AddressRangeCreate, FirewallWriteOperation.AddressRangeModify, FirewallWriteOperation.AddressRangeDelete),
                (ObjectType.IPRange, true) => (FirewallWriteOperation.Ipv6AddressRangeCreate, FirewallWriteOperation.Ipv6AddressRangeModify,
                    FirewallWriteOperation.Ipv6AddressRangeDelete),
                (_, false) => (FirewallWriteOperation.HostCreate, FirewallWriteOperation.HostModify, FirewallWriteOperation.HostDelete),
                (_, true) => (FirewallWriteOperation.Ipv6HostCreate, FirewallWriteOperation.Ipv6HostModify, FirewallWriteOperation.Ipv6HostDelete)
            };
            return action switch
            {
                nameof(RequestAction.create) => create,
                nameof(RequestAction.modify) => modify,
                nameof(RequestAction.delete) => delete,
                _ => throw new FirewallWriteException(FirewallWriteErrorKind.InvalidTaskData, $"Unsupported network object action '{action}'.")
            };
        }

        /// <summary>Returns start and end address, without netmask; a single CIDR value is expanded to its range.</summary>
        private static (string Start, string End) AddressRange(string ip, string? ipEnd)
        {
            return string.IsNullOrWhiteSpace(ipEnd)
                ? IpOperations.SplitIpToRange(ip.Trim())
                : (ip.StripOffNetmask(), ipEnd.StripOffNetmask());
        }

        private static void SetSubnetValues(FirewallWriteStep step, string start, string end)
        {
            int prefixLength = PrefixLength(start, end);
            step.SetText(FirewallWritePlaceholders.Subnet, start)
                .SetText(FirewallWritePlaceholders.MaskLength, prefixLength.ToString(CultureInfo.InvariantCulture))
                .SetText(FirewallWritePlaceholders.IpCidr, $"{start}/{prefixLength}");
            if (step.IsIpv6)
            {
                step.SetMissing(FirewallWritePlaceholders.Netmask, "an IPv6 address has no dotted netmask; use @@IP_CIDR@@ or @@MASK_LENGTH@@",
                    FirewallWriteErrorKind.Configuration);
            }
            else
            {
                step.SetText(FirewallWritePlaceholders.Netmask, Netmask(prefixLength));
            }
        }

        /// <summary>Prefix length of the network spanned by the range; host length for a range that is no network.</summary>
        private static int PrefixLength(string start, string end)
        {
            IPAddress startAddress = IPAddress.Parse(start);
            try
            {
                return new IPAddressRange(startAddress, IPAddress.Parse(end)).GetPrefixLength();
            }
            catch (FormatException)
            {
                return startAddress.GetAddressBytes().Length * kBitsPerByte;
            }
        }

        /// <summary>
        /// Splits an address range into the smallest list of aligned CIDR blocks that covers it exactly; at most two blocks
        /// per address bit.
        /// </summary>
        private static List<string> CidrBlocks(IPAddress first, IPAddress last)
        {
            int addressBytes = first.GetAddressBytes().Length;
            int addressBits = addressBytes * kBitsPerByte;
            BigInteger current = ToNumber(first);
            BigInteger end = ToNumber(last);
            List<string> blocks = [];
            while (current <= end)
            {
                int prefixLength = addressBits;
                while (prefixLength > 0)
                {
                    BigInteger widerSize = BigInteger.One << (addressBits - prefixLength + 1);
                    if (!(current % widerSize).IsZero || current + widerSize - 1 > end)
                    {
                        break;
                    }
                    prefixLength--;
                }
                blocks.Add($"{ToAddress(current, addressBytes)}/{prefixLength}");
                current += BigInteger.One << (addressBits - prefixLength);
            }
            return blocks;
        }

        private static BigInteger ToNumber(IPAddress address) => new(address.GetAddressBytes(), isUnsigned: true, isBigEndian: true);

        private static IPAddress ToAddress(BigInteger number, int addressBytes)
        {
            byte[] significantBytes = number.ToByteArray(isUnsigned: true, isBigEndian: true);
            byte[] bytes = new byte[addressBytes];
            int skippedBytes = Math.Max(0, significantBytes.Length - addressBytes);
            significantBytes.AsSpan(skippedBytes).CopyTo(bytes.AsSpan(addressBytes - (significantBytes.Length - skippedBytes)));
            return new IPAddress(bytes);
        }

        /// <summary>Dotted IPv4 netmask of a prefix length.</summary>
        private static string Netmask(int prefixLength)
        {
            uint mask = prefixLength == 0 ? 0 : uint.MaxValue << (kIpv4Bits - prefixLength);
            return IpOperations.UintToIp(mask).ToString();
        }
    }
}
