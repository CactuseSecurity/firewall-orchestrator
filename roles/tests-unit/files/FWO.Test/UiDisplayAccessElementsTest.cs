using AngleSharp.Dom;
using Bunit;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Flow;
using FWO.Data.Workflow;
using FWO.Services;
using FWO.Services.EventMediator;
using FWO.Ui.Pages.Request;
using FWO.Ui.Shared;
using FWO.Ui.Services;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using static FWO.Test.UiRequestWorkflowTest;

namespace FWO.Test
{
    [TestFixture]
    internal class UiDisplayAccessElementsTest
    {
        private static readonly string[] kExpectedFlowObjectNames = ["Flow Source"];
        private static readonly string[] kExpectedFlowServiceNames = ["Flow Service"];

        [Test]
        public async Task DisplayAccessElements_ReadOnlyObjectEntriesPreferGroupName()
        {
            await using BunitContext context = new();
            context.Services.AddSingleton<ApiConnection>(new UiRequestWorkflowTest.RequestWorkflowApiConn());
            context.Services.AddSingleton<UserConfig>(new UiRequestWorkflowTest.RequestWorkflowUserConfig());
            List<NwObjectElement> sources =
            [
                new() { NetworkId = 42, GroupName = "AR-Displayed", Name = "HiddenSourceName" }
            ];
            List<NwObjectElement> destinations =
            [
                new() { NetworkId = 43, GroupName = "AR-Destination", Name = "HiddenDestinationName" }
            ];
            List<NwServiceElement> services =
            [
                new() { ServiceId = 44, GroupName = "SG-Displayed", Name = "HiddenServiceName" }
            ];

            IRenderedComponent<DisplayAccessElements> component = context.Render<DisplayAccessElements>(parameters => parameters
                .Add(p => p.Sources, sources)
                .Add(p => p.Destinations, destinations)
                .Add(p => p.Services, services)
                .Add(p => p.IpProtos, new List<IpProtocol>())
                .Add(p => p.EditMode, false));

            Assert.Multiple(() =>
            {
                Assert.That(component.Markup, Does.Contain("AR-Displayed"));
                Assert.That(component.Markup, Does.Contain("AR-Destination"));
                Assert.That(component.Markup, Does.Contain("SG-Displayed"));
                Assert.That(component.Markup, Does.Not.Contain("HiddenSourceName"));
                Assert.That(component.Markup, Does.Not.Contain("HiddenDestinationName"));
                Assert.That(component.Markup, Does.Not.Contain("HiddenServiceName"));
                Assert.That(component.Markup, Does.Contain("manually"));
            });
        }

        [Test]
        public async Task DisplayAccessElements_ReadOnlyFlowObjectShowsDetailsFromFlowDb()
        {
            await using BunitContext context = new();
            context.Services.AddSingleton<ApiConnection>(new UiRequestWorkflowTest.RequestWorkflowApiConn
            {
                FlowNwObjects =
                [
                    new FlowNwObject
                    {
                        Id = 101,
                        Name = "Flow Source",
                        IpStart = "10.0.0.1/32",
                        IpEnd = "10.0.0.8/32",
                        State = FlowState.Implemented,
                        ShowInRequestModule = true
                    }
                ]
            });
            context.Services.AddSingleton<UserConfig>(new UiRequestWorkflowTest.RequestWorkflowUserConfig { ReqUseFlowDb = true });

            IRenderedComponent<DisplayAccessElements> component = context.Render<DisplayAccessElements>(parameters => parameters
                .Add(p => p.Sources, new List<NwObjectElement> { new() { FlowNetworkObjectId = 101, Name = "Flow Source" } })
                .Add(p => p.Destinations, new List<NwObjectElement>())
                .Add(p => p.Services, new List<NwServiceElement>())
                .Add(p => p.IpProtos, new List<IpProtocol>())
                .Add(p => p.EditMode, false));

            component.FindAll("button")[0].Click();

            Assert.Multiple(() =>
            {
                Assert.That(component.Markup, Does.Contain("10.0.0.1-10.0.0.8"));
                Assert.That(component.Markup, Does.Contain("from Flow DB"));
                Assert.That(component.Markup, Does.Not.Contain(FlowState.Implemented));
            });
        }

