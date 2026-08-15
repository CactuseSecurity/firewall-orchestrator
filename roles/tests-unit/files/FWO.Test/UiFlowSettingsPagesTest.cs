using Bunit;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Flow;
using FWO.Ui.Pages.Settings;
using FWO.Ui.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Security.Claims;
using System.Linq;
using System.Reflection;

namespace FWO.Test
{
    [TestFixture]
    internal class UiFlowSettingsPagesTest
    {
        private static readonly long[] kUnmappedNetworkCandidateIds = [13, 14];
        private static readonly string[] kUnmappedNetworkCandidateTypes = ["host", "host"];

        [SetUp]
        public void SetUp()
        {
            SeedTranslations();
        }

        [Test]
        public async Task FlowNetworkGroupsPage_RendersWithoutErrors()
        {
            await using BunitContext context = CreateContext();

            IRenderedComponent<SettingsFlowNetworkGroups> component = RenderPage<SettingsFlowNetworkGroups>(context);

            component.WaitForAssertion(() => Assert.That(component.Markup, Does.Contain("Flow Network Group")));
        }

        [Test]
        public async Task FlowNetworkGroupsPage_ShowsMemberDetailsFromFlowMembers()
        {
            await using BunitContext context = CreateContext();

            IRenderedComponent<SettingsFlowNetworkGroups> component = RenderPage<SettingsFlowNetworkGroups>(context);

            component.WaitForAssertion(() =>
            {
                var tables = component.FindAll("table");
                var objectsCell = tables[tables.Count - 1]
                    .QuerySelectorAll("tbody tr")[0]
                    .Children[5];

                Assert.That(objectsCell.TextContent.Trim(), Does.Contain("10.0.0.1"));
                Assert.That(objectsCell.TextContent.Trim(), Does.Contain("10.0.0.2"));
            });
        }

        [Test]
        public async Task FlowServiceObjectsPage_RendersWithoutErrors()
        {
            await using BunitContext context = CreateContext();

            IRenderedComponent<SettingsFlowServiceObjects> component = RenderPage<SettingsFlowServiceObjects>(context);

            component.WaitForAssertion(() => Assert.That(component.Markup, Does.Contain("Flow Service Object")));
            component.WaitForAssertion(() => Assert.That(component.Markup, Does.Contain("TCP")));
        }

        [Test]
        public async Task FlowServiceObjectsPage_CreateCustomObject_SendsInsertAndMapping()
        {
            await using BunitContext context = CreateCustomServiceCreateContext(out FlowServiceObjectsCustomCreateApiConn apiConnection);

            IRenderedComponent<SettingsFlowServiceObjects> component = RenderPage<SettingsFlowServiceObjects>(context);
            component.WaitForAssertion(() => Assert.That(component.FindAll("button.btn.btn-sm.btn-primary"), Is.Not.Empty));

            component.FindAll("button.btn.btn-sm.btn-primary")[0].Click();
            component.WaitForAssertion(() => Assert.That(component.FindAll("input.form-control.form-control-sm"), Is.Not.Empty));

            component.FindAll("input.form-control.form-control-sm")[0].Change("Custom Service");
            component.FindAll("button.btn-outline-primary")[0].Click();
            component.WaitForAssertion(() => Assert.That(component.FindAll("button.btn-success"), Is.Not.Empty));
            component.FindAll("button.btn.btn-sm.btn-primary")[^1].Click();

            component.WaitForAssertion(() =>
            {
                Assert.That(apiConnection.Queries, Does.Contain(FlowQueries.insertFlowSvcObjects));
                Assert.That(apiConnection.Queries, Does.Contain(FlowMutations.upsertFlowSvcObjectMapping));
                Assert.That(apiConnection.InsertedServiceObject, Is.Not.Null);
                Assert.That(apiConnection.InsertedServiceObject!.Name, Is.EqualTo("Custom Service"));
                Assert.That(apiConnection.InsertedServiceObject.PortStart, Is.Null);
                Assert.That(apiConnection.InsertedServiceObject.PortEnd, Is.Null);
                Assert.That(apiConnection.InsertedServiceObject.IpProtoId, Is.EqualTo(6));
                Assert.That(apiConnection.InsertedServiceObject.SvcObjHash, Is.Not.Null.And.Length.EqualTo(32));
                Assert.That(apiConnection.InsertedServiceObject.SvcObjHash, Is.Not.EqualTo(FlowHashGenerator.GenerateSvcObjectHash(6, 80, 80)));
                Assert.That(apiConnection.MappingCalls, Is.EqualTo(new List<(long ServiceId, long FlowSvcobjId, bool ActiveOnMgm)>
                {
                    (11, 900, true)
                }));
            });
        }

        [Test]
        public async Task FlowServiceObjectsPage_CreateCustomObject_AlwaysCreatesNewFlowObject()
        {
            await using BunitContext context = CreateCustomServiceReuseContext(out FlowServiceObjectsCustomCreateApiConn apiConnection);

            IRenderedComponent<SettingsFlowServiceObjects> component = RenderPage<SettingsFlowServiceObjects>(context);
            component.WaitForAssertion(() => Assert.That(component.FindAll("button.btn.btn-sm.btn-primary"), Is.Not.Empty));

            component.FindAll("button.btn.btn-sm.btn-primary")[0].Click();
            component.WaitForAssertion(() => Assert.That(component.FindAll("input.form-control.form-control-sm"), Is.Not.Empty));

            component.FindAll("input.form-control.form-control-sm")[0].Change("Custom Service");
            component.FindAll("button.btn-outline-primary")[0].Click();
            component.WaitForAssertion(() => Assert.That(component.FindAll("button.btn-success"), Is.Not.Empty));
            component.FindAll("button.btn.btn-sm.btn-primary")[^1].Click();

            component.WaitForAssertion(() =>
            {
                Assert.That(apiConnection.Queries, Does.Contain(FlowQueries.insertFlowSvcObjects));
                Assert.That(apiConnection.InsertedServiceObject, Is.Not.Null);
                Assert.That(apiConnection.InsertedServiceObject!.SvcObjHash, Has.Length.EqualTo(32));
                Assert.That(apiConnection.InsertedServiceObject.SvcObjHash, Is.Not.EqualTo(FlowHashGenerator.GenerateSvcObjectHash(6, 80, 80)));
                Assert.That(apiConnection.MappingCalls, Is.EqualTo(new List<(long ServiceId, long FlowSvcobjId, bool ActiveOnMgm)>
                {
                    (11, 900, true)
                }));
            });
        }

        [Test]
        public async Task FlowServiceObjectsPage_CreateCustomObject_DoesNotOfferServiceGroupCandidates()
        {
            await using BunitContext context = CreateCustomServiceCreateContext(out _);

            IRenderedComponent<SettingsFlowServiceObjects> component = RenderPage<SettingsFlowServiceObjects>(context);
            component.WaitForAssertion(() => Assert.That(component.FindAll("button.btn.btn-sm.btn-primary"), Is.Not.Empty));

            component.FindAll("button.btn.btn-sm.btn-primary")[0].Click();

            component.WaitForAssertion(() =>
            {
                Assert.That(component.Markup, Does.Not.Contain("Service Group Candidate"));
                Assert.That(component.Markup, Does.Contain("Service A"));
            });
        }

        [Test]
        public async Task FlowServiceObjectsPage_CreateCustomObject_DoesNotOfferMappedOrPortBoundServices()
        {
            await using BunitContext context = CreateCustomServiceCreateContext(out _);

            IRenderedComponent<SettingsFlowServiceObjects> component = RenderPage<SettingsFlowServiceObjects>(context);
            component.WaitForAssertion(() => Assert.That(component.FindAll("button.btn.btn-sm.btn-primary"), Is.Not.Empty));

            component.FindAll("button.btn.btn-sm.btn-primary")[0].Click();

            component.WaitForAssertion(() =>
            {
                Assert.That(component.Markup, Does.Not.Contain("Mapped Service"));
                Assert.That(component.Markup, Does.Not.Contain("Port Service"));
                Assert.That(component.Markup, Does.Contain("Service A"));
            });
        }

        [Test]
        public async Task FlowServiceObjectsPage_CreateCustomObject_AllowsDeselectingSelectedService()
        {
            await using BunitContext context = CreateCustomServiceCreateContext(out _);

            IRenderedComponent<SettingsFlowServiceObjects> component = RenderPage<SettingsFlowServiceObjects>(context);
            component.WaitForAssertion(() => Assert.That(component.FindAll("button.btn.btn-sm.btn-primary"), Is.Not.Empty));

            component.FindAll("button.btn.btn-sm.btn-primary")[0].Click();
            component.WaitForAssertion(() => Assert.That(component.FindAll("button.btn-outline-primary"), Is.Not.Empty));

            component.FindAll("button.btn-outline-primary")[0].Click();
            component.WaitForAssertion(() => Assert.That(component.FindAll("tr.table-warning"), Has.Count.EqualTo(1)));
            var successButtons = component.FindAll("button.btn-success");
            successButtons[successButtons.Count - 1].Click();

            component.WaitForAssertion(() => Assert.That(component.FindAll("tr.table-warning"), Is.Empty));
        }

        [Test]
        public async Task FlowServiceObjectsPage_CreateCustomObject_RejectsMixedTechnicalDefinitionSelection()
        {
            await using BunitContext context = CreateCustomServiceCreateContext(out FlowServiceObjectsCustomCreateApiConn apiConnection);

            IRenderedComponent<SettingsFlowServiceObjects> component = RenderPage<SettingsFlowServiceObjects>(context);
            string? errorMessage = null;
            SetMember(component.Instance, "DisplayMessageInUi", new Action<Exception?, string, string, bool>((exception, _, message, _) =>
            {
                errorMessage = exception?.Message ?? message;
            }));
            component.WaitForAssertion(() => Assert.That(component.FindAll("button.btn.btn-sm.btn-primary"), Is.Not.Empty));

            component.FindAll("button.btn.btn-sm.btn-primary")[0].Click();
            component.WaitForAssertion(() => Assert.That(component.FindAll("input.form-control.form-control-sm"), Is.Not.Empty));

            component.FindAll("input.form-control.form-control-sm")[0].Change("Custom Service");
            component.FindAll("button.btn-outline-primary")[0].Click();
            component.FindAll("button.btn-outline-primary")[^1].Click();
            component.FindAll("button.btn.btn-sm.btn-primary")[^1].Click();

            component.WaitForAssertion(() =>
            {
                Assert.That(errorMessage, Is.EqualTo("Selected services must share the same protocol and port range"));
                Assert.That(apiConnection.InsertedServiceObject, Is.Null);
                Assert.That(apiConnection.MappingCalls, Is.Empty);
                Assert.That(apiConnection.Queries, Does.Not.Contain(FlowQueries.insertFlowSvcObjects));
                Assert.That(apiConnection.Queries, Does.Not.Contain(FlowMutations.upsertFlowSvcObjectMapping));
            });
        }

