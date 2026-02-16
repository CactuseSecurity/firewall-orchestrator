using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Data.Workflow;
using FWO.Services;
using FWO.Services.Workflow;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    public class ExtStateHandlerTest
    {
        private sealed class ExtStateSuccessApiConn : SimulatedApiConnection
        {
            public bool QueryCalled { get; private set; }

            public override async Task<ApiResponse<QueryResponseType>> SendQuerySafeAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null)
            {
                await DefaultInit.DoNothing();
                if (typeof(QueryResponseType) == typeof(List<WfExtState>))
                {
                    if (query == RequestQueries.getExtStates)
                    {
                        QueryCalled = true;
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

        private sealed class ExtStateErrorApiConn : SimulatedApiConnection
        {
            public override async Task<ApiResponse<QueryResponseType>> SendQuerySafeAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null)
            {
                await DefaultInit.DoNothing();
                if (typeof(QueryResponseType) == typeof(List<WfExtState>))
                {
                    return (ApiResponse<QueryResponseType>)(object)new ApiResponse<List<WfExtState>>(
                        "field request_ext_state not found in type: query_root");
                }
                throw new NotImplementedException();
            }
        }

        [Test]
        public void InitUsesRequestExtStateQuery()
        {
            ExtStateSuccessApiConn apiConn = new();
            ExtStateHandler handler = new(apiConn);

            Assert.That(handler.GetInternalStateId(ExtStates.ExtReqDone), Is.EqualTo(631));
            Assert.That(apiConn.QueryCalled, Is.True);
        }

        [Test]
        public void InitThrowsWhenExtStateQueryFails()
        {
            Assert.Throws<AggregateException>(() => new ExtStateHandler(new ExtStateErrorApiConn()));
        }
    }
}