        [Test]
        public async Task DisplayAccessElements_ReadOnlyElementFallsBackToTaskData()
        {
            await using BunitContext context = new();
            context.Services.AddSingleton<ApiConnection>(new UiRequestWorkflowTest.RequestWorkflowApiConn());
            context.Services.AddSingleton<UserConfig>(new UiRequestWorkflowTest.RequestWorkflowUserConfig());
            WfReqTask fallbackTask = new()
            {
                Id = 77,
                Elements = new List<WfReqElement>
                {
                    new()
                    {
                        TaskId = 77,
                        Field = ElemFieldType.source.ToString(),
                        Name = "Task Source",
                        Cidr = new Cidr("192.0.2.1/32")
                    },
                    new()
                    {
                        TaskId = 77,
                        Field = ElemFieldType.destination.ToString(),
                        Name = "Task Destination",
                        Cidr = new Cidr("192.0.2.2/32")
                    }
                }
            };

            IRenderedComponent<DisplayAccessElements> component = context.Render<DisplayAccessElements>(parameters => parameters
                .Add(p => p.Sources, new List<NwObjectElement> { new() { Name = "Task Source", IpString = "192.0.2.1/32" } })
                .Add(p => p.Destinations, new List<NwObjectElement> { new() { Name = "Task Destination", IpString = "192.0.2.2/32" } })
                .Add(p => p.Services, new List<NwServiceElement>())
                .Add(p => p.TicketTasks, new List<WfReqTask> { fallbackTask })
                .Add(p => p.IpProtos, new List<IpProtocol>())
                .Add(p => p.EditMode, false));

            component.FindAll("button")[0].Click();

            Assert.Multiple(() =>
            {
                Assert.That(component.Markup, Does.Contain("192.0.2.1"));
                Assert.That(component.Markup, Does.Contain("From Ticket"));
                Assert.That(component.FindComponent<DisplayFlowElementDetails>().Instance.NetworkFallback, Has.Count.EqualTo(1));
            });
        }

        [Test]
        public async Task DisplayAccessElements_ReadOnlyFlowGroupShowsMembers()
        {
            await using BunitContext context = new();
            context.Services.AddSingleton<ApiConnection>(new UiRequestWorkflowTest.RequestWorkflowApiConn
            {
                FlowNwGroups =
                [
                    new FlowNwGroup
                    {
                        Id = 501,
                        Name = "Flow Group",
                        NwGroupMembers =
                        [
                            new FlowNwGroupMember
                            {
                                NwObject = new FlowNwObject { Id = 101, Name = "Group Member", IpStart = "198.51.100.1/32" }
                            }
                        ]
                    }
                ]
            });
            context.Services.AddSingleton<UserConfig>(new UiRequestWorkflowTest.RequestWorkflowUserConfig { ReqUseFlowDb = true });

            IRenderedComponent<DisplayAccessElements> component = context.Render<DisplayAccessElements>(parameters => parameters
                .Add(p => p.Sources, new List<NwObjectElement> { new() { FlowNetworkGroupId = 501, GroupName = "Flow Group" } })
                .Add(p => p.Destinations, new List<NwObjectElement>())
                .Add(p => p.Services, new List<NwServiceElement>())
                .Add(p => p.IpProtos, new List<IpProtocol>())
                .Add(p => p.EditMode, false));

            component.FindAll("button").Single().Click();

            Assert.That(component.Markup, Does.Contain("Group Member"));
        }

        [Test]
        public async Task DisplayAccessElements_ReadOnlyLocalGroupUsesGroupDefinitionTaskMembers()
        {
            await using BunitContext context = new();
            context.Services.AddSingleton<ApiConnection>(new UiRequestWorkflowTest.RequestWorkflowApiConn());
            context.Services.AddSingleton<UserConfig>(new UiRequestWorkflowTest.RequestWorkflowUserConfig());
            WfReqTask groupTask = new()
            {
                TaskType = WfTaskType.group_create.ToString(),
                AdditionalInfo = JsonSerializer.Serialize(new Dictionary<string, string>
                {
                    [AdditionalInfoKeys.GrpName] = "Local Group"
                }),
                Elements =
                [
                    new() { Field = ElemFieldType.source.ToString(), IpString = "198.51.100.1/32" },
                    new() { Field = ElemFieldType.source.ToString(), IpString = "198.51.100.2/32" }
                ]
            };

            IRenderedComponent<DisplayAccessElements> component = context.Render<DisplayAccessElements>(parameters => parameters
                .Add(p => p.Sources, new List<NwObjectElement> { new() { GroupName = "Local Group" } })
                .Add(p => p.Destinations, new List<NwObjectElement>())
                .Add(p => p.Services, new List<NwServiceElement>())
                .Add(p => p.TicketTasks, new List<WfReqTask> { groupTask })
                .Add(p => p.IpProtos, new List<IpProtocol>())
                .Add(p => p.EditMode, false));

            component.FindAll("button").Single().Click();

            Assert.Multiple(() =>
            {
                Assert.That(component.Markup, Does.Contain("198.51.100.1"));
                Assert.That(component.Markup, Does.Contain("198.51.100.2"));
                Assert.That(component.Markup, Does.Contain("From Ticket"));
            });
        }