        [Test]
        public async Task FlowServiceObjectsPage_CreateCustomObject_ShowsNameMissingError()
        {
            await using BunitContext context = CreateCustomServiceCreateContext(out FlowServiceObjectsCustomCreateApiConn apiConnection);

            IRenderedComponent<SettingsFlowServiceObjects> component = RenderPage<SettingsFlowServiceObjects>(context);
            string? errorMessage = null;
            SetMember(component.Instance, "DisplayMessageInUi", new Action<Exception?, string, string, bool>((exception, _, message, _) =>
            {
                errorMessage = exception?.Message ?? message;
            }));
            component.WaitForAssertion(() => Assert.That(component.FindAll("button.btn.btn-sm.btn-primary"), Is.Not.Empty));

            component.FindAll("button.btn.btn-sm.btn-primary")[0].Click();
            component.WaitForAssertion(() => Assert.That(component.FindAll("input.form-control.form-control-sm"), Is.Not.Empty));

            component.FindAll("button.btn.btn-sm.btn-primary")[^1].Click();

            component.WaitForAssertion(() =>
            {
                Assert.That(errorMessage, Is.EqualTo("Please enter a name for the custom flow object"));
                Assert.That(apiConnection.Queries, Does.Not.Contain(FlowQueries.insertFlowSvcObjects));
                Assert.That(apiConnection.Queries, Does.Not.Contain(FlowMutations.upsertFlowSvcObjectMapping));
            });
        }

        [Test]
        public async Task FlowServiceObjectsPage_CreateCustomObject_ShowsNoServiceSelectedError()
        {
            await using BunitContext context = CreateCustomServiceCreateContext(out FlowServiceObjectsCustomCreateApiConn apiConnection);

            IRenderedComponent<SettingsFlowServiceObjects> component = RenderPage<SettingsFlowServiceObjects>(context);
            string? errorMessage = null;
            SetMember(component.Instance, "DisplayMessageInUi", new Action<Exception?, string, string, bool>((exception, _, message, _) =>
            {
                errorMessage = exception?.Message ?? message;
            }));
            component.WaitForAssertion(() => Assert.That(component.FindAll("button.btn.btn-sm.btn-primary"), Is.Not.Empty));

            component.FindAll("button.btn.btn-sm.btn-primary")[0].Click();
            component.WaitForAssertion(() => Assert.That(component.FindAll("input.form-control.form-control-sm"), Is.Not.Empty));

            component.FindAll("input.form-control.form-control-sm")[0].Change("Custom Service");
            component.FindAll("button.btn.btn-sm.btn-primary")[^1].Click();

            component.WaitForAssertion(() =>
            {
                Assert.That(errorMessage, Is.EqualTo("Please select at least one service"));
                Assert.That(apiConnection.Queries, Does.Not.Contain(FlowQueries.insertFlowSvcObjects));
                Assert.That(apiConnection.Queries, Does.Not.Contain(FlowMutations.upsertFlowSvcObjectMapping));
            });
        }

        [Test]
        public async Task FlowServiceObjectsPage_CreateCustomObject_AllowsProtocolOnlyService()
        {
            await using BunitContext context = CreateProtocolOnlyServiceCreateContext(out FlowServiceObjectsProtocolOnlyApiConn apiConnection);

            IRenderedComponent<SettingsFlowServiceObjects> component = RenderPage<SettingsFlowServiceObjects>(context);
            string? errorMessage = null;
            SetMember(component.Instance, "DisplayMessageInUi", new Action<Exception?, string, string, bool>((exception, _, message, _) =>
            {
                errorMessage = exception?.Message ?? message;
            }));
            component.WaitForAssertion(() => Assert.That(component.FindAll("button.btn.btn-sm.btn-primary"), Is.Not.Empty));

            component.FindAll("button.btn.btn-sm.btn-primary")[0].Click();
            component.WaitForAssertion(() => Assert.That(component.FindAll("input.form-control.form-control-sm"), Is.Not.Empty));

            component.FindAll("input.form-control.form-control-sm")[0].Change("Protocol Only");
            component.FindAll("button.btn-outline-primary")[0].Click();
            component.FindAll("button.btn.btn-sm.btn-primary")[^1].Click();

            component.WaitForAssertion(() =>
            {
                Assert.That(apiConnection.Queries, Does.Contain(FlowQueries.insertFlowSvcObjects));
                Assert.That(apiConnection.Queries, Does.Contain(FlowMutations.upsertFlowSvcObjectMapping));
                Assert.That(apiConnection.InsertedServiceObject, Is.Not.Null);
                Assert.That(apiConnection.InsertedServiceObject!.Name, Is.EqualTo("Protocol Only"));
                Assert.That(apiConnection.InsertedServiceObject.PortStart, Is.Null);
                Assert.That(apiConnection.InsertedServiceObject.PortEnd, Is.Null);
                Assert.That(apiConnection.InsertedServiceObject.IpProtoId, Is.EqualTo(1));
                Assert.That(apiConnection.InsertedServiceObject.SvcObjHash, Is.Not.Null.And.Length.EqualTo(32));
                Assert.That(apiConnection.MappingCalls, Is.EqualTo(new List<(long ServiceId, long FlowSvcobjId, bool ActiveOnMgm)>
                {
                    (11, 900, true)
                }));
                Assert.That(errorMessage, Is.Null, errorMessage);
            }, TimeSpan.FromSeconds(3));
        }

        [Test]
        public async Task FlowServiceObjectsPage_ResolveDuplicateMapping_SendsExpectedMutations()
        {
            await using BunitContext context = CreateDuplicateResolverContext(out FlowServiceObjectsDuplicateResolverApiConn apiConnection);

            IRenderedComponent<SettingsFlowServiceObjects> component = RenderPage<SettingsFlowServiceObjects>(context);
            component.WaitForAssertion(() => Assert.That(component.FindAll("button.btn.btn-sm.btn-warning"), Is.Not.Empty));

            component.FindAll("button.btn.btn-sm.btn-warning")[0].Click();
            component.WaitForAssertion(() => Assert.That(component.FindAll("button.btn-outline-primary"), Is.Not.Empty));
            component.FindAll("button.btn-outline-primary")[^1].Click();
            component.FindAll("button.btn.btn-sm.btn-warning")[^1].Click();

            component.WaitForAssertion(() =>
            {
                Assert.That(apiConnection.Queries, Does.Contain(FlowMutations.upsertFlowSvcObjectMapping));
                Assert.That(apiConnection.MappingCalls, Is.EqualTo(new List<(long ServiceId, long FlowSvcobjId, bool ActiveOnMgm)>
                {
                    (11, 100, false),
                    (12, 100, true)
                }));
                Assert.That(apiConnection.Queries.Count(query => query == FlowQueries.getFlowServiceObjects), Is.EqualTo(1));
            });
        }

        [Test]
        public async Task FlowServiceObjectsPage_ResolveDuplicateMapping_BackfillsMissingFlowName()
        {
            await using BunitContext context = CreateUnnamedDuplicateResolverContext(out FlowServiceObjectsUnnamedDuplicateResolverApiConn apiConnection);

            IRenderedComponent<SettingsFlowServiceObjects> component = RenderPage<SettingsFlowServiceObjects>(context);
            component.WaitForAssertion(() => Assert.That(component.FindAll("button.btn.btn-sm.btn-warning"), Is.Not.Empty));

            component.FindAll("button.btn.btn-sm.btn-warning")[0].Click();
            component.WaitForAssertion(() => Assert.That(component.FindAll("button.btn-outline-primary"), Is.Not.Empty));
            component.FindAll("button.btn-outline-primary")[^1].Click();
            component.FindAll("button.btn.btn-sm.btn-warning")[^1].Click();

            component.WaitForAssertion(() =>
            {
                Assert.That(apiConnection.MappingCalls, Is.EqualTo(new List<(long ServiceId, long FlowSvcobjId, bool ActiveOnMgm)>
                {
                    (11, 100, false),
                    (12, 100, true)
                }));
                Assert.That(apiConnection.Queries, Does.Contain(FlowMutations.updateFlowSvcObject));
                Assert.That(apiConnection.UpdatedFlowObjectName, Is.EqualTo("Service B"));
            });
        }

        [Test]
        public async Task FlowServiceObjectsPage_ResolveDuplicateMapping_ExcludesMappedServicesFromCreateDialog()
        {
            await using BunitContext context = CreateDuplicateResolverContext(out FlowServiceObjectsDuplicateResolverApiConn apiConnection);

            IRenderedComponent<SettingsFlowServiceObjects> component = RenderPage<SettingsFlowServiceObjects>(context);
            component.WaitForAssertion(() => Assert.That(component.FindAll("button.btn.btn-sm.btn-warning"), Is.Not.Empty));

            component.FindAll("button.btn.btn-sm.btn-warning")[0].Click();
            component.WaitForAssertion(() => Assert.That(component.FindAll("button.btn-outline-primary"), Is.Not.Empty));
            component.FindAll("button.btn-outline-primary")[^1].Click();
            component.FindAll("button.btn.btn-sm.btn-warning")[^1].Click();

            component.WaitForAssertion(() =>
            {
                Assert.That(apiConnection.Queries, Does.Contain(FlowMutations.upsertFlowSvcObjectMapping));
                Assert.That(apiConnection.MappingCalls, Is.EqualTo(new List<(long ObjectId, long FlowNwobjId, bool ActiveOnMgm)>
                {
                    (11, 100, false),
                    (12, 100, true)
                }));
            });

            component.FindAll("button.btn.btn-sm.btn-primary")[0].Click();
            component.WaitForAssertion(() => Assert.That(component.FindAll("input.form-control.form-control-sm"), Is.Not.Empty));
            component.WaitForAssertion(() => Assert.That(component.FindAll("button.btn-outline-primary"), Is.Empty));
        }

        [Test]
        public async Task FlowServiceGroupsPage_RendersWithoutErrors()
        {
            await using BunitContext context = CreateContext();

            IRenderedComponent<SettingsFlowServiceGroups> component = RenderPage<SettingsFlowServiceGroups>(context);

            component.WaitForAssertion(() => Assert.That(component.Markup, Does.Contain("Flow Service Group")));
        }

        [Test]
        public async Task FlowNetworkGroupsPage_DuplicateResolverShowsMemberDetails()
        {
            await using BunitContext context = CreateContext();

            IRenderedComponent<SettingsFlowNetworkGroups> component = RenderPage<SettingsFlowNetworkGroups>(context);
            component.WaitForAssertion(() => Assert.That(component.FindAll("button.btn.btn-sm.btn-warning"), Is.Not.Empty));

            component.FindAll("button.btn.btn-sm.btn-warning")[0].Click();

            component.WaitForAssertion(() =>
            {
                Assert.That(component.Markup, Does.Contain("10.0.0.1"));
                Assert.That(component.Markup, Does.Contain("10.0.0.2"));
            });
        }

