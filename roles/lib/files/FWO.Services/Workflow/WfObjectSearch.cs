using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Data;
using FWO.Data.Workflow;
using System.Net;
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

        /// <summary>
        /// Searches network objects by ip address, when the text is one, or else by a part of the name.
        /// Texts shorter than WfObjectTaskHelper.kMinSearchLength return no hits without querying the api.
        /// </summary>
        public async Task<List<NetworkObject>> SearchNetworkObjects(int managementId, string searchText)
        {
            string text = searchText.Trim();
            if (text.Length < WfObjectTaskHelper.kMinSearchLength)
            {
                return [];
            }
            if (IPAddress.TryParse(text.StripOffNetmask(), out _))
            {
                return await apiConnection.RunWithWorkflowRole(user, () =>
                    apiConnection.SendQueryAsync<List<NetworkObject>>(ObjectQueries.searchNetworkObjectsForRequestByIp, new
                    {
                        mgmId = managementId,
                        objTypeIds = WfObjectTaskHelper.NetworkObjectTypeIds,
                        ip = text,
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
