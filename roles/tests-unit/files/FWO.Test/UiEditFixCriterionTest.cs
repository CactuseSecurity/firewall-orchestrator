using Bunit;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api;
using FWO.Data;
using FWO.Ui.Pages.Settings;
using FWO.Ui.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Reflection;

namespace FWO.Test
{
    [TestFixture]
    internal class UiEditFixCriterionTest
    {
        private static MethodInfo GetPrivateMethod(string name)
        {
            return typeof(EditFixCriterion).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance) ??
                throw new MissingMethodException(typeof(EditFixCriterion).FullName, name);
        }

        private static void SetPrivateField(EditFixCriterion component, string fieldName, object? value)
        {
            FieldInfo? field = typeof(EditFixCriterion).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
            {
                throw new MissingFieldException(typeof(EditFixCriterion).FullName, fieldName);
            }

            field.SetValue(component, value);
        }

        [SetUp]
        public void SetUp()
        {
            SimulatedUserConfig.DummyTranslate["edit_fix_crit"] = "Edit fixed criterion";
            SimulatedUserConfig.DummyTranslate["H5512"] = "Criterion help";
            SimulatedUserConfig.DummyTranslate["content_mode"] = "Content mode";
            SimulatedUserConfig.DummyTranslate["elements"] = "Elements";
            SimulatedUserConfig.DummyTranslate["content"] = "Content";
            SimulatedUserConfig.DummyTranslate["criterion_hint_minimum_cidr_length"] = "CIDR hint";
            SimulatedUserConfig.DummyTranslate["criterion_hint_forbidden_service_uid"] = "UID hint";
            SimulatedUserConfig.DummyTranslate["criterion_hint_forbidden_service_protocol_port"] = "Port/protocol hint";
            SimulatedUserConfig.DummyTranslate["criterion_hint_forbid_source_name"] = "Source name hint";
            SimulatedUserConfig.DummyTranslate["criterion_hint_forbid_destination_name"] = "Destination name hint";
            SimulatedUserConfig.DummyTranslate["criterion_error_minimum_cidr_length"] = "Invalid CIDR";
            SimulatedUserConfig.DummyTranslate["criterion_error_forbidden_service_protocol_port"] = "Invalid port protocol";
            SimulatedUserConfig.DummyTranslate["criterion_error_non_empty_content"] = "Content required";
            SimulatedUserConfig.DummyTranslate["criterion_no_content_required"] = "No content required";
            SimulatedUserConfig.DummyTranslate["port_protocol"] = "Port/Protocol";
        }

        [Test]
        public async Task Save_ForbiddenServiceWithPendingProtocolPort_AddsPendingElement()
        {
            await using Bunit.TestContext context = CreateContext(out EditFixCriterionTestApiConn apiConn);

            ComplianceCriterion criterion = new()
            {
                Name = "Forbidden ports",
                CriterionType = nameof(CriterionType.ForbiddenService)
            };

            EditFixCriterion editFixCriterion = RenderComponent(context, criterion, addMode: true).FindComponent<EditFixCriterion>().Instance;

            SetPrivateField(editFixCriterion, "SelectedForbiddenServiceMode", Enum.Parse(GetPrivateNestedType("ForbiddenServiceInputMode"), "ProtocolPort"));
            SetPrivateField(editFixCriterion, "ActForbiddenPortStart", 443);
            SetPrivateField(editFixCriterion, "ActForbiddenPortEnd", 443);
            SetPrivateField(editFixCriterion, "ActForbiddenProtocolText", "TCP");

            Task saveTask = (Task)GetPrivateMethod("Save").Invoke(editFixCriterion, null)!;
            await saveTask;

            Assert.That(apiConn.AddCriterionCalls, Is.EqualTo(1));
            Assert.That(apiConn.LastAddedCriterionContent, Is.EqualTo("443/TCP"));
            Assert.That(criterion.Content, Is.EqualTo("443/TCP"));
        }

        [Test]
        public async Task Save_ForbidSourceNameWithEmptyContent_ShowsValidationError()
        {
            List<(Exception? Exception, string Title, string Message, bool IsError)> messages = [];
            await using Bunit.TestContext context = CreateContext(out EditFixCriterionTestApiConn apiConn);

            ComplianceCriterion criterion = new()
            {
                Name = "No source fragment",
                CriterionType = nameof(CriterionType.ForbidZonesAsSource)
            };

            EditFixCriterion editFixCriterion = RenderComponent(context, criterion, addMode: true, messages).FindComponent<EditFixCriterion>().Instance;
            SetPrivateField(editFixCriterion, "ActContent", "   ");

            Task saveTask = (Task)GetPrivateMethod("Save").Invoke(editFixCriterion, null)!;
            await saveTask;

            Assert.That(apiConn.AddCriterionCalls, Is.EqualTo(0));
            Assert.That(messages, Has.Count.EqualTo(1));
            Assert.That(messages[0].Message, Is.EqualTo("Content required"));
        }

        [Test]
        public async Task Save_ForbidDestinationNameWithContent_PersistsTrimmedContent()
        {
            await using Bunit.TestContext context = CreateContext(out EditFixCriterionTestApiConn apiConn);

            ComplianceCriterion criterion = new()
            {
                Name = "Destination fragment",
                CriterionType = nameof(CriterionType.ForbidZonesAsDestination)
            };

            EditFixCriterion editFixCriterion = RenderComponent(context, criterion, addMode: true).FindComponent<EditFixCriterion>().Instance;
            SetPrivateField(editFixCriterion, "ActContent", "  PARTNER  ");

            Task saveTask = (Task)GetPrivateMethod("Save").Invoke(editFixCriterion, null)!;
            await saveTask;

            Assert.That(apiConn.LastAddedCriterionContent, Is.EqualTo("PARTNER"));
            Assert.That(criterion.Content, Is.EqualTo("PARTNER"));
        }