        [Test]
        public async Task FlowServiceGroupsPage_DuplicateResolverShowsMemberDetails()
        {
            await using BunitContext context = CreateContext();

            IRenderedComponent<SettingsFlowServiceGroups> component = RenderPage<SettingsFlowServiceGroups>(context);
            component.WaitForAssertion(() => Assert.That(component.FindAll("button.btn.btn-sm.btn-warning"), Is.Not.Empty));

            component.FindAll("button.btn.btn-sm.btn-warning")[0].Click();

            component.WaitForAssertion(() =>
            {
                Assert.That(component.Markup, Does.Contain("80/TCP"));
            });
        }

        [Test]
        public async Task FlowTimeObjectsPage_RendersWithoutErrors()
        {
            await using BunitContext context = CreateContext();

            IRenderedComponent<SettingsFlowTimeObjects> component = RenderPage<SettingsFlowTimeObjects>(context);

            component.WaitForAssertion(() => Assert.That(component.Markup, Does.Contain("Flow Time Object")));
        }

        [Test]
        public async Task FlowNetworkGroupsPage_ResolveDuplicateMapping_SendsExpectedMutations()
        {
            await using BunitContext context = CreateContext(out FlowSettingsPagesTestApiConn apiConnection);

            IRenderedComponent<SettingsFlowNetworkGroups> component = RenderPage<SettingsFlowNetworkGroups>(context);
            component.WaitForAssertion(() => Assert.That(component.FindAll("button.btn.btn-sm.btn-warning"), Is.Not.Empty));

            component.FindAll("button.btn.btn-sm.btn-warning")[0].Click();
            component.WaitForAssertion(() => Assert.That(component.FindAll("button.btn-outline-primary"), Is.Not.Empty));
            component.FindAll("button.btn-outline-primary")[^1].Click();
            component.FindAll("button.btn.btn-sm.btn-warning")[^1].Click();

            component.WaitForAssertion(() =>
            {
                Assert.That(apiConnection.Queries, Does.Contain(FlowMutations.upsertFlowNwGroupMapping));
                Assert.That(apiConnection.NetworkGroupMappingCalls, Is.EqualTo(new List<(long ObjectId, long FlowNwgrpId, bool ActiveOnMgm)>
                {
                    (21, 300, false),
                    (22, 300, true)
                }));
            });
        }

        [Test]
        public async Task FlowServiceGroupsPage_ResolveDuplicateMapping_SendsExpectedMutations()
        {
            await using BunitContext context = CreateContext(out FlowSettingsPagesTestApiConn apiConnection);

            IRenderedComponent<SettingsFlowServiceGroups> component = RenderPage<SettingsFlowServiceGroups>(context);
            component.WaitForAssertion(() => Assert.That(component.FindAll("button.btn.btn-sm.btn-warning"), Is.Not.Empty));

            component.FindAll("button.btn.btn-sm.btn-warning")[0].Click();
            component.WaitForAssertion(() => Assert.That(component.FindAll("button.btn-outline-primary"), Is.Not.Empty));
            component.FindAll("button.btn-outline-primary")[^1].Click();
            component.FindAll("button.btn.btn-sm.btn-warning")[^1].Click();

            component.WaitForAssertion(() =>
            {
                Assert.That(apiConnection.Queries, Does.Contain(FlowMutations.upsertFlowSvcGroupMapping));
                Assert.That(apiConnection.ServiceGroupMappingCalls, Is.EqualTo(new List<(long ServiceId, long FlowSvcgrpId, bool ActiveOnMgm)>
                {
                    (11, 200, false),
                    (12, 200, true)
                }));
            });
        }

        [Test]
        public async Task FlowTimeObjectsPage_ResolveDuplicateMapping_SendsExpectedMutations()
        {
            await using BunitContext context = CreateContext(out FlowSettingsPagesTestApiConn apiConnection);

            IRenderedComponent<SettingsFlowTimeObjects> component = RenderPage<SettingsFlowTimeObjects>(context);
            component.WaitForAssertion(() => Assert.That(component.FindAll("button.btn.btn-sm.btn-warning"), Is.Not.Empty));

            component.FindAll("button.btn.btn-sm.btn-warning")[0].Click();
            component.WaitForAssertion(() => Assert.That(component.FindAll("button.btn-outline-primary"), Is.Not.Empty));
            component.FindAll("button.btn-outline-primary")[^1].Click();
            component.FindAll("button.btn.btn-sm.btn-warning")[^1].Click();

            component.WaitForAssertion(() =>
            {
                Assert.That(apiConnection.Queries, Does.Contain(FlowMutations.upsertFlowTimeObjectMapping));
                Assert.That(apiConnection.TimeObjectMappingCalls, Is.EqualTo(new List<(long TimeObjectId, long FlowTimeobjId, bool ActiveOnMgm)>
                {
                    (31, 400, false),
                    (32, 400, true)
                }));
            });
        }