        [Test]
        public async Task DisplayAccessElements_ReadOnlyFlowServiceGroupUsesCatalogMemberDetails()
        {
            await using BunitContext context = new();
            context.Services.AddSingleton<ApiConnection>(new UiRequestWorkflowTest.RequestWorkflowApiConn
            {
                FlowSvcObjects =
                [
                    new FlowSvcObject { Id = 201, Name = "Correct service", PortStart = 443, PortEnd = 443, ProtoId = 6, ShowInRequestModule = true }
                ],
                FlowSvcGroups =
                [
                    new FlowSvcGroup
                    {
                        Id = 601,
                        Name = "Flow service group",
                        SvcGroupMembers =
                        [
                            new FlowSvcGroupMember
                            {
                                SvcObjectId = 201,
                                SvcObject = new FlowSvcObject { Id = 201, Name = "Stale nested service", PortStart = 1, ProtoId = 17 }
                            }
                        ]
                    }
                ]
            });
            context.Services.AddSingleton<UserConfig>(new UiRequestWorkflowTest.RequestWorkflowUserConfig { ReqUseFlowDb = true });

            IRenderedComponent<DisplayAccessElements> component = context.Render<DisplayAccessElements>(parameters => parameters
                .Add(p => p.Sources, new List<NwObjectElement>())
                .Add(p => p.Destinations, new List<NwObjectElement>())
                .Add(p => p.Services, new List<NwServiceElement>
                {
                    new() { FlowServiceGroupId = 601, GroupName = "Flow service group" }
                })
                .Add(p => p.IpProtos, new List<IpProtocol> { new() { Id = 6, Name = "tcp" } })
                .Add(p => p.EditMode, false));

            component.FindAll("button").Single().Click();

            Assert.Multiple(() =>
            {
                Assert.That(component.Markup, Does.Contain("Flow service group"));
                Assert.That(component.Markup, Does.Contain("Correct service"));
                Assert.That(component.Markup, Does.Contain("443/tcp"));
                Assert.That(component.Markup, Does.Not.Contain("Stale nested service"));
            });

            component.FindAll("button")[^1].Click();
            Assert.That(component.Markup, Does.Not.Contain("modal-backdrop"));
        }

        [Test]
        public async Task DisplayAccessElements_ReadOnlyLocalServiceGroupUsesParallelGroupTaskMembers()
        {
            await using BunitContext context = new();
            context.Services.AddSingleton<ApiConnection>(new UiRequestWorkflowTest.RequestWorkflowApiConn());
            context.Services.AddSingleton<UserConfig>(new UiRequestWorkflowTest.RequestWorkflowUserConfig());
            WfReqTask groupTask = new()
            {
                TaskType = WfTaskType.group_create.ToString(),
                AdditionalInfo = JsonSerializer.Serialize(new Dictionary<string, string>
                {
                    [AdditionalInfoKeys.GrpName] = "Local service group"
                }),
                Elements =
                [
                    new()
                    {
                        Field = ElemFieldType.service.ToString(),
                        Name = "Local service member",
                        Port = 8443,
                        ProtoId = 6,
                        GroupName = "Local service group"
                    }
                ]
            };

            IRenderedComponent<DisplayAccessElements> component = context.Render<DisplayAccessElements>(parameters => parameters
                .Add(p => p.Sources, new List<NwObjectElement>())
                .Add(p => p.Destinations, new List<NwObjectElement>())
                .Add(p => p.Services, new List<NwServiceElement>
                {
                    new() { GroupName = "Local service group", Name = "Local service group" }
                })
                .Add(p => p.TicketTasks, new List<WfReqTask> { groupTask })
                .Add(p => p.IpProtos, new List<IpProtocol> { new() { Id = 6, Name = "tcp" } })
                .Add(p => p.EditMode, false));

            component.FindAll("button").Single().Click();

            Assert.Multiple(() =>
            {
                Assert.That(component.Markup, Does.Contain("Local service member"));
                Assert.That(component.Markup, Does.Contain("8443/tcp"));
                Assert.That(component.Markup, Does.Contain("From Ticket"));
            });
        }

