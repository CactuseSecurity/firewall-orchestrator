using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Data.Middleware;

namespace FWO.Test
{
    /// <summary>
    /// API connection answering the queries of the session revocation and the session check: it lists the local
    /// users, records which users' refresh tokens are revoked, and looks up the refresh token of a session.
    /// </summary>
    internal sealed class SessionRevocationApiConnection : SimulatedApiConnection
    {
        /// <summary>Local users returned by the user query.</summary>
        public List<UiUser> LocalUsers { get; } = [];

        /// <summary>User ids of each revocation, in the order of the revocations.</summary>
        public List<List<int>> RevokedUserIds { get; } = [];

        /// <summary>Raised by the user query, if set.</summary>
        public Exception? UserQueryException { get; set; }

        /// <summary>Raised by the revocation, if set.</summary>
        public Exception? RevocationException { get; set; }

        /// <summary>Raised by the refresh token lookup, if set.</summary>
        public Exception? RefreshTokenQueryException { get; set; }

        /// <summary>Owner of the live refresh token; 0 if the token is not live.</summary>
        public int RefreshTokenUserId { get; set; }

        /// <summary>Number of refresh tokens a revocation reports.</summary>
        public int RevokedRows { get; set; } = 1;

        public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
        {
            return Task.FromResult((QueryResponseType)Answer(query, variables));
        }

        private object Answer(string query, object? variables)
        {
            if (query == AuthQueries.getUserEmails)
            {
                return UserQueryException == null ? LocalUsers.Select(user => new UiUser(user)).ToList() : throw UserQueryException;
            }
            if (query == AuthQueries.revokeRefreshTokensOfUsers)
            {
                if (RevocationException != null)
                {
                    throw RevocationException;
                }
                List<int> userIds = [.. Variable<IEnumerable<int>>(variables, "userIds")];
                RevokedUserIds.Add(userIds);
                return new ReturnId { AffectedRows = RevokedRows };
            }
            if (query == AuthQueries.getRefreshToken)
            {
                if (RefreshTokenQueryException != null)
                {
                    throw RefreshTokenQueryException;
                }
                List<RefreshTokenInfo> tokens = [];
                if (RefreshTokenUserId > 0)
                {
                    tokens.Add(new RefreshTokenInfo { UserId = RefreshTokenUserId });
                }
                return tokens.ToArray();
            }
            throw new NotImplementedException($"unexpected query {query}");
        }

        private static T Variable<T>(object? variables, string name)
        {
            object? value = variables?.GetType().GetProperty(name)?.GetValue(variables);
            return value is T typedValue ? typedValue : throw new ArgumentException($"variable {name} is missing");
        }
    }
}