        [Test]
        public async Task FlowGeneralPage_RecalculateNames_UsesNamingManagementCandidates()
        {
            await using BunitContext context = CreateNetworkObjectsContext(out FlowNetworkObjectsNamingApiConn apiConnection);

            IRenderedComponent<SettingsFlowGeneral> component = RenderPage<SettingsFlowGeneral>(context);
            FieldInfo? namingManagementField = typeof(SettingsFlowGeneral).GetField("namingNetworkObjectManagements", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(namingManagementField, Is.Not.Null);
            component.WaitForAssertion(() =>
            {
                List<Management> namingManagements = (List<Management>)namingManagementField!.GetValue(component.Instance)!;
                Assert.That(namingManagements, Has.Count.EqualTo(2));
            });

            MethodInfo? saveNamingSource = typeof(SettingsFlowGeneral).GetMethod("SaveNamingSource", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(saveNamingSource, Is.Not.Null);
            await component.InvokeAsync(async () => await (Task)saveNamingSource!.Invoke(component.Instance, null)!);

            component.WaitForAssertion(() =>
            {
                Assert.That(apiConnection.Queries, Does.Contain(FlowQueries.getFlowCustomObjectNamingCandidates));
                Assert.That(apiConnection.Queries, Does.Contain(FlowQueries.getFlowCustomServiceNamingCandidates));
                Assert.That(apiConnection.Queries, Does.Contain(FlowQueries.getFlowCustomTimeObjectNamingCandidates));
                Assert.That(apiConnection.UpdatedFlowObjectNames, Has.Count.EqualTo(1).And.Contains("Global Object Name"));
            });
        }

        [Test]
        public async Task FlowNetworkObjectsPage_ShowsSpinnerOnBusyActionButtons()
        {
            await using BunitContext context = CreateNetworkObjectsContext(out _);

            IRenderedComponent<SettingsFlowNetworkObjects> component = RenderPage<SettingsFlowNetworkObjects>(context);
            SetMember(component.Instance, "workInProgress", true);
            component.Render();

            component.WaitForAssertion(() =>
            {
                var busyButtons = component.FindAll("button")
                    .Where(button => button.InnerHtml.Contains("spinner-border", StringComparison.Ordinal))
                    .ToList();
                Assert.That(busyButtons, Is.Not.Empty);
                Assert.That(busyButtons.All(button => button.GetAttribute("disabled") != null), Is.True);
            });
        }

        [Test]
        public async Task FlowNetworkObjectsPage_ShowsIpRangeInCatalog()
        {
            await using BunitContext context = CreateNetworkObjectsContext(out _);

            IRenderedComponent<SettingsFlowNetworkObjects> component = RenderPage<SettingsFlowNetworkObjects>(context);

            component.WaitForAssertion(() =>
            {
                Assert.That(component.Markup, Does.Contain("192.0.2.10-192.0.2.20"));
            });
        }

        [Test]
        public async Task FlowNetworkObjectsPage_Refresh_ReloadsCustomObjectCandidates()
        {
            await using BunitContext context = CreateNetworkObjectsContext(out FlowNetworkObjectsNamingApiConn apiConnection);

            IRenderedComponent<SettingsFlowNetworkObjects> component = RenderPage<SettingsFlowNetworkObjects>(context);
            MethodInfo? refresh = typeof(SettingsFlowNetworkObjects).GetMethod("Refresh", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(refresh, Is.Not.Null);

            component.WaitForAssertion(() => Assert.That(apiConnection.CustomObjectCandidateQueryCount, Is.EqualTo(1)));

            await component.InvokeAsync(async () => await (Task)refresh!.Invoke(component.Instance, null)!);

            component.WaitForAssertion(() => Assert.That(apiConnection.CustomObjectCandidateQueryCount, Is.EqualTo(2)));
        }

        [Test]
        public async Task FlowNetworkObjectsPage_ResolveDuplicateMapping_ShowsOnlyUnmappedObjectsInCustomObjectDialog()
        {
            await using BunitContext context = CreateNetworkDuplicateResolverContext(out FlowNetworkObjectsDuplicateResolverApiConn apiConnection);

            IRenderedComponent<SettingsFlowNetworkObjects> component = RenderPage<SettingsFlowNetworkObjects>(context);
            FieldInfo? duplicateGroupsField = typeof(SettingsFlowNetworkObjects).GetField("duplicateGroups", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo? selectedGroupField = typeof(SettingsFlowNetworkObjects).GetField("SelectedDuplicateGroup", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo? resolveDuplicateMapping = typeof(SettingsFlowNetworkObjects).GetMethod("ResolveDuplicateMapping", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo? openCreateCustomObject = typeof(SettingsFlowNetworkObjects).GetMethod("OpenCreateCustomObject", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(duplicateGroupsField, Is.Not.Null);
            Assert.That(selectedGroupField, Is.Not.Null);
            Assert.That(resolveDuplicateMapping, Is.Not.Null);
            Assert.That(openCreateCustomObject, Is.Not.Null);

            component.WaitForAssertion(() =>
            {
                List<FlowNwObjectDuplicateGroup> duplicateGroups = (List<FlowNwObjectDuplicateGroup>)duplicateGroupsField!.GetValue(component.Instance)!;
                Assert.That(duplicateGroups, Has.Count.EqualTo(1));
            });

            List<FlowNwObjectDuplicateGroup> loadedDuplicateGroups = (List<FlowNwObjectDuplicateGroup>)duplicateGroupsField!.GetValue(component.Instance)!;
            FlowNwObjectDuplicateGroup duplicateGroup = loadedDuplicateGroups.Single();
            selectedGroupField!.SetValue(component.Instance, duplicateGroup);
            await component.InvokeAsync(async () => await (Task)resolveDuplicateMapping!.Invoke(component.Instance, [duplicateGroup.Objects[0]])!);

            component.WaitForAssertion(() => Assert.That(apiConnection.MappingCalls.Count, Is.EqualTo(2)));

            await component.InvokeAsync(async () => await (Task)openCreateCustomObject!.Invoke(component.Instance, null)!);
            FieldInfo? selectionsField = typeof(SettingsFlowNetworkObjects).GetField("customObjectSelections", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(selectionsField, Is.Not.Null);
            System.Collections.IEnumerable selections = (System.Collections.IEnumerable)selectionsField!.GetValue(component.Instance)!;
            object selection = selections.Cast<object>().Single();
            PropertyInfo searchTextProperty = selection.GetType().GetProperty("SearchText")!;
            PropertyInfo filteredCandidatesProperty = selection.GetType().GetProperty("FilteredCandidates")!;

            searchTextProperty.SetValue(selection, "host");
            List<NetworkObject> candidatesByType = GetFilteredCandidates(selection, filteredCandidatesProperty);
            Assert.That(candidatesByType.Select(candidate => candidate.Id), Is.EqualTo(kUnmappedNetworkCandidateIds));
            Assert.That(candidatesByType.Select(candidate => candidate.Type.Name), Is.EqualTo(kUnmappedNetworkCandidateTypes));

            searchTextProperty.SetValue(selection, "Object C");
            Assert.That(GetFilteredCandidates(selection, filteredCandidatesProperty).Single().Id, Is.EqualTo(13));

            searchTextProperty.SetValue(selection, "13");
            Assert.That(GetFilteredCandidates(selection, filteredCandidatesProperty).Single().Id, Is.EqualTo(13));

            searchTextProperty.SetValue(selection, "obj-c");
            Assert.That(GetFilteredCandidates(selection, filteredCandidatesProperty).Single().Id, Is.EqualTo(13));
        }

        [Test]
        public async Task FlowNetworkObjectsPage_CreateCustomObject_AllowsDeselectingSelectedObject()
        {
            await using BunitContext context = CreateNetworkObjectsContext(out _);

            IRenderedComponent<SettingsFlowNetworkObjects> component = RenderPage<SettingsFlowNetworkObjects>(context);
            component.WaitForAssertion(() => Assert.That(component.FindAll("button.btn.btn-sm.btn-primary"), Is.Not.Empty));
            component.FindAll("button.btn.btn-sm.btn-primary")[0].Click();
            component.WaitForAssertion(() => Assert.That(component.FindAll("button.btn-outline-primary"), Is.Not.Empty));
            component.FindAll("button.btn-outline-primary")[0].Click();
            component.WaitForAssertion(() => Assert.That(component.FindAll("tr.table-warning"), Has.Count.EqualTo(1)));
            var successButtons = component.FindAll("button.btn-success");
            successButtons[successButtons.Count - 1].Click();

            component.WaitForAssertion(() => Assert.That(component.FindAll("tr.table-warning"), Is.Empty));
        }

        private static BunitContext CreateContext()
        {
            return CreateContext(out _);
        }

        private static BunitContext CreateContext(out FlowSettingsPagesTestApiConn apiConnection)
        {
            BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddLocalization();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            apiConnection = new FlowSettingsPagesTestApiConn();
            context.Services.AddSingleton<ApiConnection>(apiConnection);
            context.Services.AddScoped<DomEventService>();
            context.Services.AddSingleton<UserConfig>(new SimulatedUserConfig
            {
                User = { Roles = [Roles.Admin] }
            });
            context.Services.AddSingleton<AuthenticationStateProvider>(new FlowSettingsPagesAuthStateProvider(Roles.Admin));
            return context;
        }

        private static List<NetworkObject> GetFilteredCandidates(object selection, PropertyInfo filteredCandidatesProperty)
        {
            return ((System.Collections.IEnumerable)filteredCandidatesProperty.GetValue(selection)!)
                .Cast<NetworkObject>()
                .ToList();
        }

        private static BunitContext CreateCustomServiceCreateContext(out FlowServiceObjectsCustomCreateApiConn apiConnection)
        {
            BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddLocalization();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            apiConnection = new FlowServiceObjectsCustomCreateApiConn();
            context.Services.AddSingleton<ApiConnection>(apiConnection);
            context.Services.AddScoped<DomEventService>();
            context.Services.AddSingleton<UserConfig>(new SimulatedUserConfig
            {
                User = { Roles = [Roles.Admin] }
            });
            context.Services.AddSingleton<AuthenticationStateProvider>(new FlowSettingsPagesAuthStateProvider(Roles.Admin));
            return context;
        }

        private static BunitContext CreateProtocolOnlyServiceCreateContext(out FlowServiceObjectsProtocolOnlyApiConn apiConnection)
        {
            BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddLocalization();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            apiConnection = new FlowServiceObjectsProtocolOnlyApiConn();
            context.Services.AddSingleton<ApiConnection>(apiConnection);
            context.Services.AddScoped<DomEventService>();
            context.Services.AddSingleton<UserConfig>(new SimulatedUserConfig
            {
                User = { Roles = [Roles.Admin] }
            });
            context.Services.AddSingleton<AuthenticationStateProvider>(new FlowSettingsPagesAuthStateProvider(Roles.Admin));
            return context;
        }

        private static BunitContext CreateCustomServiceReuseContext(out FlowServiceObjectsCustomCreateApiConn apiConnection)
        {
            BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddLocalization();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            apiConnection = new FlowServiceObjectsCustomCreateApiConn(
                existingFlowSvcObject: new FlowSvcObject
                {
                    Id = 777,
                    Name = "Existing HTTP Service",
                    PortStart = 80,
                    PortEnd = 80,
                    ProtoId = 6,
                    Hash = FlowHashGenerator.GenerateSvcObjectHash(6, 80, 80),
                    State = FlowState.Implemented,
                    ShowInRequestModule = true
                });
            context.Services.AddSingleton<ApiConnection>(apiConnection);
            context.Services.AddScoped<DomEventService>();
            context.Services.AddSingleton<UserConfig>(new SimulatedUserConfig
            {
                User = { Roles = [Roles.Admin] }
            });
            context.Services.AddSingleton<AuthenticationStateProvider>(new FlowSettingsPagesAuthStateProvider(Roles.Admin));
            return context;
        }

        private static BunitContext CreateDuplicateResolverContext(out FlowServiceObjectsDuplicateResolverApiConn apiConnection)
        {
            BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddLocalization();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            apiConnection = new FlowServiceObjectsDuplicateResolverApiConn();
            context.Services.AddSingleton<ApiConnection>(apiConnection);
            context.Services.AddScoped<DomEventService>();
            context.Services.AddSingleton<UserConfig>(new SimulatedUserConfig
            {
                User = { Roles = [Roles.Admin] }
            });
            context.Services.AddSingleton<AuthenticationStateProvider>(new FlowSettingsPagesAuthStateProvider(Roles.Admin));
            return context;
        }

        private static BunitContext CreateUnnamedDuplicateResolverContext(out FlowServiceObjectsUnnamedDuplicateResolverApiConn apiConnection)
        {
            BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddLocalization();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            apiConnection = new FlowServiceObjectsUnnamedDuplicateResolverApiConn();
            context.Services.AddSingleton<ApiConnection>(apiConnection);
            context.Services.AddScoped<DomEventService>();
            context.Services.AddSingleton<UserConfig>(new SimulatedUserConfig
            {
                User = { Roles = [Roles.Admin] }
            });
            context.Services.AddSingleton<AuthenticationStateProvider>(new FlowSettingsPagesAuthStateProvider(Roles.Admin));
            return context;
        }

        private static BunitContext CreateNetworkDuplicateResolverContext(out FlowNetworkObjectsDuplicateResolverApiConn apiConnection)
        {
            BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddLocalization();
            context.Services.AddSingleton<IAuthorizationService>(new AllowAllAuthorizationService());
            apiConnection = new FlowNetworkObjectsDuplicateResolverApiConn();
            context.Services.AddSingleton<ApiConnection>(apiConnection);
            context.Services.AddScoped<DomEventService>();
            context.Services.AddSingleton<GlobalConfig>(new SimulatedGlobalConfig());
            context.Services.AddSingleton<UserConfig>(new SimulatedUserConfig
            {
                User = { Roles = [Roles.Admin] }
            });
            context.Services.AddSingleton<AuthenticationStateProvider>(new FlowSettingsPagesAuthStateProvider(Roles.Admin));
            return context;
        }

        private static BunitContext CreateNetworkObjectsContext(out FlowNetworkObjectsNamingApiConn apiConnection)
        {
            BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddLocalization();
            context.Services.AddSingleton<IAuthorizationService>(new AllowAllAuthorizationService());
            apiConnection = new FlowNetworkObjectsNamingApiConn();
            context.Services.AddSingleton<ApiConnection>(apiConnection);
            context.Services.AddScoped<DomEventService>();
            context.Services.AddSingleton<GlobalConfig>(new SimulatedGlobalConfig());
            context.Services.AddSingleton<UserConfig>(new SimulatedUserConfig
            {
                User = { Roles = [Roles.Admin] }
            });
            context.Services.AddSingleton<AuthenticationStateProvider>(new FlowSettingsPagesAuthStateProvider(Roles.Admin));
            return context;
        }

        private static void SeedTranslations()
        {
            foreach (string key in new[]
            {
                "network_groups",
                "service_objects",
                "service_groups",
                "time_objects",
                "duplicate_objects",
                "flow_object",
                "management",
                "objects",
                "actions",
                "id",
                "name",
                "state",
                "show_in_request_module",
                "details",
                "uid",
                "search_name",
                "custom_objects",
                "custom_network_objects",
                "custom_service_objects",
                "create_custom_flow_object",
                "create_custom_network_object",
                "create_custom_service_object",
                "flow_objects",
                "flow_network_objects",
                "flow_service_objects",
                "flow_network_groups",
                "flow_service_groups",
                "flow_time_objects",
                "edit_flow_object",
                "save",
                "cancel",
                "select",
                "no_duplicate_conflicts",
                "current",
                "type",
                "ip"
            })
            {
                SimulatedUserConfig.DummyTranslate.TryAdd(key, key);
            }
        }

        private static IRenderedComponent<TComponent> RenderPage<TComponent>(BunitContext context)
            where TComponent : Microsoft.AspNetCore.Components.IComponent
        {
            return context.Render<CascadingAuthenticationState>(parameters => parameters
                .AddChildContent<TComponent>())
                .FindComponent<TComponent>();
        }

        private static void SetMember(object instance, string memberName, object? value)
        {
            Type type = instance.GetType();
            PropertyInfo? property = type.GetProperty(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (property != null)
            {
                property.SetValue(instance, value);
                return;
            }

            FieldInfo? field = type.GetField(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(instance, value);
                return;
            }

            throw new MissingFieldException(type.FullName, memberName);
        }

        private sealed class FlowSettingsPagesAuthStateProvider(params string[] roles) : AuthenticationStateProvider
        {
            private readonly ClaimsPrincipal principal = new(new ClaimsIdentity(
                Array.ConvertAll(roles, role => new Claim(ClaimTypes.Role, role)),
                authenticationType: "Test",
                nameType: ClaimTypes.Name,
                roleType: ClaimTypes.Role));

            public override Task<AuthenticationState> GetAuthenticationStateAsync()
            {
                return Task.FromResult(new AuthenticationState(principal));
            }
        }
    }

    internal sealed class FlowNetworkObjectsNamingApiConn : SimulatedApiConnection
    {
        public List<string> Queries { get; } = [];
        public List<string> UpdatedFlowObjectNames { get; } = [];
        public int CustomObjectCandidateQueryCount { get; private set; }

        private readonly FlowNwObject flowNwObject = new()
        {
            Id = 100,
            Name = "",
            IpStart = "192.0.2.10/32",
            IpEnd = "192.0.2.20/32",
            Hash = "hash-100",
            State = FlowState.Requested,
            ShowInRequestModule = false,
            Objects = []
        };

        private readonly Management localManagement = new()
        {
            Id = 10,
            Name = "A Management",
            Objects =
            [
                new NetworkObject
                {
                    Id = 1,
                    Name = "",
                    IP = null!,
                    IpEnd = null!,
                    Uid = "local-1",
                    Active = true,
                    FlowNetworkObjectId = null,
                    FlowActive = false,
                    Type = new NetworkObjectType { Id = 1, Name = "host" }
                },
                new NetworkObject
                {
                    Id = 3,
                    Name = "Second Local Object",
                    IP = null!,
                    IpEnd = null!,
                    Uid = "local-2",
                    Active = true,
                    FlowNetworkObjectId = null,
                    FlowActive = false,
                    Type = new NetworkObjectType { Id = 1, Name = "host" }
                }
            ]
        };

        private readonly Management globalManagement = new()
        {
            Id = 20,
            Name = "Global Management",
            Objects =
            [
                new NetworkObject
                {
                    Id = 2,
                    Name = "Global Object Name",
                    IP = "",
                    IpEnd = "",
                    Uid = "global-1",
                    Active = true,
                    FlowNetworkObjectId = 100,
                    FlowActive = false,
                    Type = new NetworkObjectType { Id = 1, Name = "host" }
                }
            ]
        };

        public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
        {
            Queries.Add(query);
            if (query == FlowQueries.getFlowSelectableManagements)
            {
                return Task.FromResult((QueryResponseType)(object)new List<Management>
                {
                    localManagement,
                    globalManagement
                });
            }
            if (query == FlowQueries.getFlowNwObjectCatalog)
            {
                return Task.FromResult((QueryResponseType)(object)new List<FlowNwObject> { flowNwObject });
            }
            if (query == FlowQueries.getFlowAddressGroups)
            {
                return Task.FromResult((QueryResponseType)(object)new List<FlowNwGroup>());
            }
            if (query == FlowQueries.getFlowServiceObjects)
            {
                return Task.FromResult((QueryResponseType)(object)new List<FlowSvcObject>());
            }
            if (query == FlowQueries.getFlowServiceGroups)
            {
                return Task.FromResult((QueryResponseType)(object)new List<FlowSvcGroup>());
            }
            if (query == FlowQueries.getFlowTimeObjects)
            {
                return Task.FromResult((QueryResponseType)(object)new List<FlowTimeObject>());
            }
            if (query == FlowQueries.getFlowCustomObjectCandidates)
            {
                CustomObjectCandidateQueryCount++;
                return Task.FromResult((QueryResponseType)(object)new List<Management> { localManagement });
            }
            if (query == FlowQueries.getFlowCustomObjectNamingCandidates)
            {
                return Task.FromResult((QueryResponseType)(object)new List<Management>
                {
                    localManagement,
                    globalManagement
                });
            }
            if (query == FlowQueries.getFlowCustomServiceCandidates)
            {
                return Task.FromResult((QueryResponseType)(object)new List<Management>());
            }
            if (query == FlowQueries.getFlowCustomServiceNamingCandidates)
            {
                return Task.FromResult((QueryResponseType)(object)new List<Management>());
            }
            if (query == FlowQueries.getFlowCustomTimeObjectCandidates)
            {
                return Task.FromResult((QueryResponseType)(object)new List<Management>());
            }
            if (query == FlowQueries.getFlowCustomTimeObjectNamingCandidates)
            {
                return Task.FromResult((QueryResponseType)(object)new List<Management>());
            }
            if (query == FlowMutations.updateFlowNwObjects && typeof(QueryResponseType) == typeof(List<MutationResult>))
            {
                List<object> updates = GetAnonymousProperty<List<object>>(variables, "updates");
                foreach (object update in updates)
                {
                    object setObject = update.GetType().GetProperty("_set", BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase)?.GetValue(update)
                        ?? throw new InvalidOperationException("Missing _set payload.");
                    string name = (string)(setObject.GetType().GetProperty("name", BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase)?.GetValue(setObject)
                        ?? throw new InvalidOperationException("Missing name payload."));
                    UpdatedFlowObjectNames.Add(name);
                }

                return Task.FromResult((QueryResponseType)(object)new List<MutationResult>
                {
                    new()
                    {
                        AffectedRows = updates.Count
                    }
                });
            }
            if (query == ConfigQueries.upsertConfigItems)
            {
                return Task.FromResult((QueryResponseType)(object)new object());
            }

            throw new InvalidOperationException($"Unexpected query: {query}");
        }

        private static T GetAnonymousProperty<T>(object? variables, string propertyName)
        {
            if (variables == null)
            {
                throw new InvalidOperationException($"Missing variables for {propertyName}");
            }

            return (T)(variables.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase)?.GetValue(variables)
                ?? throw new InvalidOperationException($"Missing property {propertyName}"));
        }
    }

    internal sealed class FlowSettingsPagesTestApiConn : SimulatedApiConnection
    {
        private static readonly FlowSvcObject kFlowSvcObject = new()
        {
            Id = 100,
            Name = "Flow Service Object",
            PortStart = 80,
            PortEnd = 80,
            ProtoId = 6,
            State = FlowState.Requested,
            ShowInRequestModule = true
        };

        private static readonly FlowSvcGroup kFlowSvcGroup = new()
        {
            Id = 200,
            Name = "Flow Service Group",
            State = FlowState.Requested,
            ShowInRequestModule = true,
            SvcGroupMembers =
            [
                new FlowSvcGroupMember
                {
                    SvcObject = new FlowSvcObject
                    {
                        Id = 100,
                        PortStart = 80,
                        PortEnd = 80,
                        ProtoId = 6
                    }
                }
            ]
        };

        private static readonly FlowNwGroup kFlowNwGroup = new()
        {
            Id = 300,
            Name = "Flow Network Group",
            State = FlowState.Requested,
            ShowInRequestModule = true,
            NwGroupMembers =
            [
                new FlowNwGroupMember
                {
                    NwObject = new FlowNwObject
                    {
                        Id = 301,
                        IpStart = "10.0.0.1",
                        IpEnd = "10.0.0.1"
                    }
                },
                new FlowNwGroupMember
                {
                    NwObject = new FlowNwObject
                    {
                        Id = 302,
                        IpStart = "10.0.0.2",
                        IpEnd = "10.0.0.2"
                    }
                }
            ]
        };

        private static readonly FlowTimeObject kFlowTimeObject = new()
        {
            Id = 400,
            Name = "Flow Time Object",
            StartTime = new DateTime(2026, 5, 1, 8, 0, 0, DateTimeKind.Utc),
            EndTime = new DateTime(2026, 5, 1, 18, 0, 0, DateTimeKind.Utc),
            State = FlowState.Requested,
            ShowInRequestModule = true
        };

        private static readonly Management kManagement = new()
        {
            Id = 10,
            Name = "Management"
        };

        public List<string> Queries { get; } = [];
        public List<(long ServiceId, long FlowSvcgrpId, bool ActiveOnMgm)> ServiceGroupMappingCalls { get; } = [];
        public List<(long ObjectId, long FlowNwgrpId, bool ActiveOnMgm)> NetworkGroupMappingCalls { get; } = [];
        public List<(long TimeObjectId, long FlowTimeobjId, bool ActiveOnMgm)> TimeObjectMappingCalls { get; } = [];

        private static readonly List<IpProtocol> kIpProtocols =
        [
            new() { Id = 1, Name = "ICMP" },
            new() { Id = 6, Name = "TCP" },
            new() { Id = 17, Name = "UDP" }
        ];

        private static readonly Management kServiceManagement = new()
        {
            Id = 10,
            Name = "Management",
            Services =
            [
                new()
                {
                    Id = 11,
                    Name = "Service A",
                    Uid = "svc-a",
                    DestinationPort = 80,
                    ProtoId = 6,
                    FlowServiceObjectId = 100,
                    FlowServiceGroupId = 200,
                    FlowActive = false
                },
                new()
                {
                    Id = 12,
                    Name = "Service B",
                    Uid = "svc-b",
                    DestinationPort = 80,
                    ProtoId = 6,
                    FlowServiceObjectId = 100,
                    FlowServiceGroupId = 200,
                    FlowActive = false
                }
            ]
        };

        private static readonly Management kNetworkManagement = new()
        {
            Id = 10,
            Name = "Management",
            Objects =
            [
                new()
                {
                    Id = 21,
                    Name = "Object A",
                    Uid = "obj-a",
                    IP = "10.0.0.1/32",
                    FlowNetworkGroupId = 300,
                    FlowActive = false
                },
                new()
                {
                    Id = 22,
                    Name = "Object B",
                    Uid = "obj-b",
                    IP = "10.0.0.2/32",
                    FlowNetworkGroupId = 300,
                    FlowActive = false
                }
            ]
        };

        private static readonly Management kTimeManagement = new()
        {
            Id = 10,
            Name = "Management",
            TimeObjects =
            [
                new()
                {
                    Id = 31,
                    Name = "Time A",
                    Uid = "time-a",
                    StartTime = new DateTime(2026, 5, 1, 8, 0, 0, DateTimeKind.Utc),
                    EndTime = new DateTime(2026, 5, 1, 18, 0, 0, DateTimeKind.Utc),
                    FlowTimeObjectId = 400,
                    FlowActive = false
                },
                new()
                {
                    Id = 32,
                    Name = "Time B",
                    Uid = "time-b",
                    StartTime = new DateTime(2026, 5, 1, 9, 0, 0, DateTimeKind.Utc),
                    EndTime = new DateTime(2026, 5, 1, 19, 0, 0, DateTimeKind.Utc),
                    FlowTimeObjectId = 400,
                    FlowActive = false
                }
            ]
        };

        public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
        {
            Queries.Add(query);

            if (query == FlowMutations.upsertFlowSvcGroupMapping && typeof(QueryResponseType) == typeof(NetworkService))
            {
                long serviceId = GetAnonymousProperty<long>(variables, "svcId");
                long flowSvcgrpId = GetAnonymousProperty<long>(variables, "flowSvcgrpId");
                bool activeOnMgm = GetAnonymousProperty<bool>(variables, "activeOnMgm");
                ServiceGroupMappingCalls.Add((serviceId, flowSvcgrpId, activeOnMgm));
                return Task.FromResult((QueryResponseType)(object)new NetworkService
                {
                    Id = serviceId,
                    Name = serviceId == 11 ? "Service A" : "Service B",
                    Uid = serviceId == 11 ? "svc-a" : "svc-b",
                    DestinationPort = 80,
                    DestinationPortEnd = 80,
                    ProtoId = 6,
                    Active = true,
                    Removed = null,
                    FlowServiceGroupId = flowSvcgrpId,
                    FlowActive = activeOnMgm
                });
            }

            if (query == FlowMutations.upsertFlowNwGroupMapping && typeof(QueryResponseType) == typeof(NetworkObject))
            {
                long objectId = GetAnonymousProperty<long>(variables, "objId");
                long flowNwgrpId = GetAnonymousProperty<long>(variables, "flowNwgrpId");
                bool activeOnMgm = GetAnonymousProperty<bool>(variables, "activeOnMgm");
                NetworkGroupMappingCalls.Add((objectId, flowNwgrpId, activeOnMgm));
                return Task.FromResult((QueryResponseType)(object)new NetworkObject
                {
                    Id = objectId,
                    Name = objectId == 21 ? "Object A" : "Object B",
                    Uid = objectId == 21 ? "obj-a" : "obj-b",
                    IP = objectId == 21 ? "10.0.0.1/32" : "10.0.0.2/32",
                    IpEnd = "",
                    Active = true,
                    Removed = null,
                    Type = new NetworkObjectType { Id = 1, Name = "host" },
                    FlowNetworkGroupId = flowNwgrpId,
                    FlowActive = activeOnMgm
                });
            }

            if (query == FlowMutations.upsertFlowTimeObjectMapping && typeof(QueryResponseType) == typeof(TimeObject))
            {
                long timeObjectId = GetAnonymousProperty<long>(variables, "timeObjId");
                long flowTimeobjId = GetAnonymousProperty<long>(variables, "flowTimeobjId");
                bool activeOnMgm = GetAnonymousProperty<bool>(variables, "activeOnMgm");
                TimeObjectMappingCalls.Add((timeObjectId, flowTimeobjId, activeOnMgm));
                return Task.FromResult((QueryResponseType)(object)new TimeObject
                {
                    Id = timeObjectId,
                    Name = timeObjectId == 31 ? "Time A" : "Time B",
                    Uid = timeObjectId == 31 ? "time-a" : "time-b",
                    StartTime = timeObjectId == 31
                        ? new DateTime(2026, 5, 1, 8, 0, 0, DateTimeKind.Utc)
                        : new DateTime(2026, 5, 1, 9, 0, 0, DateTimeKind.Utc),
                    EndTime = timeObjectId == 31
                        ? new DateTime(2026, 5, 1, 18, 0, 0, DateTimeKind.Utc)
                        : new DateTime(2026, 5, 1, 19, 0, 0, DateTimeKind.Utc),
                    FlowTimeObjectId = flowTimeobjId,
                    FlowActive = activeOnMgm
                });
            }

            object result = query switch
            {
                string q when q == FlowQueries.getFlowServiceObjects => new List<FlowSvcObject> { kFlowSvcObject },
                string q when q == FlowQueries.getFlowServiceGroups => new List<FlowSvcGroup> { kFlowSvcGroup },
                string q when q == FlowQueries.getFlowAddressGroups => new List<FlowNwGroup> { kFlowNwGroup },
                string q when q == FlowQueries.getFlowTimeObjects => new List<FlowTimeObject> { kFlowTimeObject },
                string q when q == StmQueries.getIpProtocols => new List<IpProtocol>(kIpProtocols),
                string q when q == FlowQueries.getFlowSelectableManagements => new List<Management> { kManagement },
                string q when q == FlowQueries.getFlowCustomServiceCandidates || q == FlowQueries.getFlowCustomServiceNamingCandidates => new List<Management> { kServiceManagement },
                string q when q == FlowQueries.getFlowCustomObjectCandidates || q == FlowQueries.getFlowCustomObjectNamingCandidates => new List<Management> { kNetworkManagement },
                string q when q == FlowQueries.getFlowCustomTimeObjectCandidates || q == FlowQueries.getFlowCustomTimeObjectNamingCandidates => new List<Management> { kTimeManagement },
                _ => throw new InvalidOperationException($"Unexpected query: {query}")
            };

            return Task.FromResult((QueryResponseType)result);
        }

        private static T GetAnonymousProperty<T>(object? variables, string propertyName)
        {
            if (variables == null)
            {
                throw new InvalidOperationException($"Missing variables for {propertyName}");
            }

            return (T)(variables.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase)?.GetValue(variables)
                ?? throw new InvalidOperationException($"Missing property {propertyName}"));
        }
    }

    internal sealed class FlowServiceObjectsCustomCreateApiConn : SimulatedApiConnection
    {
        private static readonly List<IpProtocol> kIpProtocols =
        [
            new() { Id = 1, Name = "ICMP" },
            new() { Id = 6, Name = "TCP" },
            new() { Id = 17, Name = "UDP" }
        ];

        public List<string> Queries { get; } = [];
        public FlowSvcObjectInsert? InsertedServiceObject { get; private set; }
        public List<(long ServiceId, long FlowSvcobjId, bool ActiveOnMgm)> MappingCalls { get; } = [];

        private readonly FlowSvcObject flowSvcObject;

        private readonly Management managementOne = new()
        {
            Id = 10,
            Name = "Management",
            Services =
            [
                new()
                {
                    Id = 11,
                    Name = "Service A",
                    Uid = "svc-a",
                    DestinationPort = null,
                    DestinationPortEnd = null,
                    ProtoId = 6,
                    FlowServiceObjectId = null,
                    Type = new NetworkServiceType { Name = ServiceType.SimpleService },
                    FlowActive = false
                },
                new()
                {
                    Id = 12,
                    Name = "Service Group Candidate",
                    Uid = "svc-group",
                    Type = new NetworkServiceType { Name = ServiceType.Group },
                    FlowServiceGroupId = null,
                    FlowActive = false
                },
                new()
                {
                    Id = 13,
                    Name = "Mapped Service",
                    Uid = "svc-mapped",
                    DestinationPort = null,
                    DestinationPortEnd = null,
                    ProtoId = 6,
                    FlowServiceObjectId = 123,
                    Type = new NetworkServiceType { Name = ServiceType.SimpleService },
                    FlowActive = false
                },
                new()
                {
                    Id = 14,
                    Name = "Port Service",
                    Uid = "svc-port",
                    DestinationPort = 8080,
                    DestinationPortEnd = 8080,
                    ProtoId = 6,
                    FlowServiceObjectId = null,
                    Type = new NetworkServiceType { Name = ServiceType.SimpleService },
                    FlowActive = false
                }
            ]
        };

        private readonly Management managementTwo = new()
        {
            Id = 20,
            Name = "Management 2",
            Services =
            [
                new()
                {
                    Id = 21,
                    Name = "Service B",
                    Uid = "svc-b",
                    DestinationPort = null,
                    DestinationPortEnd = null,
                    ProtoId = 17,
                    FlowServiceObjectId = null,
                    Type = new NetworkServiceType { Name = ServiceType.SimpleService },
                    FlowActive = false
                }
            ]
        };

        public FlowServiceObjectsCustomCreateApiConn(FlowSvcObject? existingFlowSvcObject = null)
        {
            flowSvcObject = existingFlowSvcObject ?? new FlowSvcObject
            {
                Id = 100,
                Name = "Flow Service Object",
                PortStart = 8080,
                PortEnd = 8080,
                ProtoId = 6,
                Hash = FlowHashGenerator.GenerateSvcObjectHash(6, 8080, 8080),
                State = FlowState.Requested,
                ShowInRequestModule = true
            };
        }

        public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
        {
            Queries.Add(query);
            if (query == FlowQueries.getFlowServiceObjects)
            {
                return Task.FromResult((QueryResponseType)(object)new List<FlowSvcObject> { flowSvcObject });
            }
            if (query == FlowQueries.getFlowSelectableManagements)
            {
                return Task.FromResult((QueryResponseType)(object)new List<Management>
                {
                    new() { Id = 10, Name = "Management" },
                    new() { Id = 20, Name = "Management 2" }
                });
            }
            if (query == StmQueries.getIpProtocols)
            {
                return Task.FromResult((QueryResponseType)(object)new List<IpProtocol>(kIpProtocols));
            }
            if (query == FlowQueries.getFlowCustomServiceCandidates || query == FlowQueries.getFlowCustomServiceNamingCandidates)
            {
                return Task.FromResult((QueryResponseType)(object)new List<Management> { managementOne, managementTwo });
            }
            if (query == FlowQueries.insertFlowSvcObjects && typeof(QueryResponseType) == typeof(FlowSvcObjectInsertResult))
            {
                object?[] insertedObjects = GetAnonymousArray(variables, "objects");
                object? firstObject = insertedObjects.FirstOrDefault();
                InsertedServiceObject = new FlowSvcObjectInsert
                {
                    Name = GetAnonymousProperty<string>(firstObject, "Name"),
                    PortStart = GetAnonymousNullableProperty<int>(firstObject, "PortStart"),
                    PortEnd = GetAnonymousNullableProperty<int>(firstObject, "PortEnd"),
                    IpProtoId = GetAnonymousProperty<int>(firstObject, "IpProtoId"),
                    SvcObjHash = GetAnonymousProperty<string>(firstObject, "SvcObjHash"),
                    State = GetAnonymousProperty<string>(firstObject, "State"),
                    RemovedDate = null,
                    ShowInRequestModule = GetAnonymousProperty<bool>(firstObject, "ShowInRequestModule")
                };
                return Task.FromResult((QueryResponseType)(object)new FlowSvcObjectInsertResult
                {
                    Returning =
                    [
                        new FlowSvcObject
                        {
                            Id = 900,
                            Name = InsertedServiceObject.Name ?? "",
                            PortStart = InsertedServiceObject.PortStart,
                            PortEnd = InsertedServiceObject.PortEnd,
                            ProtoId = InsertedServiceObject.IpProtoId,
                            Hash = InsertedServiceObject.SvcObjHash ?? "",
                            State = InsertedServiceObject.State ?? FlowState.Implemented,
                            ShowInRequestModule = InsertedServiceObject.ShowInRequestModule
                        }
                    ]
                });
            }
            if (query == FlowMutations.upsertFlowSvcObjectMapping && typeof(QueryResponseType) == typeof(NetworkService))
            {
                long serviceId = GetAnonymousProperty<long>(variables, "svcId");
                long flowSvcobjId = GetAnonymousProperty<long>(variables, "flowSvcobjId");
                bool activeOnMgm = GetAnonymousProperty<bool>(variables, "activeOnMgm");
                MappingCalls.Add((serviceId, flowSvcobjId, activeOnMgm));
                return Task.FromResult((QueryResponseType)(object)new NetworkService
                {
                    Id = serviceId,
                    Name = serviceId == 11 ? "Service A" : "Service B",
                    Uid = serviceId == 11 ? "svc-a" : "svc-b",
                    DestinationPort = null,
                    DestinationPortEnd = null,
                    ProtoId = serviceId == 11 ? 6 : 17,
                    Active = true,
                    Removed = null,
                    FlowServiceObjectId = flowSvcobjId,
                    FlowActive = activeOnMgm
                });
            }
            throw new InvalidOperationException($"Unexpected query: {query}");
        }

        private static T GetAnonymousProperty<T>(object? variables, string propertyName)
        {
            if (variables == null)
            {
                throw new InvalidOperationException($"Missing variables for {propertyName}");
            }

            return (T)(variables.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase)?.GetValue(variables)
                ?? throw new InvalidOperationException($"Missing property {propertyName}"));
        }

        private static T? GetAnonymousNullableProperty<T>(object? variables, string propertyName)
            where T : struct
        {
            if (variables == null)
            {
                throw new InvalidOperationException($"Missing variables for {propertyName}");
            }

            object? value = variables.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase)?.GetValue(variables);
            return value == null ? null : (T)value;
        }

        private static object?[] GetAnonymousArray(object? variables, string propertyName)
        {
            if (variables == null)
            {
                throw new InvalidOperationException($"Missing variables for {propertyName}");
            }

            return (object?[])(variables.GetType().GetProperty(propertyName)?.GetValue(variables)
                ?? throw new InvalidOperationException($"Missing property {propertyName}"));
        }
    }

    internal sealed class FlowServiceObjectsProtocolOnlyApiConn : SimulatedApiConnection
    {
        private static readonly List<IpProtocol> kIpProtocols =
        [
            new() { Id = 1, Name = "ICMP" },
            new() { Id = 6, Name = "TCP" },
            new() { Id = 17, Name = "UDP" }
        ];

        public List<string> Queries { get; } = [];
        public FlowSvcObjectInsert? InsertedServiceObject { get; private set; }
        public List<(long ServiceId, long FlowSvcobjId, bool ActiveOnMgm)> MappingCalls { get; } = [];

        private readonly FlowSvcObject flowSvcObject = new()
        {
            Id = 100,
            Name = "Flow Service Object",
            State = FlowState.Requested,
            ShowInRequestModule = true
        };

        private readonly Management management = new()
        {
            Id = 10,
            Name = "Management",
            Services =
            [
                new()
                {
                    Id = 11,
                    Name = "Protocol Only Service",
                    Uid = "svc-proto-only",
                    DestinationPort = null,
                    DestinationPortEnd = null,
                    ProtoId = 1,
                    FlowServiceObjectId = null,
                    FlowActive = false
                }
            ]
        };

        public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
        {
            Queries.Add(query);
            if (query == FlowQueries.getFlowServiceObjects)
            {
                return Task.FromResult((QueryResponseType)(object)new List<FlowSvcObject> { flowSvcObject });
            }
            if (query == FlowQueries.getFlowSelectableManagements)
            {
                return Task.FromResult((QueryResponseType)(object)new List<Management> { new() { Id = 10, Name = "Management" } });
            }
            if (query == StmQueries.getIpProtocols)
            {
                return Task.FromResult((QueryResponseType)(object)new List<IpProtocol>(kIpProtocols));
            }
            if (query == FlowQueries.getFlowCustomServiceCandidates || query == FlowQueries.getFlowCustomServiceNamingCandidates)
            {
                return Task.FromResult((QueryResponseType)(object)new List<Management> { management });
            }
            if (query == FlowQueries.insertFlowSvcObjects && typeof(QueryResponseType) == typeof(FlowSvcObjectInsertResult))
            {
                object?[] insertedObjects = GetAnonymousArray(variables, "objects");
                object? firstObject = insertedObjects.FirstOrDefault();
                InsertedServiceObject = new FlowSvcObjectInsert
                {
                    Name = GetAnonymousProperty<string>(firstObject, "Name"),
                    PortStart = GetAnonymousNullableProperty<int>(firstObject, "PortStart"),
                    PortEnd = GetAnonymousNullableProperty<int>(firstObject, "PortEnd"),
                    IpProtoId = GetAnonymousProperty<int>(firstObject, "IpProtoId"),
                    SvcObjHash = GetAnonymousProperty<string>(firstObject, "SvcObjHash"),
                    State = GetAnonymousProperty<string>(firstObject, "State"),
                    RemovedDate = null,
                    ShowInRequestModule = GetAnonymousProperty<bool>(firstObject, "ShowInRequestModule")
                };
                return Task.FromResult((QueryResponseType)(object)new FlowSvcObjectInsertResult
                {
                    Returning =
                    [
                        new FlowSvcObject
                        {
                            Id = 900,
                            Name = InsertedServiceObject.Name ?? "",
                            PortStart = InsertedServiceObject.PortStart,
                            PortEnd = InsertedServiceObject.PortEnd,
                            ProtoId = InsertedServiceObject.IpProtoId,
                            Hash = InsertedServiceObject.SvcObjHash ?? "",
                            State = InsertedServiceObject.State ?? FlowState.Implemented,
                            ShowInRequestModule = InsertedServiceObject.ShowInRequestModule
                        }
                    ]
                });
            }
            if (query == FlowMutations.upsertFlowSvcObjectMapping && typeof(QueryResponseType) == typeof(NetworkService))
            {
                long serviceId = GetAnonymousProperty<long>(variables, "svcId");
                long flowSvcobjId = GetAnonymousProperty<long>(variables, "flowSvcobjId");
                bool activeOnMgm = GetAnonymousProperty<bool>(variables, "activeOnMgm");
                MappingCalls.Add((serviceId, flowSvcobjId, activeOnMgm));
                return Task.FromResult((QueryResponseType)(object)new NetworkService
                {
                    Id = serviceId,
                    Name = "Protocol Only Service",
                    Uid = "svc-proto-only",
                    DestinationPort = null,
                    DestinationPortEnd = null,
                    Active = true,
                    Removed = null,
                    FlowServiceObjectId = flowSvcobjId,
                    FlowActive = activeOnMgm
                });
            }
            throw new InvalidOperationException($"Unexpected query: {query}");
        }

        private static T GetAnonymousProperty<T>(object? variables, string propertyName)
        {
            if (variables == null)
            {
                throw new InvalidOperationException($"Missing variables for {propertyName}");
            }

            return (T)(variables.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase)?.GetValue(variables)
                ?? throw new InvalidOperationException($"Missing property {propertyName}"));
        }

        private static T? GetAnonymousNullableProperty<T>(object? variables, string propertyName)
            where T : struct
        {
            if (variables == null)
            {
                throw new InvalidOperationException($"Missing variables for {propertyName}");
            }

            object? value = variables.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase)?.GetValue(variables);
            return value == null ? null : (T)value;
        }

        private static object?[] GetAnonymousArray(object? variables, string propertyName)
        {
            if (variables == null)
            {
                throw new InvalidOperationException($"Missing variables for {propertyName}");
            }

            return (object?[])(variables.GetType().GetProperty(propertyName)?.GetValue(variables)
                ?? throw new InvalidOperationException($"Missing property {propertyName}"));
        }
    }

    internal sealed class FlowServiceObjectsDuplicateResolverApiConn : SimulatedApiConnection
    {
        private static readonly List<IpProtocol> kIpProtocols =
        [
            new() { Id = 1, Name = "ICMP" },
            new() { Id = 6, Name = "TCP" },
            new() { Id = 17, Name = "UDP" }
        ];

        public List<string> Queries { get; } = [];
        public List<(long ServiceId, long FlowSvcobjId, bool ActiveOnMgm)> MappingCalls { get; } = [];
        public FlowSvcObjectInsert? InsertedServiceObject { get; private set; }

        private readonly FlowSvcObject flowSvcObject = new()
        {
            Id = 100,
            Name = "Flow Service Object",
            PortStart = 80,
            PortEnd = 80,
            ProtoId = 6,
            State = FlowState.Requested,
            ShowInRequestModule = true
        };

        private readonly Management management = new()
        {
            Id = 10,
            Name = "Management",
            Services =
            [
                new()
                {
                    Id = 11,
                    Name = "Service A",
                    Uid = "svc-a",
                    DestinationPort = 80,
                    DestinationPortEnd = 80,
                    ProtoId = 6,
                    FlowServiceObjectId = 100,
                    FlowActive = false
                },
                new()
                {
                    Id = 12,
                    Name = "Service B",
                    Uid = "svc-b",
                    DestinationPort = 80,
                    DestinationPortEnd = 80,
                    ProtoId = 6,
                    FlowServiceObjectId = 100,
                    FlowActive = false
                }
            ]
        };

        public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
        {
            Queries.Add(query);
            if (query == FlowQueries.getFlowServiceObjects)
            {
                return Task.FromResult((QueryResponseType)(object)new List<FlowSvcObject> { flowSvcObject });
            }
            if (query == FlowQueries.getFlowSelectableManagements)
            {
                return Task.FromResult((QueryResponseType)(object)new List<Management> { new() { Id = 10, Name = "Management" } });
            }
            if (query == StmQueries.getIpProtocols)
            {
                return Task.FromResult((QueryResponseType)(object)new List<IpProtocol>(kIpProtocols));
            }
            if (query == FlowQueries.getFlowCustomServiceCandidates || query == FlowQueries.getFlowCustomServiceNamingCandidates)
            {
                return Task.FromResult((QueryResponseType)(object)new List<Management> { management });
            }
            if (query == FlowQueries.insertFlowSvcObjects && typeof(QueryResponseType) == typeof(FlowSvcObjectInsertResult))
            {
                object?[] insertedObjects = GetAnonymousArray(variables, "objects");
                object? firstObject = insertedObjects.FirstOrDefault();
                InsertedServiceObject = new FlowSvcObjectInsert
                {
                    Name = GetAnonymousProperty<string>(firstObject, "Name"),
                    PortStart = GetAnonymousNullableProperty<int>(firstObject, "PortStart"),
                    PortEnd = GetAnonymousNullableProperty<int>(firstObject, "PortEnd"),
                    IpProtoId = GetAnonymousProperty<int>(firstObject, "IpProtoId"),
                    SvcObjHash = GetAnonymousProperty<string>(firstObject, "SvcObjHash"),
                    State = GetAnonymousProperty<string>(firstObject, "State"),
                    RemovedDate = null,
                    ShowInRequestModule = GetAnonymousProperty<bool>(firstObject, "ShowInRequestModule")
                };
                return Task.FromResult((QueryResponseType)(object)new FlowSvcObjectInsertResult
                {
                    Returning =
                    [
                        new FlowSvcObject
                        {
                            Id = 900,
                            Name = InsertedServiceObject.Name ?? "",
                            PortStart = InsertedServiceObject.PortStart,
                            PortEnd = InsertedServiceObject.PortEnd,
                            ProtoId = InsertedServiceObject.IpProtoId,
                            Hash = InsertedServiceObject.SvcObjHash ?? "",
                            State = InsertedServiceObject.State ?? FlowState.Implemented,
                            ShowInRequestModule = InsertedServiceObject.ShowInRequestModule
                        }
                    ]
                });
            }
            if (query == FlowMutations.upsertFlowSvcObjectMapping && typeof(QueryResponseType) == typeof(NetworkService))
            {
                long serviceId = GetAnonymousProperty<long>(variables, "svcId");
                long flowSvcobjId = GetAnonymousProperty<long>(variables, "flowSvcobjId");
                bool activeOnMgm = GetAnonymousProperty<bool>(variables, "activeOnMgm");
                MappingCalls.Add((serviceId, flowSvcobjId, activeOnMgm));
                return Task.FromResult((QueryResponseType)(object)new NetworkService
                {
                    Id = serviceId,
                    Name = serviceId == 11 ? "Service A" : "Service B",
                    Uid = serviceId == 11 ? "svc-a" : "svc-b",
                    DestinationPort = 80,
                    DestinationPortEnd = 80,
                    Active = true,
                    Removed = null,
                    FlowServiceObjectId = flowSvcobjId,
                    FlowActive = activeOnMgm
                });
            }

            throw new InvalidOperationException($"Unexpected query: {query}");
        }

        private static T GetAnonymousProperty<T>(object? variables, string propertyName)
        {
            if (variables == null)
            {
                throw new InvalidOperationException($"Missing variables for {propertyName}");
            }

            return (T)(variables.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase)?.GetValue(variables)
                ?? throw new InvalidOperationException($"Missing property {propertyName}"));
        }

        private static T? GetAnonymousNullableProperty<T>(object? variables, string propertyName)
            where T : struct
        {
            if (variables == null)
            {
                throw new InvalidOperationException($"Missing variables for {propertyName}");
            }

            object? value = variables.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase)?.GetValue(variables);
            return value == null ? null : (T)value;
        }

        private static object?[] GetAnonymousArray(object? variables, string propertyName)
        {
            if (variables == null)
            {
                throw new InvalidOperationException($"Missing variables for {propertyName}");
            }

            return (object?[])(variables.GetType().GetProperty(propertyName)?.GetValue(variables)
                ?? throw new InvalidOperationException($"Missing property {propertyName}"));
        }
    }