        [Test]
        public async Task DisplayAccessElements_LoadsFlowObjectsForSearch_WhenFlowDbEnabled()
        {
            await using BunitContext context = new();
            UiRequestWorkflowTest.RequestWorkflowApiConn apiConn = new()
            {
                FlowNwObjects =
                [
                    new FlowNwObject { Id = 101, Name = "Flow Source", ShowInRequestModule = true },
                    new FlowNwObject { Id = 102, Name = "Hidden Source", ShowInRequestModule = false }
                ],
                FlowSvcObjects =
                [
                    new FlowSvcObject { Id = 201, Name = "Flow Service", ProtoId = 6, ShowInRequestModule = true },
                    new FlowSvcObject { Id = 202, Name = "Removed Service", ProtoId = 6, ShowInRequestModule = true, RemovedDate = DateTime.UtcNow }
                ]
            };
            context.Services.AddSingleton<ApiConnection>(apiConn);
            context.Services.AddSingleton<UserConfig>(new UiRequestWorkflowTest.RequestWorkflowUserConfig { ReqUseFlowDb = true });
            context.Services.AddSingleton<DomEventService>();

            IRenderedComponent<DisplayAccessElements> component = context.Render<DisplayAccessElements>(parameters => parameters
                .Add(p => p.Sources, new List<NwObjectElement>())
                .Add(p => p.Destinations, new List<NwObjectElement>())
                .Add(p => p.Services, new List<NwServiceElement>())
                .Add(p => p.IpProtos, new List<IpProtocol>())
                .Add(p => p.EditMode, true));

            List<NetworkObject> loadedObjects = GetMember<List<NetworkObject>>(component.Instance, "nwObjects");
            List<NetworkService> loadedServices = GetMember<List<NetworkService>>(component.Instance, "nwServices");
            IReadOnlyList<IRenderedComponent<Dropdown<NetworkObject>>> networkDropdowns = component.FindComponents<Dropdown<NetworkObject>>();
            IReadOnlyList<IRenderedComponent<Dropdown<NetworkService>>> serviceDropdowns = component.FindComponents<Dropdown<NetworkService>>();

            Assert.Multiple(() =>
            {
                Assert.That(apiConn.Queries, Does.Contain(FlowQueries.getFlowRequestNwObjectCatalog));
                Assert.That(apiConn.Queries, Does.Contain(FlowQueries.getFlowRequestSvcObjectCatalog));
                Assert.That(loadedObjects.Select(obj => obj.Name), Is.EqualTo(kExpectedFlowObjectNames));
                Assert.That(loadedObjects.Single().FlowNetworkObjectId, Is.EqualTo(101));
                Assert.That(loadedServices.Select(svc => svc.Name), Is.EqualTo(kExpectedFlowServiceNames));
                Assert.That(loadedServices.Single().FlowServiceObjectId, Is.EqualTo(201));
                Assert.That(networkDropdowns, Has.Count.EqualTo(2));
                Assert.That(networkDropdowns.All(dropdown => dropdown.Instance.Nullable), Is.True);
                Assert.That(serviceDropdowns.Single().Instance.Nullable, Is.True);
            });
        }

        [Test]
        public async Task DisplayAccessElements_UsesPortAndProtocolNameForFlowServiceFallbackName()
        {
            await using BunitContext context = new();
            UiRequestWorkflowTest.RequestWorkflowApiConn apiConn = new()
            {
                FlowSvcObjects =
                [
                    new FlowSvcObject { Id = 201, Name = "", PortStart = 443, PortEnd = 8443, ProtoId = 6, ShowInRequestModule = true }
                ]
            };
            context.Services.AddSingleton<ApiConnection>(apiConn);
            context.Services.AddSingleton<UserConfig>(new UiRequestWorkflowTest.RequestWorkflowUserConfig { ReqUseFlowDb = true });
            context.Services.AddSingleton<DomEventService>();

            IRenderedComponent<DisplayAccessElements> component = context.Render<DisplayAccessElements>(parameters => parameters
                .Add(p => p.Sources, new List<NwObjectElement>())
                .Add(p => p.Destinations, new List<NwObjectElement>())
                .Add(p => p.Services, new List<NwServiceElement>())
                .Add(p => p.IpProtos, new List<IpProtocol> { new() { Id = 6, Name = "tcp" } })
                .Add(p => p.EditMode, true));

            List<NetworkService> loadedServices = GetMember<List<NetworkService>>(component.Instance, "nwServices");

            Assert.That(loadedServices.Single().Name, Is.EqualTo("443-8443/tcp"));
        }

        [Test]
        public async Task DisplayAccessElements_UsesProtocolNameForPortlessFlowServiceFallbackName()
        {
            await using BunitContext context = new();
            UiRequestWorkflowTest.RequestWorkflowApiConn apiConn = new()
            {
                FlowSvcObjects = new List<FlowSvcObject>
                {
                    new FlowSvcObject { Id = 202, Name = "", PortStart = null, PortEnd = null, ProtoId = 6, ShowInRequestModule = true }
                }
            };
            context.Services.AddSingleton<ApiConnection>(apiConn);
            context.Services.AddSingleton<UserConfig>(new UiRequestWorkflowTest.RequestWorkflowUserConfig { ReqUseFlowDb = true });
            context.Services.AddSingleton<DomEventService>();

            IRenderedComponent<DisplayAccessElements> component = context.Render<DisplayAccessElements>(parameters => parameters
                .Add(p => p.Sources, new List<NwObjectElement>())
                .Add(p => p.Destinations, new List<NwObjectElement>())
                .Add(p => p.Services, new List<NwServiceElement>())
                .Add(p => p.IpProtos, new List<IpProtocol> { new() { Id = 6, Name = "tcp" } })
                .Add(p => p.EditMode, true));

            List<NetworkService> loadedServices = GetMember<List<NetworkService>>(component.Instance, "nwServices");

            Assert.That(loadedServices.Single().Name, Is.EqualTo("/tcp"));
        }

