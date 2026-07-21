using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Data.Flow;
using FWO.Middleware.Server.Controllers;
using FWO.Middleware.Server.Requests;
using FWO.Middleware.Server.Responses;
using FWO.Middleware.Server.Services;
using Microsoft.AspNetCore.Mvc;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class FlowCatalogControllerTest
    {
        [Test]
        public async Task FlowCatalogController_ReturnsMappedResultsForCatalogAndLookupEndpoints()
        {
            RecordingApiConnection apiConnection = new();
            FlowCatalogController controller = new(new FlowCatalogService(apiConnection));

            ActionResult<List<AddressObjectResponse>> addressObjectsResult = await controller.GetAddressObjects(new GetAddressObjectsRequest
            {
                Filter = new VisibleInRequestFilter { VisibleInRequest = true }
            });
            ActionResult<List<AddressGroupResponse>> addressGroupsResult = await controller.GetAddressGroups(new GetAddressGroupsRequest());
            ActionResult<List<ServiceObjectResponse>> serviceObjectsResult = await controller.GetServiceObjects(new GetServiceObjectsRequest());
            ActionResult<List<ServiceGroupResponse>> serviceGroupsResult = await controller.GetServiceGroups(new GetServiceGroupsRequest());
            ActionResult<List<TimeObjectResponse>> timeObjectsResult = await controller.GetTimeObjects(new GetTimeObjectsRequest());
            ActionResult<ServiceObjectIdResponse> serviceObjectIdResult = await controller.GetServiceObjectId(new GetServiceObjectIdRequest
            {
                PortStart = 80,
                PortEnd = 80,
                Protocol = "TCP",
                Filter = new VisibleInRequestFilter { VisibleInRequest = false }
            });
            ActionResult<ServiceObjectIdResponse> numericServiceObjectIdResult = await controller.GetServiceObjectId(new GetServiceObjectIdRequest
            {
                PortStart = 53,
                PortEnd = 53,
                Protocol = "6"
            });
            ActionResult<AddressObjectIdResponse> addressObjectIdResult = await controller.GetAddressObjectId(new GetAddressObjectIdRequest
            {
                IpStart = "10.0.0.1",
                IpEnd = "10.0.0.2"
            });

            Assert.Multiple(() =>
            {
                Assert.That(addressObjectsResult.Result, Is.TypeOf<OkObjectResult>());
                Assert.That(ExtractValue<List<AddressObjectResponse>>(addressObjectsResult), Has.Count.EqualTo(1));
                Assert.That(ExtractValue<List<AddressObjectResponse>>(addressObjectsResult)[0].Name, Is.EqualTo("Host"));
                Assert.That(ExtractValue<List<AddressObjectResponse>>(addressObjectsResult)[0].ShowInRequest, Is.True);

                Assert.That(addressGroupsResult.Result, Is.TypeOf<OkObjectResult>());
                Assert.That(ExtractValue<List<AddressGroupResponse>>(addressGroupsResult)[0].Members[0].Name, Is.EqualTo("Host"));

                Assert.That(serviceObjectsResult.Result, Is.TypeOf<OkObjectResult>());
                Assert.That(ExtractValue<List<ServiceObjectResponse>>(serviceObjectsResult)[0].Protocol, Is.EqualTo("TCP"));

                Assert.That(serviceGroupsResult.Result, Is.TypeOf<OkObjectResult>());
                Assert.That(ExtractValue<List<ServiceGroupResponse>>(serviceGroupsResult)[0].Members[0].Name, Is.EqualTo("Web"));

                Assert.That(timeObjectsResult.Result, Is.TypeOf<OkObjectResult>());
                Assert.That(ExtractValue<List<TimeObjectResponse>>(timeObjectsResult)[0].StartTime, Does.StartWith("2026-07-20T10:00:00"));

                Assert.That(serviceObjectIdResult.Result, Is.TypeOf<OkObjectResult>());
                Assert.That(ExtractValue<ServiceObjectIdResponse>(serviceObjectIdResult).Name, Is.EqualTo("Dns"));

                Assert.That(numericServiceObjectIdResult.Result, Is.TypeOf<OkObjectResult>());
                Assert.That(ExtractValue<ServiceObjectIdResponse>(numericServiceObjectIdResult).Id, Is.EqualTo(22));

                Assert.That(addressObjectIdResult.Result, Is.TypeOf<OkObjectResult>());
                Assert.That(ExtractValue<AddressObjectIdResponse>(addressObjectIdResult).Name, Is.EqualTo("Host"));
            });

            Assert.That(apiConnection.Queries, Does.Contain(FlowQueries.getFlowAddressObjects));
            Assert.That(apiConnection.Queries, Does.Contain(FlowQueries.getFlowServiceObjects));
            Assert.That(apiConnection.Queries, Does.Contain(StmQueries.getIpProtocols));
            Assert.That(apiConnection.Queries, Does.Contain(FlowQueries.getFlowServiceObjectId));
            Assert.That(apiConnection.Queries, Does.Contain(FlowQueries.getFlowAddressObjectId));
        }

        [Test]
        public async Task FlowCatalogController_ReturnsValidationErrorsForInvalidLookupRequests()
        {
            FlowCatalogController controller = new(new FlowCatalogService(new RecordingApiConnection()));

            ActionResult<ServiceObjectIdResponse> missingProtocol = await controller.GetServiceObjectId(new GetServiceObjectIdRequest
            {
                PortStart = 80,
                PortEnd = 80,
                Protocol = string.Empty
            });

            ActionResult<AddressObjectIdResponse> missingIpBounds = await controller.GetAddressObjectId(new GetAddressObjectIdRequest
            {
                IpStart = string.Empty,
                IpEnd = "10.0.0.2"
            });

            Assert.Multiple(() =>
            {
                Assert.That(missingProtocol.Result, Is.TypeOf<BadRequestObjectResult>());
                Assert.That(missingIpBounds.Result, Is.TypeOf<BadRequestObjectResult>());
            });
        }

        private static T ExtractValue<T>(ActionResult<T> result)
        {
            Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
            return (T)((OkObjectResult)result.Result!).Value!;
        }

        private sealed class RecordingApiConnection : ApiConnection
        {
            public List<string> Queries { get; } = [];

            public override void SetAuthHeader(string jwt) { }
            public override void SetRole(string role) { }
            public override void SetBestRole(System.Security.Claims.ClaimsPrincipal user, List<string> targetRoleList) { }
            public override void SwitchBack() { }
            protected override void Dispose(bool disposing) { }
            public override void DisposeSubscriptions<T>() { }
            public override Task ReconnectSubscriptionsAsync(string jwt, CancellationToken ct) => Task.CompletedTask;
            public override GraphQlApiSubscription<SubscriptionResponseType> GetSubscription<SubscriptionResponseType>(Action<Exception> exceptionHandler, GraphQlApiSubscription<SubscriptionResponseType>.SubscriptionUpdate subscriptionUpdateHandler, string subscription, object? variables = null, string? operationName = null)
            {
                throw new NotImplementedException();
            }

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                Queries.Add(query);

                if (typeof(QueryResponseType) == typeof(List<FlowNwObject>) && query == FlowQueries.getFlowAddressObjects)
                {
                    return Task.FromResult((QueryResponseType)(object)new List<FlowNwObject>
                    {
                        new()
                        {
                            Id = 11,
                            Name = "Host",
                            IpStart = "10.0.0.1",
                            IpEnd = "10.0.0.2",
                            ShowInRequestModule = true
                        }
                    });
                }

                if (typeof(QueryResponseType) == typeof(List<FlowNwGroup>) && query == FlowQueries.getFlowAddressGroups)
                {
                    return Task.FromResult((QueryResponseType)(object)new List<FlowNwGroup>
                    {
                        new()
                        {
                            Id = 12,
                            Name = "Address Group",
                            ShowInRequestModule = false,
                            NwGroupMembers =
                            [
                                new FlowNwGroupMember
                                {
                                    NwObjectId = 11,
                                    NwObject = new FlowNwObject { Id = 11, Name = "Host" }
                                }
                            ]
                        }
                    });
                }

                if (typeof(QueryResponseType) == typeof(List<FlowSvcObject>) && query == FlowQueries.getFlowServiceObjects)
                {
                    return Task.FromResult((QueryResponseType)(object)new List<FlowSvcObject>
                    {
                        new()
                        {
                            Id = 21,
                            Name = "Web",
                            PortStart = 80,
                            PortEnd = 80,
                            ProtoId = 6,
                            ShowInRequestModule = true
                        }
                    });
                }

                if (typeof(QueryResponseType) == typeof(List<FlowSvcGroup>) && query == FlowQueries.getFlowServiceGroups)
                {
                    return Task.FromResult((QueryResponseType)(object)new List<FlowSvcGroup>
                    {
                        new()
                        {
                            Id = 22,
                            Name = "Service Group",
                            ShowInRequestModule = false,
                            SvcGroupMembers =
                            [
                                new FlowSvcGroupMember
                                {
                                    SvcObjectId = 21,
                                    SvcObject = new FlowSvcObject { Id = 21, Name = "Web" }
                                }
                            ]
                        }
                    });
                }

                if (typeof(QueryResponseType) == typeof(List<FlowTimeObject>) && query == FlowQueries.getFlowTimeObjects)
                {
                    return Task.FromResult((QueryResponseType)(object)new List<FlowTimeObject>
                    {
                        new()
                        {
                            Id = 31,
                            Name = "Business Hours",
                            StartTime = DateTime.Parse("2026-07-20T08:00:00Z"),
                            EndTime = DateTime.Parse("2026-07-20T18:00:00Z"),
                            ShowInRequestModule = false
                        }
                    });
                }

                if (typeof(QueryResponseType) == typeof(List<IpProtocol>) && query == StmQueries.getIpProtocols)
                {
                    return Task.FromResult((QueryResponseType)(object)new List<IpProtocol>
                    {
                        new() { Id = 6, Name = "TCP" }
                    });
                }

                if (typeof(QueryResponseType) == typeof(List<FlowSvcObject>) && query == FlowQueries.getFlowServiceObjectId)
                {
                    return Task.FromResult((QueryResponseType)(object)new List<FlowSvcObject>
                    {
                        new() { Id = 22, Name = "Dns" }
                    });
                }

                if (typeof(QueryResponseType) == typeof(List<FlowNwObject>) && query == FlowQueries.getFlowAddressObjectId)
                {
                    return Task.FromResult((QueryResponseType)(object)new List<FlowNwObject>
                    {
                        new() { Id = 11, Name = "Host" }
                    });
                }

                throw new NotSupportedException($"Unexpected query {query} for {typeof(QueryResponseType).Name}.");
            }

            public override Task<ApiResponse<QueryResponseType>> SendQuerySafeAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null)
            {
                throw new NotSupportedException($"Unexpected safe query {query} for {typeof(QueryResponseType).Name}.");
            }
        }
    }
}
