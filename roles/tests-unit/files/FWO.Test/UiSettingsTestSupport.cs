using System.Security.Claims;
using System.Reflection;
using Bunit;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api.Data;
using FWO.Data;
using FWO.Data.Modelling;
using Microsoft.AspNetCore.Components.Authorization;

namespace FWO.Test
{
    internal sealed class AllowAllAuthStateProvider : AuthenticationStateProvider
    {
        private readonly AuthenticationState authenticationState;

        public AllowAllAuthStateProvider(params string[] roles)
        {
            ClaimsIdentity identity = new(
                roles.Select(role => new Claim(ClaimTypes.Role, role)),
                authenticationType: "Test",
                nameType: ClaimTypes.Name,
                roleType: ClaimTypes.Role);
            authenticationState = new AuthenticationState(new ClaimsPrincipal(identity));
        }

        public override Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            return Task.FromResult(authenticationState);
        }
    }

    internal sealed class RecordingSettingsApiConn : SimulatedApiConnection
    {
        public List<string> Queries { get; } = [];
        public List<object> Variables { get; } = [];
        public List<IpProtocol> IpProtocols { get; set; } = [];
        public List<OwnerResponsibleType> OwnerResponsibleTypes { get; set; } = [];
        public List<ModellingNwGroup> ModellingGroups { get; set; } = [];
        public List<PathAnalysisAlgorithm> PathAnalysisAlgorithms { get; set; } = [];
        public ReturnId UpdateServiceResult { get; set; } = new() { UpdatedId = 1 };
        public ReturnIdWrapper AddHistoryResult { get; set; } = new() { ReturnIds = [new ReturnId()] };
        public List<ConfigItem> LastUpsertConfigItems { get; private set; } = [];
        public int UpsertConfigCallCount { get; private set; }
        public int UpdateUserLanguageCallCount { get; private set; }
        public string? LastUpdatedLanguage { get; private set; }

        public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
        {
            Queries.Add(query);
            if (variables != null)
            {
                Variables.Add(variables);
            }

            if (query == StmQueries.getIpProtocols && typeof(QueryResponseType) == typeof(List<IpProtocol>))
            {
                return Task.FromResult((QueryResponseType)(object)IpProtocols);
            }

            if (query == OwnerQueries.getOwnerResponsibleTypes && typeof(QueryResponseType) == typeof(List<OwnerResponsibleType>))
            {
                return Task.FromResult((QueryResponseType)(object)OwnerResponsibleTypes);
            }

            if (query == ModellingQueries.getNwGroupObjects && typeof(QueryResponseType) == typeof(List<ModellingNwGroup>))
            {
                return Task.FromResult((QueryResponseType)(object)ModellingGroups);
            }

            if (query == ModellingQueries.updateService && typeof(QueryResponseType) == typeof(ReturnId))
            {
                return Task.FromResult((QueryResponseType)(object)UpdateServiceResult);
            }

            if (query == ModellingQueries.addHistoryEntry && typeof(QueryResponseType) == typeof(ReturnIdWrapper))
            {
                return Task.FromResult((QueryResponseType)(object)AddHistoryResult);
            }

            if (query == PathAnalysisAlgorithmQueries.getPathAnalysisAlgorithms
                && typeof(QueryResponseType) == typeof(List<PathAnalysisAlgorithm>))
            {
                return Task.FromResult((QueryResponseType)(object)PathAnalysisAlgorithms);
            }

            if (query == ConfigQueries.upsertConfigItems)
            {
                UpsertConfigCallCount++;
                PropertyInfo? configItemsProperty = variables?.GetType().GetProperty("config_items");
                LastUpsertConfigItems = configItemsProperty == null
                    ? []
                    : ((IEnumerable<ConfigItem>)configItemsProperty.GetValue(variables)!).ToList();
                return Task.FromResult(default(QueryResponseType)!);
            }

            if (query == ConfigQueries.getConfigItemsByUser && typeof(QueryResponseType) == typeof(ConfigItem[]))
            {
                return Task.FromResult((QueryResponseType)(object)Array.Empty<ConfigItem>());
            }

            if (query == AuthQueries.updateUserLanguage && typeof(QueryResponseType) == typeof(ReturnId))
            {
                UpdateUserLanguageCallCount++;
                PropertyInfo? languageProperty = variables?.GetType().GetProperty("language");
                LastUpdatedLanguage = languageProperty?.GetValue(variables)?.ToString();
                return Task.FromResult((QueryResponseType)(object)new ReturnId { UpdatedId = 1 });
            }

            if (query == ConfigQueries.getCustomTextsPerLanguage && typeof(QueryResponseType) == typeof(List<UiText>))
            {
                return Task.FromResult((QueryResponseType)(object)new List<UiText>());
            }

            throw new NotImplementedException($"Unhandled query {query} for {typeof(QueryResponseType).Name}");
        }
    }
}