        private static readonly List<long> kOnlyRequestableServiceId = [203];

        /// <summary>
        /// The canonical any-IP-protocol service is an internal representation the platform attaches
        /// itself; offering it in the request module would let a requester ask for any protocol by
        /// picking a catalog entry (SEC-09).
        /// </summary>
        [Test]
        public async Task DisplayAccessElements_OmitsInternalAnyProtocolFlowServiceFromCatalog()
        {
            await using BunitContext context = new();
            UiRequestWorkflowTest.RequestWorkflowApiConn apiConn = new()
            {
                FlowSvcObjects = new List<FlowSvcObject>
                {
                    new FlowSvcObject { Id = 202, Name = "", PortStart = null, PortEnd = null, ProtoId = -1, ShowInRequestModule = true },
                    new FlowSvcObject { Id = 203, Name = "https", PortStart = 443, PortEnd = 443, ProtoId = 6, ShowInRequestModule = true }
                }
            };
            context.Services.AddSingleton<ApiConnection>(apiConn);
            context.Services.AddSingleton<UserConfig>(new UiRequestWorkflowTest.RequestWorkflowUserConfig { ReqUseFlowDb = true });
            context.Services.AddSingleton<DomEventService>();

            IRenderedComponent<DisplayAccessElements> component = context.Render<DisplayAccessElements>(parameters => parameters
                .Add(p => p.Sources, new List<NwObjectElement>())
                .Add(p => p.Destinations, new List<NwObjectElement>())
                .Add(p => p.Services, new List<NwServiceElement>())
                .Add(p => p.IpProtos, new List<IpProtocol> { new() { Id = -1, Name = "ANY" }, new() { Id = 6, Name = "tcp" } })
                .Add(p => p.EditMode, true));

            List<NetworkService> loadedServices = GetMember<List<NetworkService>>(component.Instance, "nwServices");

            Assert.That(loadedServices.Select(service => service.Id), Is.EquivalentTo(kOnlyRequestableServiceId));
        }

        [Test]
        public async Task DisplayAccessElements_ManualServiceSelectorUsesSelectableProtocols()
        {
            await using BunitContext context = new();
            context.JSInterop.SetupVoid("initializeEventHandlers", _ => true).SetVoidResult();
            context.Services.AddSingleton<ApiConnection>(new UiRequestWorkflowTest.RequestWorkflowApiConn());
            context.Services.AddSingleton<UserConfig>(new UiRequestWorkflowTest.RequestWorkflowUserConfig());
            context.Services.AddSingleton<DomEventService>();
            List<IpProtocol> displayProtocols = new()
            {
                new() { Id = -1, Name = "ANY" },
                new() { Id = 6, Name = "TCP" }
            };
            List<IpProtocol> selectableProtocols = new()
            {
                new() { Id = 6, Name = "TCP" }
            };

            IRenderedComponent<DisplayAccessElements> component = context.Render<DisplayAccessElements>(parameters => parameters
                .Add(p => p.Sources, new List<NwObjectElement>())
                .Add(p => p.Destinations, new List<NwObjectElement>())
                .Add(p => p.Services, new List<NwServiceElement>())
                .Add(p => p.IpProtos, displayProtocols)
                .Add(p => p.SelectableIpProtos, selectableProtocols)
                .Add(p => p.EditMode, true));

            IRenderedComponent<ServiceSelector> serviceSelector = component.FindComponent<ServiceSelector>();

            Assert.That(serviceSelector.Instance.IpProtos.Select(protocol => protocol.Id), Is.EqualTo(new List<int> { 6 }));
        }

