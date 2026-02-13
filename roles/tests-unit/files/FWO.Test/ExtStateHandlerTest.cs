using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Data.Workflow;
using FWO.Services;
using NUnit.Framework;
using System.Text.Json;

namespace FWO.Test
{
    [TestFixture]
    public class ExtStateHandlerTest
    {
        private sealed class ExtStateFallbackApiConn : SimulatedApiConnection
        {
            public bool DefaultQueryCalled { get; private set; }
            public bool LegacyQueryCalled { get; private set; }

            public override async Task<ApiResponse<QueryResponseType>> SendQuerySafeAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null)
            {
                await DefaultInit.DoNothing();
                if (query.Contains("__type(name: \"query_root\")", StringComparison.Ordinal))
                {
                    QueryResponseType rootInfo = JsonSerializer.Deserialize<QueryResponseType>("{\"Fields\":[{\"Name\":\"request_ext_state\"}]}")
                        ?? throw new InvalidOperationException("Could not deserialize root info.");
                    return new ApiResponse<QueryResponseType>(rootInfo);
                }

                if (typeof(QueryResponseType) == typeof(List<WfExtState>))
                {
                    if (query == RequestQueries.getExtStates)
                    {
                        DefaultQueryCalled = true;
                        return (ApiResponse<QueryResponseType>)(object)new ApiResponse<List<WfExtState>>(
                            "field ext_state not found in type: query_root");
                    }
                    if (query.Contains("request_ext_state", StringComparison.Ordinal))
                    {
                        LegacyQueryCalled = true;
                        List<WfExtState> extStates =
                        [
                            new() { Id = 1, Name = "ExtReqInitialized", StateId = 1 },
                            new() { Id = 2, Name = "ExtReqDone", StateId = 631 }
                        ];
                        return (ApiResponse<QueryResponseType>)(object)new ApiResponse<List<WfExtState>>(extStates);
                    }
                }
                throw new NotImplementedException();
            }
        }

        private sealed class ExtStateMissingApiConn : SimulatedApiConnection
        {
            public override async Task<ApiResponse<QueryResponseType>> SendQuerySafeAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null)
            {
                await DefaultInit.DoNothing();
                if (query.Contains("__type(name: \"query_root\")", StringComparison.Ordinal))
                {
                    return new ApiResponse<QueryResponseType>("introspection disabled");
                }
                if (typeof(QueryResponseType) == typeof(List<WfExtState>))
                {
                    return (ApiResponse<QueryResponseType>)(object)new ApiResponse<List<WfExtState>>(
                        "field ext_state not found in type: query_root");
                }
                throw new NotImplementedException();
            }
        }

        private sealed class ExtStateCurrentRootApiConn : SimulatedApiConnection
        {
            public bool DefaultQueryCalled { get; private set; }
            public bool LegacyQueryCalled { get; private set; }

            public override async Task<ApiResponse<QueryResponseType>> SendQuerySafeAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null)
            {
                await DefaultInit.DoNothing();
                if (query.Contains("__type(name: \"query_root\")", StringComparison.Ordinal))
                {
                    QueryResponseType rootInfo = JsonSerializer.Deserialize<QueryResponseType>("{\"Fields\":[{\"Name\":\"ext_state\"}]}")
                        ?? throw new InvalidOperationException("Could not deserialize root info.");
                    return new ApiResponse<QueryResponseType>(rootInfo);
                }

                if (typeof(QueryResponseType) == typeof(List<WfExtState>))
                {
                    if (query == RequestQueries.getExtStates)
                    {
                        DefaultQueryCalled = true;
                        List<WfExtState> extStates =
                        [
                            new() { Id = 1, Name = "ExtReqInitialized", StateId = 1 },
                            new() { Id = 2, Name = "ExtReqDone", StateId = 632 }
                        ];
                        return (ApiResponse<QueryResponseType>)(object)new ApiResponse<List<WfExtState>>(extStates);
                    }
                    if (query.Contains("request_ext_state", StringComparison.Ordinal))
                    {
                        LegacyQueryCalled = true;
                    }
                }
                throw new NotImplementedException();
            }
        }

        [Test]
        public void InitUsesLegacyQueryWhenDefaultRootMissing()
        {
            ExtStateFallbackApiConn apiConn = new();
            ExtStateHandler handler = new(apiConn);

            Assert.That(handler.GetInternalStateId(ExtStates.ExtReqDone), Is.EqualTo(631));
            Assert.That(apiConn.LegacyQueryCalled, Is.True);
            Assert.That(apiConn.DefaultQueryCalled, Is.False);
        }

        [Test]
        public void InitDoesNotThrowWhenNoExtStateRootExists()
        {
            Assert.DoesNotThrow(() => new ExtStateHandler(new ExtStateMissingApiConn()));
        }

        [Test]
        public void InitUsesCurrentRootWhenDetected()
        {
            ExtStateCurrentRootApiConn apiConn = new();
            ExtStateHandler handler = new(apiConn);

            Assert.That(handler.GetInternalStateId(ExtStates.ExtReqDone), Is.EqualTo(632));
            Assert.That(apiConn.DefaultQueryCalled, Is.True);
            Assert.That(apiConn.LegacyQueryCalled, Is.False);
        }
    }
}