    internal sealed class FlowServiceObjectsUnnamedDuplicateResolverApiConn : SimulatedApiConnection
    {
        private static readonly List<IpProtocol> kIpProtocols =
        [
            new() { Id = 1, Name = "ICMP" },
            new() { Id = 6, Name = "TCP" },
            new() { Id = 17, Name = "UDP" }
        ];

        public List<string> Queries { get; } = [];
        public List<(long ServiceId, long FlowSvcobjId, bool ActiveOnMgm)> MappingCalls { get; } = [];
        public string? UpdatedFlowObjectName { get; private set; }

        private readonly FlowSvcObject flowSvcObject = new()
        {
            Id = 100,
            Name = "",
            PortStart = 80,
            PortEnd = 80,
            ProtoId = 6,
            State = FlowState.Requested,
            ShowInRequestModule = true
        };

        private readonly Management management = new()
        {
            Id = 10,
            Name = "Management",
            Services =
            [
                new()
                {
                    Id = 11,
                    Name = "Service A",
                    Uid = "svc-a",
                    DestinationPort = 80,
                    DestinationPortEnd = 80,
                    ProtoId = 6,
                    FlowServiceObjectId = 100,
                    FlowActive = false
                },
                new()
                {
                    Id = 12,
                    Name = "Service B",
                    Uid = "svc-b",
                    DestinationPort = 80,
                    DestinationPortEnd = 80,
                    ProtoId = 6,
                    FlowServiceObjectId = 100,
                    FlowActive = false
                }
            ]
        };