        [Test]
        public async Task DisplayAccessElements_SelectedFlowObjectsAreAddedWithFlowIds()
        {
            await using BunitContext context = new();
            context.Services.AddSingleton<ApiConnection>(new UiRequestWorkflowTest.RequestWorkflowApiConn
            {
                FlowNwObjects =
                [
                    new FlowNwObject { Id = 101, Name = "Flow Source", IpStart = "10.0.0.1/32", ShowInRequestModule = true },
                    new FlowNwObject { Id = 102, Name = "Flow Destination", IpStart = "10.0.0.2/32", ShowInRequestModule = true }
                ],
                FlowSvcObjects = [new FlowSvcObject { Id = 201, Name = "Flow Service", PortStart = 443, ProtoId = 6, ShowInRequestModule = true }]
            });
            context.Services.AddSingleton<UserConfig>(new UiRequestWorkflowTest.RequestWorkflowUserConfig { ReqUseFlowDb = true });
            context.Services.AddSingleton<DomEventService>();
            List<NwObjectElement> sources = [];
            List<NwObjectElement> sourcesToAdd = [];
            List<NwObjectElement> destinationsToAdd = [];
            List<NwServiceElement> services = [];
            List<NwServiceElement> servicesToAdd = [];
            IRenderedComponent<DisplayAccessElements> component = context.Render<DisplayAccessElements>(parameters => parameters
                .Add(p => p.Sources, sources)
                .Add(p => p.SourcesToAdd, sourcesToAdd)
                .Add(p => p.Destinations, new List<NwObjectElement>())
                .Add(p => p.DestinationsToAdd, destinationsToAdd)
                .Add(p => p.Services, services)
                .Add(p => p.ServicesToAdd, servicesToAdd)
                .Add(p => p.IpProtos, new List<IpProtocol>())
                .Add(p => p.EditMode, true));

            NetworkObject flowObject = GetMember<List<NetworkObject>>(component.Instance, "nwObjects").Single(obj => obj.FlowNetworkObjectId == 101);
            NetworkObject flowDestination = GetMember<List<NetworkObject>>(component.Instance, "nwObjects").Single(obj => obj.FlowNetworkObjectId == 102);
            NetworkService flowService = GetMember<List<NetworkService>>(component.Instance, "nwServices").Single();
            await component.InvokeAsync(() => SetMember(component.Instance, "newSourceNetwork", flowObject));
            await component.InvokeAsync(() => SetMember(component.Instance, "newDestinationNetwork", flowDestination));
            await component.InvokeAsync(() => SetMember(component.Instance, "newService", flowService));
            IReadOnlyList<IRenderedComponent<IpSelector>> ipSelectors = component.FindComponents<IpSelector>();
            IRenderedComponent<ServiceSelector> serviceSelector = component.FindComponent<ServiceSelector>();

            Assert.Multiple(() =>
            {
                Assert.That(sources, Is.Empty);
                Assert.That(sourcesToAdd.Single().NetworkId, Is.Null);
                Assert.That(sourcesToAdd.Single().FlowNetworkObjectId, Is.EqualTo(101));
                Assert.That(sourcesToAdd.Single().IpString, Is.EqualTo("10.0.0.1/32"));
                Assert.That(destinationsToAdd.Single().FlowNetworkObjectId, Is.EqualTo(102));
                Assert.That(destinationsToAdd.Single().IpString, Is.EqualTo("10.0.0.2/32"));
                Assert.That(services, Is.Empty);
                Assert.That(servicesToAdd.Single().ServiceId, Is.Null);
                Assert.That(servicesToAdd.Single().FlowServiceObjectId, Is.EqualTo(201));
                Assert.That(servicesToAdd.Single().Port, Is.EqualTo(443));
                Assert.That(servicesToAdd.Single().ProtoId, Is.EqualTo(6));
                Assert.That(ipSelectors, Has.Count.EqualTo(2));
                Assert.That(ipSelectors.SelectMany(selector => selector.Instance.IpAddresses).Any(HasNetworkFlowReference), Is.False);
                Assert.That(serviceSelector.Instance.Services.Any(HasServiceFlowReference), Is.False);
                Assert.That(component.Markup, Does.Contain("Flow Source"));
                Assert.That(component.Markup, Does.Contain("Flow Service"));
            });
        }