        [Test]
        public async Task Save_EditMode_ReLinksPoliciesToReplacementCriterion()
        {
            await using Bunit.TestContext context = CreateContext(out EditFixCriterionTestApiConn apiConn);

            ComplianceCriterion criterion = new()
            {
                Id = 8,
                Name = "Existing CIDR criterion",
                CriterionType = nameof(CriterionType.MinimumCIDRLength),
                Content = "24"
            };
            List<ComplianceCriterion> criteria = [criterion];

            EditFixCriterion editFixCriterion = RenderComponent(context, criterion, addMode: false, criteriaList: criteria).FindComponent<EditFixCriterion>().Instance;
            SetPrivateField(editFixCriterion, "ActContent", "26");

            Task saveTask = (Task)GetPrivateMethod("Save").Invoke(editFixCriterion, null)!;
            await saveTask;

            Assert.That(apiConn.RemoveCriterionCalls, Is.EqualTo(1));
            Assert.That(apiConn.AddCriterionCalls, Is.EqualTo(1));
            Assert.That(apiConn.RemoveCritFromPolicyCalls, Is.EqualTo(1));
            Assert.That(apiConn.AddCritToPolicyCalls, Is.EqualTo(1));
            Assert.That(criterion.Id, Is.EqualTo(99));
            Assert.That(criterion.Content, Is.EqualTo("26"));
        }

        private static Type GetPrivateNestedType(string name)
        {
            return typeof(EditFixCriterion).GetNestedType(name, BindingFlags.NonPublic) ??
                throw new MissingMemberException(typeof(EditFixCriterion).FullName, name);
        }

        private static Bunit.TestContext CreateContext(out EditFixCriterionTestApiConn apiConn)
        {
            Bunit.TestContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<AuthenticationStateProvider>(new TestAuthStateProvider());
            apiConn = new EditFixCriterionTestApiConn();
            context.Services.AddSingleton<ApiConnection>(apiConn);
            context.Services.AddSingleton<DomEventService>();
            context.Services.AddSingleton<UserConfig>(new SimulatedUserConfig());
            return context;
        }

        private static IRenderedComponent<CascadingAuthenticationState> RenderComponent(
            Bunit.TestContext context,
            ComplianceCriterion criterion,
            bool addMode,
            List<(Exception? Exception, string Title, string Message, bool IsError)>? messages = null,
            List<ComplianceCriterion>? criteriaList = null)
        {
            Action<Exception?, string, string, bool> displayMessageInUi = (exception, title, message, isError) =>
            {
                messages?.Add((exception, title, message, isError));
            };

            return context.Render<CascadingAuthenticationState>(parameters => parameters
                .AddChildContent<CascadingValue<Action<Exception?, string, string, bool>>>(child => child
                    .Add(p => p.Value, displayMessageInUi)
                    .AddChildContent<EditFixCriterion>(component => component
                        .Add(p => p.Display, true)
                        .Add(p => p.AddMode, addMode)
                        .Add(p => p.SelectedCriterion, criterion)
                        .Add(p => p.CriteriaList, criteriaList ?? []))));
        }
    }

    internal sealed class EditFixCriterionTestApiConn : SimulatedApiConnection
    {
        public int AddCriterionCalls { get; private set; }
        public int RemoveCriterionCalls { get; private set; }
        public int RemoveCritFromPolicyCalls { get; private set; }
        public int AddCritToPolicyCalls { get; private set; }
        public string? LastAddedCriterionContent { get; private set; }

        public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, FWO.Api.Client.QueryChunkingOptions? chunkingOptions = null)
        {
            Type responseType = typeof(QueryResponseType);

            if (responseType == typeof(ReturnIdWrapper) && query == ComplianceQueries.addCriterion)
            {
                AddCriterionCalls++;
                LastAddedCriterionContent = variables?.GetType().GetProperty("content")?.GetValue(variables)?.ToString();
                return Task.FromResult((QueryResponseType)(object)new ReturnIdWrapper { ReturnIds = [new ReturnId { InsertedId = 99 }] });
            }

            if (responseType == typeof(ReturnId))
            {
                if (query == ComplianceQueries.removeCriterion)
                {
                    RemoveCriterionCalls++;
                }
                else if (query == ComplianceQueries.removeCritFromPolicy)
                {
                    RemoveCritFromPolicyCalls++;
                }
                else if (query == ComplianceQueries.addCritToPolicy)
                {
                    AddCritToPolicyCalls++;
                }

                return Task.FromResult((QueryResponseType)(object)new ReturnId());
            }

            if (responseType == typeof(List<LinkedPolicy>) && query == ComplianceQueries.getPolicyIdsForCrit)
            {
                return Task.FromResult((QueryResponseType)(object)new List<LinkedPolicy> { new() { PolicyId = 5 } });
            }

            if (responseType == typeof(List<CompliancePolicy>) && query == ComplianceQueries.getPolicies)
            {
                return Task.FromResult((QueryResponseType)(object)new List<CompliancePolicy> { new() { Id = 5, Name = "Policy 5" } });
            }

            throw new NotImplementedException($"Unhandled query {query} for {responseType.Name}");
        }
    }
}
