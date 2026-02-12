using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data.Workflow;
using FWO.Services;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    public class ExtStateHandlerTest
    {
        private sealed class ExtStateFallbackApiConn : SimulatedApiConnection
        {
            public override async Task<ApiResponse<QueryResponseType>> SendQuerySafeAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null)
            {
                await DefaultInit.DoNothing();
                if (typeof(QueryResponseType) == typeof(List<WfExtState>))
                {
                    if (query == RequestQueries.getExtStates)
                    {
                        return (ApiResponse<QueryResponseType>)(object)new ApiResponse<List<WfExtState>>(
                            "field ext_state not found in type: query_root");
                    }
                    if (query.Contains("request_ext_state", StringComparison.Ordinal))
                    {
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
                if (typeof(QueryResponseType) == typeof(List<WfExtState>))
                {
                    return (ApiResponse<QueryResponseType>)(object)new ApiResponse<List<WfExtState>>(
                        "field ext_state not found in type: query_root");
                }
                throw new NotImplementedException();
            }
        }

        [Test]
        public void InitUsesLegacyQueryWhenDefaultRootMissing()
        {
            ExtStateHandler handler = new(new ExtStateFallbackApiConn());

            Assert.That(handler.GetInternalStateId(ExtStates.ExtReqDone), Is.EqualTo(631));
        }

        [Test]
        public void InitDoesNotThrowWhenNoExtStateRootExists()
        {
            Assert.DoesNotThrow(() => new ExtStateHandler(new ExtStateMissingApiConn()));
        }
    }
}