        [Test]
        public async Task DisplayAccessElements_CatalogOnlySelectionsAreDisplayedInEditList()
        {
            await using BunitContext context = new();
            context.Services.AddSingleton<ApiConnection>(new UiRequestWorkflowTest.RequestWorkflowApiConn
            {
                FlowNwObjects = [new FlowNwObject { Id = 101, Name = "Flow Source", IpStart = "10.0.0.1/32", ShowInRequestModule = true }],
                FlowSvcObjects = [new FlowSvcObject { Id = 201, Name = "Flow Service", PortStart = 443, ProtoId = 6, ShowInRequestModule = true }]
            });
            context.Services.AddSingleton<UserConfig>(new UiRequestWorkflowTest.RequestWorkflowUserConfig
            {
                ReqUseFlowDb = true,
                ReqFlowIntegration = new FlowIntegrationConfig
                {
                    SelectObjects = FlowIntegrationObjectSelectionOptions.FromFlowDb,
                    SelectServices = FlowIntegrationObjectSelectionOptions.FromFlowDb,
                    SelectTimeObjects = FlowIntegrationObjectSelectionOptions.Both,
                    TimeObjectPrecision = FlowIntegrationTimePrecisionOptions.Seconds
                }.ToConfigValue()
            });
            context.Services.AddSingleton<DomEventService>();
            List<NwObjectElement> sourcesToAdd = [];
            List<NwServiceElement> servicesToAdd = [];

            IRenderedComponent<DisplayAccessElements> component = context.Render<DisplayAccessElements>(parameters => parameters
                .Add(p => p.Sources, new List<NwObjectElement>())
                .Add(p => p.SourcesToAdd, sourcesToAdd)
                .Add(p => p.Destinations, new List<NwObjectElement>())
                .Add(p => p.Services, new List<NwServiceElement>())
                .Add(p => p.ServicesToAdd, servicesToAdd)
                .Add(p => p.IpProtos, new List<IpProtocol>())
                .Add(p => p.EditMode, true));

            NetworkObject flowObject = GetMember<List<NetworkObject>>(component.Instance, "nwObjects").Single();
            NetworkService flowService = GetMember<List<NetworkService>>(component.Instance, "nwServices").Single();
            await component.InvokeAsync(() => SetMember(component.Instance, "newSourceNetwork", flowObject));
            await component.InvokeAsync(() => SetMember(component.Instance, "newService", flowService));

            Assert.Multiple(() =>
            {
                Assert.That(component.FindComponents<IpSelector>(), Is.Empty);
                Assert.That(component.FindComponents<ServiceSelector>(), Is.Empty);
                Assert.That(sourcesToAdd.Single().Name, Is.EqualTo("Flow Source"));
                Assert.That(servicesToAdd.Single().Name, Is.EqualTo("Flow Service"));
                Assert.That(component.Markup, Does.Contain("Flow Source"));
                Assert.That(component.Markup, Does.Contain("Flow Service"));
            });
        }

        [Test]
        public async Task DisplayAccessElements_ServiceCatalogStaysInServiceColumn_WhenObjectCatalogDisabled()
        {
            await using BunitContext context = new();
            context.Services.AddSingleton<ApiConnection>(new UiRequestWorkflowTest.RequestWorkflowApiConn
            {
                FlowSvcObjects = [new FlowSvcObject { Id = 201, Name = "Flow Service", PortStart = 443, ProtoId = 6, ShowInRequestModule = true }]
            });
            context.Services.AddSingleton<UserConfig>(new UiRequestWorkflowTest.RequestWorkflowUserConfig
            {
                ReqUseFlowDb = true,
                ReqFlowIntegration = new FlowIntegrationConfig
                {
                    SelectObjects = FlowIntegrationObjectSelectionOptions.Manually,
                    SelectServices = FlowIntegrationObjectSelectionOptions.FromFlowDb,
                    SelectTimeObjects = FlowIntegrationObjectSelectionOptions.Both,
                    TimeObjectPrecision = FlowIntegrationTimePrecisionOptions.Seconds
                }.ToConfigValue()
            });
            context.Services.AddSingleton<DomEventService>();

            IRenderedComponent<DisplayAccessElements> component = context.Render<DisplayAccessElements>(parameters => parameters
                .Add(p => p.Sources, new List<NwObjectElement>())
                .Add(p => p.Destinations, new List<NwObjectElement>())
                .Add(p => p.Services, new List<NwServiceElement>())
                .Add(p => p.IpProtos, new List<IpProtocol>())
                .Add(p => p.EditMode, true));

            IReadOnlyList<IElement> catalogColumns = component.FindAll(".bg-secondary > .form-group.row.col-sm-12 > .col-sm-4");

            Assert.Multiple(() =>
            {
                Assert.That(component.FindComponents<Dropdown<NetworkObject>>(), Is.Empty);
                Assert.That(component.FindComponents<Dropdown<NetworkService>>(), Has.Count.EqualTo(1));
                Assert.That(catalogColumns, Has.Count.EqualTo(3));
                Assert.That(catalogColumns[0].TextContent, Does.Not.Contain("service_catalog"));
                Assert.That(catalogColumns[1].TextContent, Does.Not.Contain("service_catalog"));
                Assert.That(catalogColumns[2].TextContent, Does.Contain("service_catalog"));
            });
        }

        [Test]
        public async Task IpSelector_DisplaysObjectReferenceNamesInMixedList()
        {
            await using BunitContext context = new();
            context.Services.AddSingleton<UserConfig>(new UiRequestWorkflowTest.RequestWorkflowUserConfig());
            List<NwObjectElement> ipAddresses =
            [
                new NwObjectElement("10.0.0.1", 1),
                new() { Name = "Flow Source", FlowNetworkObjectId = 101, TaskId = 1 },
                new() { Name = "Classic Source", NetworkId = 201, TaskId = 1 }
            ];

            IRenderedComponent<IpSelector> component = context.Render<IpSelector>(parameters => parameters
                .Add(p => p.IpAddresses, ipAddresses)
                .Add(p => p.WithLabel, false));

            Assert.Multiple(() =>
            {
                Assert.That(component.Markup, Does.Contain("10.0.0.1"));
                Assert.That(component.Markup, Does.Contain("Flow Source"));
                Assert.That(component.Markup, Does.Contain("Classic Source"));
            });
        }