        public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
        {
            Queries.Add(query);
            if (query == FlowQueries.getFlowServiceObjects)
            {
                return Task.FromResult((QueryResponseType)(object)new List<FlowSvcObject> { flowSvcObject });
            }
            if (query == FlowQueries.getFlowSelectableManagements)
            {
                return Task.FromResult((QueryResponseType)(object)new List<Management> { new() { Id = 10, Name = "Management" } });
            }
            if (query == StmQueries.getIpProtocols)
            {
                return Task.FromResult((QueryResponseType)(object)new List<IpProtocol>(kIpProtocols));
            }
            if (query == FlowQueries.getFlowCustomServiceCandidates || query == FlowQueries.getFlowCustomServiceNamingCandidates)
            {
                return Task.FromResult((QueryResponseType)(object)new List<Management> { management });
            }
            if (query == FlowMutations.upsertFlowSvcObjectMapping && typeof(QueryResponseType) == typeof(NetworkService))
            {
                long serviceId = GetAnonymousProperty<long>(variables, "svcId");
                long flowSvcobjId = GetAnonymousProperty<long>(variables, "flowSvcobjId");
                bool activeOnMgm = GetAnonymousProperty<bool>(variables, "activeOnMgm");
                MappingCalls.Add((serviceId, flowSvcobjId, activeOnMgm));
                return Task.FromResult((QueryResponseType)(object)new NetworkService
                {
                    Id = serviceId,
                    Name = serviceId == 11 ? "Service A" : "Service B",
                    Uid = serviceId == 11 ? "svc-a" : "svc-b",
                    DestinationPort = 80,
                    DestinationPortEnd = 80,
                    Active = true,
                    Removed = null,
                    FlowServiceObjectId = flowSvcobjId,
                    FlowActive = activeOnMgm
                });
            }
            if (query == FlowMutations.updateFlowSvcObject && typeof(QueryResponseType) == typeof(FlowSvcObject))
            {
                UpdatedFlowObjectName = GetAnonymousProperty<string>(variables, "name");
                flowSvcObject.Name = UpdatedFlowObjectName;
                return Task.FromResult((QueryResponseType)(object)new FlowSvcObject
                {
                    Id = flowSvcObject.Id,
                    Name = flowSvcObject.Name,
                    PortStart = flowSvcObject.PortStart,
                    PortEnd = flowSvcObject.PortEnd,
                    ProtoId = flowSvcObject.ProtoId,
                    Hash = flowSvcObject.Hash,
                    State = flowSvcObject.State,
                    RemovedDate = flowSvcObject.RemovedDate,
                    ShowInRequestModule = flowSvcObject.ShowInRequestModule
                });
            }

            throw new InvalidOperationException($"Unexpected query: {query}");
        }

