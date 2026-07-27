using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api.Data;
using FWO.Data;
using FWO.Data.Workflow;
using FWO.Middleware.Server;

namespace FWO.Test
{
    internal abstract class JobTestApiConnectionBase : SimulatedApiConnection
    {
        public List<string> Queries { get; } = [];

        public override void SetAuthHeader(string jwt) { }
        public override void SetRole(string role) { }
        public override void SetBestRole(System.Security.Claims.ClaimsPrincipal user, List<string> targetRoleList) { }
        public override void SwitchBack() { }
        public override void DisposeSubscriptions<T>() { }
        public override Task ReconnectSubscriptionsAsync(string jwt, CancellationToken ct) => Task.CompletedTask;
        protected override void Dispose(bool disposing) { }
        public override GraphQlApiSubscription<SubscriptionResponseType> GetSubscription<SubscriptionResponseType>(
            Action<Exception> exceptionHandler,
            GraphQlApiSubscription<SubscriptionResponseType>.SubscriptionUpdate subscriptionUpdateHandler,
            string subscription,
            object? variables = null,
            string? operationName = null)
        {
            throw new NotImplementedException();
        }

        public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(
            string query,
            object? variables = null,
            string? operationName = null,
            QueryChunkingOptions? chunkingOptions = null)
        {
            Queries.Add(query);
            return HandleQueryAsync<QueryResponseType>(query, variables, operationName, chunkingOptions);
        }

        public override Task<ApiResponse<QueryResponseType>> SendQuerySafeAsync<QueryResponseType>(
            string query,
            object? variables = null,
            string? operationName = null)
        {
            return base.SendQuerySafeAsync<QueryResponseType>(query, variables, operationName);
        }

        protected abstract Task<QueryResponseType> HandleQueryAsync<QueryResponseType>(
            string query,
            object? variables,
            string? operationName,
            QueryChunkingOptions? chunkingOptions);

        protected static Task<QueryResponseType> ReturnEmptyOrDefault<QueryResponseType>()
        {
            Type responseType = typeof(QueryResponseType);
            if (responseType.IsArray)
            {
                object emptyArray = Array.CreateInstance(responseType.GetElementType()!, 0);
                return Task.FromResult((QueryResponseType)emptyArray);
            }
            if (responseType.IsGenericType && responseType.GetGenericTypeDefinition() == typeof(List<>))
            {
                object emptyList = Activator.CreateInstance(responseType)
                    ?? throw new InvalidOperationException($"Could not create empty {responseType.Name}.");
                return Task.FromResult((QueryResponseType)emptyList);
            }

            return Task.FromResult(default(QueryResponseType)!);
        }
    }

    internal sealed class ThrowingJobApiConnection : JobTestApiConnectionBase
    {
        protected override Task<QueryResponseType> HandleQueryAsync<QueryResponseType>(
            string query,
            object? variables,
            string? operationName,
            QueryChunkingOptions? chunkingOptions)
        {
            throw new InvalidOperationException("boom");
        }
    }

    internal sealed class EmptyListJobApiConnection : JobTestApiConnectionBase
    {
        protected override Task<QueryResponseType> HandleQueryAsync<QueryResponseType>(
            string query,
            object? variables,
            string? operationName,
            QueryChunkingOptions? chunkingOptions)
        {
            return ReturnEmptyOrDefault<QueryResponseType>();
        }
    }

    internal sealed class AppDataImportNoOpApiConnection : JobTestApiConnectionBase
    {
        protected override Task<QueryResponseType> HandleQueryAsync<QueryResponseType>(
            string query,
            object? variables,
            string? operationName,
            QueryChunkingOptions? chunkingOptions)
        {
            if (typeof(QueryResponseType) == typeof(List<Ldap>) && query == AuthQueries.getLdapConnections)
            {
                return Task.FromResult((QueryResponseType)(object)new List<Ldap>
                {
                    new()
                    {
                        Id = 1,
                        UserSearchPath = "dc=fworch,dc=internal",
                        GroupSearchPath = "ou=groups,dc=fworch,dc=internal"
                    }
                });
            }

            if (typeof(QueryResponseType) == typeof(List<OwnerResponsibleType>) && query == OwnerQueries.getOwnerResponsibleTypes)
            {
                return Task.FromResult((QueryResponseType)(object)new List<OwnerResponsibleType>());
            }

            if (typeof(QueryResponseType) == typeof(List<OwnerLifeCycleState>) && query == OwnerQueries.getOwnerLifeCycleStates)
            {
                return Task.FromResult((QueryResponseType)(object)new List<OwnerLifeCycleState>());
            }

            if (typeof(QueryResponseType) == typeof(List<FwoNotification>) && query == NotificationQueries.getNotifications)
            {
                return Task.FromResult((QueryResponseType)(object)new List<FwoNotification>());
            }

            return ReturnEmptyOrDefault<QueryResponseType>();
        }
    }

    internal sealed class ExternalRequestNoOpApiConnection : JobTestApiConnectionBase
    {
        protected override Task<QueryResponseType> HandleQueryAsync<QueryResponseType>(
            string query,
            object? variables,
            string? operationName,
            QueryChunkingOptions? chunkingOptions)
        {
            if (typeof(QueryResponseType) == typeof(ExternalRequestDataHelper) && query == ExtRequestQueries.getAndLockOpenRequests)
            {
                return Task.FromResult((QueryResponseType)(object)new ExternalRequestDataHelper
                {
                    ExternalRequests = []
                });
            }

            if (typeof(QueryResponseType) == typeof(ReturnId) && query == ExtRequestQueries.updateExternalRequestLock)
            {
                return Task.FromResult((QueryResponseType)(object)new ReturnId { UpdatedIdLong = 0 });
            }

            return ReturnEmptyOrDefault<QueryResponseType>();
        }
    }
}