        [Test]
        public async Task ServiceSelector_DisplaysServiceReferenceNamesInMixedList()
        {
            await using BunitContext context = new();
            context.Services.AddSingleton<UserConfig>(new UiRequestWorkflowTest.RequestWorkflowUserConfig());
            context.Services.AddSingleton<DomEventService>();
            List<NwServiceElement> services =
            [
                new() { Port = 443, ProtoId = 6, TaskId = 1 },
                new() { Name = "Flow Service", FlowServiceObjectId = 201, TaskId = 1 },
                new() { Name = "Classic Service", ServiceId = 301, TaskId = 1 }
            ];
            List<IpProtocol> ipProtos = [new() { Id = 6, Name = "tcp" }];

            IRenderedComponent<ServiceSelector> component = context.Render<ServiceSelector>(parameters => parameters
                .Add(p => p.Services, services)
                .Add(p => p.IpProtos, ipProtos)
                .Add(p => p.WithLabel, false));

            Assert.Multiple(() =>
            {
                Assert.That(component.Markup, Does.Contain("443"));
                Assert.That(component.Markup, Does.Contain("Flow Service"));
                Assert.That(component.Markup, Does.Contain("Classic Service"));
            });
        }

        [Test]
        public async Task ServiceSelector_RequiresPortForProtocolsWithPorts()
        {
            await using BunitContext context = new();
            context.Services.AddSingleton<UserConfig>(new UiRequestWorkflowTest.RequestWorkflowUserConfig());
            context.Services.AddSingleton<DomEventService>();
            List<NwServiceElement> servicesToAdd = [];
            List<IpProtocol> ipProtos =
            [
                new() { Id = 6, Name = "tcp" },
                new() { Id = 50, Name = "esp" }
            ];

            IRenderedComponent<ServiceSelector> component = context.Render<ServiceSelector>(parameters => parameters
                .Add(p => p.Services, new List<NwServiceElement>())
                .Add(p => p.ServicesToAdd, servicesToAdd)
                .Add(p => p.IpProtos, ipProtos)
                .Add(p => p.WithLabel, false));

            await component.InvokeAsync(() => SetMember(component.Instance, "actPort", null));
            await component.InvokeAsync(() => SetMember(component.Instance, "actPortEnd", null));
            component.Find("button.btn-success").Click();

            Assert.That(servicesToAdd, Is.Empty);
        }

        [Test]
        public async Task ServiceSelector_AllowsAddingProtocolWithoutPortsAndAfterRemoval()
        {
            await using BunitContext context = new();
            context.Services.AddSingleton<UserConfig>(new UiRequestWorkflowTest.RequestWorkflowUserConfig());
            context.Services.AddSingleton<DomEventService>();
            List<NwServiceElement> servicesToAdd = [];
            List<IpProtocol> ipProtos =
            [
                new() { Id = 6, Name = "tcp" },
                new() { Id = 50, Name = "esp" }
            ];

            IRenderedComponent<ServiceSelector> component = context.Render<ServiceSelector>(parameters => parameters
                .Add(p => p.Services, new List<NwServiceElement>())
                .Add(p => p.ServicesToAdd, servicesToAdd)
                .Add(p => p.IpProtos, ipProtos)
                .Add(p => p.WithLabel, false));

            await component.InvokeAsync(() => SetMember(component.Instance, "actPort", 443));
            component.Find("button.btn-success").Click();

            IRenderedComponent<Dropdown<int>> protoDropdown = component.FindComponent<Dropdown<int>>();
            await (await StartPrivateTask(protoDropdown, "SelectElement", 50));

            Assert.That(component.FindComponents<PortRangeInput>(), Is.Empty);

            component.Find("button.btn-success").Click();
            Assert.Multiple(() =>
            {
                Assert.That(servicesToAdd, Has.Count.EqualTo(2));
                Assert.That(servicesToAdd[0].ProtoId, Is.EqualTo(6));
                Assert.That(servicesToAdd[0].Port, Is.EqualTo(443));
                Assert.That(servicesToAdd[1].ProtoId, Is.EqualTo(50));
                Assert.That(servicesToAdd[1].Port, Is.EqualTo(0));
                Assert.That(servicesToAdd[1].PortEnd, Is.Null);
            });

            servicesToAdd.RemoveAll(service => service.ProtoId == 50);
            await (await StartPrivateTask(protoDropdown, "SelectElement", 50));
            component.Find("button.btn-success").Click();

            Assert.That(servicesToAdd.Count(service => service.ProtoId == 50), Is.EqualTo(1));
        }


    }
}