        private static T GetAnonymousProperty<T>(object? variables, string propertyName)
        {
            if (variables == null)
            {
                throw new InvalidOperationException($"Missing variables for {propertyName}");
            }

            return (T)(variables.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase)?.GetValue(variables)
                ?? throw new InvalidOperationException($"Missing property {propertyName}"));
        }
    }

    internal sealed class FlowNetworkObjectsDuplicateResolverApiConn : SimulatedApiConnection
    {
        public List<string> Queries { get; } = [];
        public List<(long ObjectId, long FlowNwobjId, bool ActiveOnMgm)> MappingCalls { get; } = [];
        private readonly Management customObjectManagement;

        private readonly FlowNwObject flowNwObject = new()
        {
            Id = 100,
            Name = "Flow Object",
            IpStart = null,
            IpEnd = null,
            Hash = "hash-100",
            State = FlowState.Implemented,
            ShowInRequestModule = false
        };

        private readonly Management management = new()
        {
            Id = 10,
            Name = "Management",
            Objects =
            [
                new NetworkObject
                {
                    Id = 11,
                    Name = "Object A",
                    IP = "",
                    IpEnd = "",
                    Uid = "obj-a",
                    Active = true,
                    Type = new NetworkObjectType { Id = 1, Name = "host" },
                    FlowNetworkObjectId = 100,
                    FlowActive = false
                },
                new NetworkObject
                {
                    Id = 12,
                    Name = "Object B",
                    IP = "",
                    IpEnd = "",
                    Uid = "obj-b",
                    Active = true,
                    Type = new NetworkObjectType { Id = 1, Name = "host" },
                    FlowNetworkObjectId = 100,
                    FlowActive = false
                },
                new NetworkObject
                {
                    Id = 13,
                    Name = "Object C",
                    IP = null!,
                    IpEnd = null!,
                    Uid = "obj-c",
                    Active = true,
                    Type = new NetworkObjectType { Id = 1, Name = "host" },
                    FlowNetworkObjectId = null,
                    FlowActive = false
                },
                new NetworkObject
                {
                    Id = 14,
                    Name = "Object D",
                    IP = null!,
                    IpEnd = null!,
                    Uid = "obj-d",
                    Active = true,
                    Type = new NetworkObjectType { Id = 1, Name = "host" },
                    FlowNetworkObjectId = null,
                    FlowActive = false
                },
                new NetworkObject
                {
                    Id = 15,
                    Name = "Group Candidate",
                    IP = null!,
                    IpEnd = null!,
                    Uid = "group-candidate",
                    Active = true,
                    Type = new NetworkObjectType { Id = 2, Name = ObjectType.Group },
                    FlowNetworkObjectId = null,
                    FlowActive = false
                },
                new NetworkObject
                {
                    Id = 16,
                    Name = "Technical Candidate",
                    IP = "192.0.2.16",
                    IpEnd = "",
                    Uid = "technical-candidate",
                    Active = true,
                    Type = new NetworkObjectType { Id = 1, Name = "host" },
                    FlowNetworkObjectId = null,
                    FlowActive = false
                }
            ]
        };

