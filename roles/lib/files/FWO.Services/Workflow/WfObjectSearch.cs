using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Data;
using FWO.Data.Workflow;
using NetTools;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Claims;
using System.Text;

namespace FWO.Services.Workflow
{
    /// <summary>
    /// Server side search for the imported object or service an object_modify request task refers to.
    /// The search runs with the workflow role scope, which prefers the requester role - the only workflow role
    /// permitted to read imported objects - and returns at most WfObjectTaskHelper.kSearchLimit hits of one
    /// management. Searching is only offered while editing a request task, which requires the requester role.
    /// </summary>
    public class WfObjectSearch(ApiConnection apiConnection, ClaimsPrincipal user)
    {
        private const char kLikeEscape = '\\';
        private const char kLikeWildcard = '%';
        private const char kLikeSingleCharacter = '_';
        private const char kPrefixSeparator = '/';
        private const char kOctetSeparator = '.';
        private const int kIpv4OctetCount = 4;
        private const int kMaxOctetLength = 3;
        private const int kIpv4Bits = 32;
        private const int kIpv6Bits = 128;

        /// <summary>
        /// Searches network objects by ip address, when the text is a complete address or network (see
        /// ToCidrSearchValue), or else by a part of the name.
        /// Texts shorter than WfObjectTaskHelper.kMinSearchLength return no hits without querying the api.
        /// </summary>
        public async Task<List<NetworkObject>> SearchNetworkObjects(int managementId, string searchText)
        {
            string text = searchText.Trim();
            if (text.Length < WfObjectTaskHelper.kMinSearchLength)
            {
                return [];
            }
            string? cidr = ToCidrSearchValue(text);
            if (cidr != null)
            {
                return await apiConnection.RunWithWorkflowRole(user, () =>
                    apiConnection.SendQueryAsync<List<NetworkObject>>(ObjectQueries.searchNetworkObjectsForRequestByIp, new
                    {
                        mgmId = managementId,
                        objTypeIds = WfObjectTaskHelper.NetworkObjectTypeIds,
                        ip = cidr,
                        limit = WfObjectTaskHelper.kSearchLimit
                    }));
            }
            return await apiConnection.RunWithWorkflowRole(user, () =>
                apiConnection.SendQueryAsync<List<NetworkObject>>(ObjectQueries.searchNetworkObjectsForRequestByName, new
                {
                    mgmId = managementId,
                    objTypeIds = WfObjectTaskHelper.NetworkObjectTypeIds,
                    pattern = ToContainsPattern(text),
                    limit = WfObjectTaskHelper.kSearchLimit
                }));
        }

        /// <summary>
        /// Searches services by port, when the text is a number, or else by a part of the name.
        /// Texts shorter than WfObjectTaskHelper.kMinSearchLength return no hits without querying the api,
        /// except for a port number, which may be shorter.
        /// </summary>
        public async Task<List<NetworkService>> SearchServices(int managementId, string searchText)
        {
            string text = searchText.Trim();
            if (int.TryParse(text, out int port) && port is >= 1 and <= GlobalConst.kMaxPortNumber)
            {
                return await apiConnection.RunWithWorkflowRole(user, () =>
                    apiConnection.SendQueryAsync<List<NetworkService>>(ObjectQueries.searchNetworkServicesForRequestByPort, new
                    {
                        mgmId = managementId,
                        svcTypeIds = WfObjectTaskHelper.ServiceTypeIds,
                        port,
                        limit = WfObjectTaskHelper.kSearchLimit
                    }));
            }
            if (text.Length < WfObjectTaskHelper.kMinSearchLength)
            {
                return [];
            }
            return await apiConnection.RunWithWorkflowRole(user, () =>
                apiConnection.SendQueryAsync<List<NetworkService>>(ObjectQueries.searchNetworkServicesForRequestByName, new
                {
                    mgmId = managementId,
                    svcTypeIds = WfObjectTaskHelper.ServiceTypeIds,
                    pattern = ToContainsPattern(text),
                    limit = WfObjectTaskHelper.kSearchLimit
                }));
        }

        /// <summary>
        /// Returns the canonical cidr literal for a completely typed address or network, or null when the text is
        /// none and is to be searched as name. IPAddress.TryParse alone accepts short forms like "4711" or "10.1.1",
        /// which PostgreSQL rejects as cidr or reads as a different network; a host with mask ("10.1.1.5/24") is
        /// reduced to its network, since cidr does not accept bits right of the mask.
        /// </summary>
        public static string? ToCidrSearchValue(string text)
        {
            string[] parts = text.Split(kPrefixSeparator);
            if (parts.Length > 2 || !IPAddress.TryParse(parts[0], out IPAddress? address) || !IsCompleteNotation(parts[0], address))
            {
                return null;
            }
            if (parts.Length == 1)
            {
                return address.ToString();
            }
            int maxPrefix = address.AddressFamily == AddressFamily.InterNetwork ? kIpv4Bits : kIpv6Bits;
            if (!int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int prefix) || prefix > maxPrefix)
            {
                return null;
            }
            return new IPAddressRange(address, prefix).ToCidrString();
        }

        private static bool IsCompleteNotation(string text, IPAddress address)
        {
            return address.AddressFamily switch
            {
                AddressFamily.InterNetwork => IsDottedDecimal(text),
                AddressFamily.InterNetworkV6 => address.ScopeId == 0,
                _ => false
            };
        }

        private static bool IsDottedDecimal(string text)
        {
            string[] octets = text.Split(kOctetSeparator);
            // leading zeros are rejected: .NET and PostgreSQL disagree whether "010" is octal
            return octets.Length == kIpv4OctetCount && octets.All(octet => octet.Length is > 0 and <= kMaxOctetLength
                && octet.All(char.IsAsciiDigit) && (octet.Length == 1 || octet[0] != '0'));
        }

        /// <summary>
        /// Builds an ilike pattern matching names that contain the text literally, so that a typed % or _
        /// is not taken as wildcard.
        /// </summary>
        public static string ToContainsPattern(string text)
        {
            StringBuilder pattern = new();
            pattern.Append(kLikeWildcard);
            foreach (char character in text)
            {
                if (character == kLikeEscape || character == kLikeWildcard || character == kLikeSingleCharacter)
                {
                    pattern.Append(kLikeEscape);
                }
                pattern.Append(character);
            }
            pattern.Append(kLikeWildcard);
            return pattern.ToString();
        }
    }
}