        public FlowNetworkObjectsDuplicateResolverApiConn()
        {
            customObjectManagement = new Management
            {
                Id = management.Id,
                Name = management.Name,
                Objects = management.Objects.Where(nwObject => nwObject.FlowNetworkObjectId == null).ToArray()
            };
        }

        public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
        {
            Queries.Add(query);
            if (query == FlowQueries.getFlowSelectableManagements)
            {
                return Task.FromResult((QueryResponseType)(object)new List<Management> { new() { Id = 10, Name = "Management" } });
            }
            if (query == FlowQueries.getFlowNwObjectCatalog)
            {
                return Task.FromResult((QueryResponseType)(object)new List<FlowNwObject> { flowNwObject });
            }
            if (query == FlowQueries.getFlowCustomObjectCandidates)
            {
                return Task.FromResult((QueryResponseType)(object)new List<Management> { customObjectManagement });
            }
            if (query == FlowQueries.getFlowCustomObjectNamingCandidates)
            {
                return Task.FromResult((QueryResponseType)(object)new List<Management> { management });
            }
            if (query == FlowMutations.upsertFlowNwObjectMapping && typeof(QueryResponseType) == typeof(NetworkObject))
            {
                long objectId = GetAnonymousProperty<long>(variables, "objId");
                long flowNwobjId = GetAnonymousProperty<long>(variables, "flowNwobjId");
                bool activeOnMgm = GetAnonymousProperty<bool>(variables, "activeOnMgm");
                MappingCalls.Add((objectId, flowNwobjId, activeOnMgm));
                return Task.FromResult((QueryResponseType)(object)new NetworkObject
                {
                    Id = objectId,
                    Name = objectId == 11 ? "Object A" : "Object B",
                    IP = "",
                    IpEnd = "",
                    Uid = objectId == 11 ? "obj-a" : "obj-b",
                    Active = true,
                    Removed = null,
                    FlowNetworkObjectId = flowNwobjId,
                    FlowActive = activeOnMgm
                });
            }

            throw new InvalidOperationException($"Unexpected query: {query}");
        }

        private static T GetAnonymousProperty<T>(object? variables, string propertyName)
        {
            if (variables == null)
            {
                throw new InvalidOperationException($"Missing variables for {propertyName}");
            }

            return (T)(variables.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase)?.GetValue(variables)
                ?? throw new InvalidOperationException($"Missing property {propertyName}"));
        }
    }
}
