using Bunit;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Config.Api.Data;
using FWO.Data;
using FWO.Data.Workflow;
using FWO.Middleware.Client;
using FWO.Services.EventMediator;
using FWO.Services.EventMediator.Interfaces;
using FWO.Ui.Pages.Settings;
using FWO.Ui.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Claims;

namespace FWO.Test
{
    [TestFixture]
    internal class UiEditOwnerTest
    {
        private static MethodInfo GetPrivateMethod(string name)
        {
            return typeof(EditOwner).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMethodException(typeof(EditOwner).FullName, name);
        }

        private static MethodInfo GetPrivateStaticMethod(string name)
        {
            return typeof(EditOwner).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new MissingMethodException(typeof(EditOwner).FullName, name);
        }

        private static void SetPrivateField<T>(EditOwner component, string fieldName, T value)
        {
            FieldInfo? field = typeof(EditOwner).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
            {
                throw new MissingFieldException(typeof(EditOwner).FullName, fieldName);
            }
            field.SetValue(component, value);
        }

        private static void SetPrivateProperty<T>(EditOwner component, string propertyName, T value)
        {
            PropertyInfo? property = typeof(EditOwner).GetProperty(propertyName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (property == null)
            {
                throw new MissingMemberException(typeof(EditOwner).FullName, propertyName);
            }
            property.SetValue(component, value);
        }

        private static IRenderedComponent<EditOwner> RenderEditOwner(
            Bunit.TestContext context,
            FwoOwner owner,
            bool readOnly,
            List<OwnerResponsibleType>? responsibleTypes = null,
            List<FwoOwner>? existingOwners = null,
            List<OwnerLifeCycleState>? ownerLifeCycleStates = null)
        {
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<AuthenticationStateProvider>(new UiEditOwnerAuthStateProvider());
            context.Services.AddSingleton<ApiConnection>(new EditOwnerTestApiConn());
            context.Services.AddSingleton(new MiddlewareClient("http://localhost/"));
            context.Services.AddSingleton<UserConfig>(new EditOwnerTestUserConfig());
            context.Services.AddSingleton<DomEventService>();
            context.Services.AddSingleton<IEventMediator>(new EventMediator());

            IRenderedComponent<CascadingAuthenticationState> wrapper = context.Render<CascadingAuthenticationState>(parameters => parameters
                .AddChildContent<EditOwner>(child => child
                    .Add(p => p.Display, true)
                    .Add(p => p.Readonly, readOnly)
                    .Add(p => p.ActOwner, owner)
                    .Add(p => p.OwnerResponsibleTypes, responsibleTypes ?? [])
                    .Add(p => p.Tenants, [])
                    .Add(p => p.OwnerLifeCycleStates, ownerLifeCycleStates ?? [])
                    .Add(p => p.ExistingOwners, existingOwners ?? [])));

            return wrapper.FindComponent<EditOwner>();
        }

        [Test]
        public async Task EditOwner_EditableMode_BindsCriticalityInput()
        {
            await using Bunit.TestContext context = new();
            FwoOwner owner = new() { Id = 0, Name = "Owner A", Criticality = "high" };
            IRenderedComponent<EditOwner> editOwner = RenderEditOwner(context, owner, readOnly: false);

            var criticalityInput = editOwner.FindAll("input.form-control.form-control-sm")
                .First(i => i.GetAttribute("value") == "high");
            criticalityInput.Change("very-high");

            Assert.That(owner.Criticality, Is.EqualTo("very-high"));
        }

        [Test]
        public async Task EditOwner_ReadonlyMode_ShowsCriticalityAsText()
        {
            await using Bunit.TestContext context = new();
            FwoOwner owner = new() { Id = 0, Name = "Owner A", Criticality = "high" };
            IRenderedComponent<EditOwner> editOwner = RenderEditOwner(context, owner, readOnly: true);

            Assert.That(editOwner.Markup, Does.Contain("criticality"));
            Assert.That(editOwner.Markup, Does.Contain("high"));
        }

        [Test]
        public async Task EditOwner_AddOwnerResponsible_AddsDnAndClearsInput()
        {
            await using Bunit.TestContext context = new();
            FwoOwner owner = new() { Id = 0, Name = "Owner A" };
            List<OwnerResponsibleType> types =
            [
                new OwnerResponsibleType { Id = GlobalConst.kOwnerResponsibleTypeMain, Name = "main", Active = true }
            ];
            IRenderedComponent<EditOwner> editOwner = RenderEditOwner(context, owner, readOnly: false, responsibleTypes: types);

            SetPrivateField(editOwner.Instance, "NewOwnerResponsibleDnByType", new Dictionary<int, string>
            {
                [GlobalConst.kOwnerResponsibleTypeMain] = "cn=main,dc=test"
            });

            GetPrivateMethod("AddOwnerResponsible").Invoke(editOwner.Instance, [GlobalConst.kOwnerResponsibleTypeMain]);

            Assert.That(owner.GetOwnerResponsiblesByType(GlobalConst.kOwnerResponsibleTypeMain), Has.Count.EqualTo(1));
            Assert.That(owner.GetOwnerResponsiblesByType(GlobalConst.kOwnerResponsibleTypeMain)[0], Is.EqualTo("cn=main,dc=test"));

            FieldInfo field = typeof(EditOwner).GetField("NewOwnerResponsibleDnByType", BindingFlags.NonPublic | BindingFlags.Instance)!;
            Dictionary<int, string> state = (Dictionary<int, string>)field.GetValue(editOwner.Instance)!;
            Assert.That(state[GlobalConst.kOwnerResponsibleTypeMain], Is.EqualTo(""));
        }

        [Test]
        public async Task EditOwner_AddOwnerResponsible_DoesNotDuplicateExistingDn()
        {
            await using Bunit.TestContext context = new();
            FwoOwner owner = new() { Id = 0, Name = "Owner A" };
            owner.AddOwnerResponsible(GlobalConst.kOwnerResponsibleTypeMain, "cn=main,dc=test");
            List<OwnerResponsibleType> types =
            [
                new OwnerResponsibleType { Id = GlobalConst.kOwnerResponsibleTypeMain, Name = "main", Active = true }
            ];
            IRenderedComponent<EditOwner> editOwner = RenderEditOwner(context, owner, readOnly: false, responsibleTypes: types);

            SetPrivateField(editOwner.Instance, "NewOwnerResponsibleDnByType", new Dictionary<int, string>
            {
                [GlobalConst.kOwnerResponsibleTypeMain] = "cn=main,dc=test"
            });

            GetPrivateMethod("AddOwnerResponsible").Invoke(editOwner.Instance, [GlobalConst.kOwnerResponsibleTypeMain]);

            Assert.That(owner.GetOwnerResponsiblesByType(GlobalConst.kOwnerResponsibleTypeMain), Has.Count.EqualTo(1));
        }

        [Test]
        public async Task EditOwner_AddOwnerResponsible_EmptyDn_DoesNothing()
        {
            await using Bunit.TestContext context = new();
            FwoOwner owner = new() { Id = 0, Name = "Owner A" };
            List<OwnerResponsibleType> types =
            [
                new OwnerResponsibleType { Id = GlobalConst.kOwnerResponsibleTypeMain, Name = "main", Active = true }
            ];
            IRenderedComponent<EditOwner> editOwner = RenderEditOwner(context, owner, readOnly: false, responsibleTypes: types);

            SetPrivateField(editOwner.Instance, "NewOwnerResponsibleDnByType", new Dictionary<int, string>
            {
                [GlobalConst.kOwnerResponsibleTypeMain] = "   "
            });

            GetPrivateMethod("AddOwnerResponsible").Invoke(editOwner.Instance, [GlobalConst.kOwnerResponsibleTypeMain]);

            Assert.That(owner.GetOwnerResponsiblesByType(GlobalConst.kOwnerResponsibleTypeMain), Is.Empty);
        }

        [Test]
        public async Task EditOwner_CheckValues_FailsWithoutAnyResponsibles()
        {
            await using Bunit.TestContext context = new();
            FwoOwner owner = new() { Id = 0, Name = "Owner A" };
            IRenderedComponent<EditOwner> editOwner = RenderEditOwner(context, owner, readOnly: false);

            bool valid = (bool)GetPrivateMethod("CheckValues").Invoke(editOwner.Instance, null)!;

            Assert.That(valid, Is.False);
        }

        [Test]
        public async Task EditOwner_CheckValues_FailsWithoutName()
        {
            await using Bunit.TestContext context = new();
            FwoOwner owner = new() { Id = 0, Name = "" };
            owner.AddOwnerResponsible(GlobalConst.kOwnerResponsibleTypeMain, "cn=main,dc=test");
            IRenderedComponent<EditOwner> editOwner = RenderEditOwner(context, owner, readOnly: false);

            bool valid = (bool)GetPrivateMethod("CheckValues").Invoke(editOwner.Instance, null)!;

            Assert.That(valid, Is.False);
        }

        [Test]
        public async Task EditOwner_CheckValues_FailsForDuplicateOwnerNameInAddMode()
        {
            await using Bunit.TestContext context = new();
            FwoOwner owner = new() { Id = 0, Name = "Owner A" };
            owner.AddOwnerResponsible(GlobalConst.kOwnerResponsibleTypeMain, "cn=main,dc=test");
            List<FwoOwner> existing = [new() { Id = 42, Name = "Owner A" }];

            IRenderedComponent<EditOwner> editOwner = RenderEditOwner(context, owner, readOnly: false, existingOwners: existing);

            bool valid = (bool)GetPrivateMethod("CheckValues").Invoke(editOwner.Instance, null)!;

            Assert.That(valid, Is.False);
        }

        [Test]
        public async Task EditOwner_PrepareOwnerForSave_MapsSelectionsAndNormalizesRecertParams()
        {
            await using Bunit.TestContext context = new();
            FwoOwner owner = new() { Id = 0, Name = "Owner A" };
            IRenderedComponent<EditOwner> editOwner = RenderEditOwner(context, owner, readOnly: false);

            SetPrivateField(editOwner.Instance, "SelectedTenant", new Tenant { Id = 11, Name = "Tenant A" });
            SetPrivateField(editOwner.Instance, "SelectedOwnerLifeCycleState", new OwnerLifeCycleState { Id = 5, Name = "prod" });
            SetPrivateField(editOwner.Instance, "ActRecCheckParams", new RecertCheckParams
            {
                RecertCheckInterval = SchedulerInterval.Weeks,
                RecertCheckOffset = 2,
                RecertCheckDayOfMonth = 0
            });
            SetPrivateField(editOwner.Instance, "SelectedDayOfWeek", DayOfWeek.Friday);

            GetPrivateMethod("PrepareOwnerForSave").Invoke(editOwner.Instance, null);

            Assert.That(owner.TenantId, Is.EqualTo(11));
            Assert.That(owner.OwnerLifeCycleStateId, Is.EqualTo(5));
            Assert.That(owner.RecertCheckParamString, Is.Not.Null.And.Not.Empty);

            RecertCheckParams? parsed = System.Text.Json.JsonSerializer.Deserialize<RecertCheckParams>(owner.RecertCheckParamString!);
            Assert.That(parsed, Is.Not.Null);
            Assert.That(parsed!.RecertCheckWeekday, Is.EqualTo((int)DayOfWeek.Friday));
            Assert.That(parsed.RecertCheckDayOfMonth, Is.Null);
        }

        [Test]
        public async Task EditOwner_HasRelevantOwnerMappingChanges_ReturnsTrue_ForLifecycleActivityChange()
        {
            await using Bunit.TestContext context = new();
            FwoOwner owner = new() { Id = 5, Name = "Owner A", OwnerLifeCycleStateId = 1 };
            IRenderedComponent<EditOwner> editOwner = RenderEditOwner(context, owner, readOnly: false, ownerLifeCycleStates:
            [
                new() { Id = 1, Name = "inactive", ActiveState = false },
                new() { Id = 2, Name = "active", ActiveState = true }
            ]);
            owner.OwnerLifeCycleStateId = 2;

            bool changed = (bool)GetPrivateMethod("HasRelevantOwnerMappingChanges").Invoke(editOwner.Instance, null)!;

            Assert.That(changed, Is.True);
        }

        [Test]
        public async Task EditOwner_HasRelevantOwnerMappingChanges_ReturnsTrue_ForOwnedIpChange()
        {
            await using Bunit.TestContext context = new();
            FwoOwner owner = new() { Id = 6, Name = "Owner B", OwnerLifeCycleStateId = 1 };
            IRenderedComponent<EditOwner> editOwner = RenderEditOwner(context, owner, readOnly: false);

            List<NwObjectElement> originalIps = [new("10.0.0.1/32", 1)];
            List<NwObjectElement> currentIps = [new("10.0.0.2/32", 1)];
            SetPrivateField(editOwner.Instance, "OriginalOwnedIpKeys", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "10.0.0.1/32|10.0.0.1/32" });
            SetPrivateField(editOwner.Instance, "ActIpAddresses", currentIps);

            bool changed = (bool)GetPrivateMethod("HasRelevantOwnerMappingChanges").Invoke(editOwner.Instance, null)!;

            Assert.That(changed, Is.True);
        }

        [Test]
        public async Task EditOwner_GetDecommDateAfterLifecycleChange_SetsDateForDeactivateAndClearsForReactivate()
        {
            await using Bunit.TestContext context = new();
            FwoOwner owner = new() { Id = 7, Name = "Owner C", OwnerLifeCycleStateId = 1 };
            IRenderedComponent<EditOwner> editOwner = RenderEditOwner(context, owner, readOnly: false, ownerLifeCycleStates:
            [
                new() { Id = 1, Name = "active", ActiveState = true },
                new() { Id = 2, Name = "inactive", ActiveState = false }
            ]);

            owner.OwnerLifeCycleStateId = 2;
            DateTime? decommDate = (DateTime?)GetPrivateMethod("GetDecommDateAfterLifecycleChange").Invoke(editOwner.Instance, null);
            Assert.That(decommDate, Is.Not.Null);

            SetPrivateProperty(editOwner.Instance, "OriginalOwnerLifeCycleStateId", 2);
            owner.DecommDate = DateTime.UtcNow.AddDays(-1);
            owner.OwnerLifeCycleStateId = 1;
            DateTime? reactivatedDate = (DateTime?)GetPrivateMethod("GetDecommDateAfterLifecycleChange").Invoke(editOwner.Instance, null);
            Assert.That(reactivatedDate, Is.Null);
        }

        [Test]
        public void EditOwner_FormatOwnerResponsibles_UsesUserOrGroupNameAndJoins()
        {
            string dnUser = "CN=Max Mustermann,OU=Users,DC=example,DC=com";
            string dnGroup = "CN=NetOps,OU=Groups,DC=example,DC=com";
            string dnRaw = "invalid-dn";

            string formatted = (string)GetPrivateStaticMethod("FormatOwnerResponsibles").Invoke(null, [new List<string> { dnUser, dnGroup, dnRaw }])!;

            Assert.That(formatted, Does.Contain("Max Mustermann"));
            Assert.That(formatted, Does.Contain("NetOps"));
            Assert.That(formatted, Does.Contain("invalid-dn"));
            Assert.That(formatted, Does.Contain(","));
        }

        [Test]
        public void EditOwner_FormatOwnerResponsibles_IgnoresEmptyEntries()
        {
            string formatted = (string)GetPrivateStaticMethod("FormatOwnerResponsibles").Invoke(null, [new List<string> { "", "   " }])!;

            Assert.That(formatted, Is.EqualTo(""));
        }

        [Test]
        public void EditOwner_FormatOwnerResponsibles_ShowsFullNameWhenCnContainsEscapedComma()
        {
            string dnUser = @"CN=Mustermann\, Max,OU=Users,DC=example,DC=com";

            string formatted = (string)GetPrivateStaticMethod("FormatOwnerResponsibles").Invoke(null, [new List<string> { dnUser }])!;

            Assert.That(formatted, Is.EqualTo("Mustermann, Max"));
        }

        [Test]
        public void EditOwner_ParseRolesWithImport_ParsesLegacyArrayToSupportingType()
        {
            string rolesJson = "[\"modeller\", \"recertifier\"]";

            Dictionary<int, List<string>> parsed =
                (Dictionary<int, List<string>>)GetPrivateStaticMethod("ParseRolesWithImport").Invoke(null, [rolesJson])!;

            Assert.That(parsed.ContainsKey(GlobalConst.kOwnerResponsibleTypeSupporting), Is.True);
            Assert.That(parsed[GlobalConst.kOwnerResponsibleTypeSupporting], Is.EquivalentTo(new[] { "modeller", "recertifier" }));
        }

        [Test]
        public void EditOwner_ParseRolesWithImport_ParsesMappingByResponsibleType()
        {
            string rolesJson = "{\"1\":[\"roleA\"],\"2\":[\"roleB\",\"roleC\"]}";

            Dictionary<int, List<string>> parsed =
                (Dictionary<int, List<string>>)GetPrivateStaticMethod("ParseRolesWithImport").Invoke(null, [rolesJson])!;

            Assert.That(parsed.Keys, Is.EquivalentTo(new[] { 1, 2 }));
            Assert.That(parsed[1], Is.EquivalentTo(new[] { "roleA" }));
            Assert.That(parsed[2], Is.EquivalentTo(new[] { "roleB", "roleC" }));
        }

        [Test]
        public void EditOwner_ParseRolesWithImport_ReturnsEmptyForWhitespaceInput()
        {
            Dictionary<int, List<string>> parsed =
                (Dictionary<int, List<string>>)GetPrivateStaticMethod("ParseRolesWithImport").Invoke(null, ["   "])!;

            Assert.That(parsed, Is.Empty);
        }
    }

    internal sealed class UiEditOwnerAuthStateProvider : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            return Task.FromResult(new AuthenticationState(new ClaimsPrincipal()));
        }
    }

    internal sealed class EditOwnerTestUserConfig : SimulatedUserConfig
    {
        public override string GetText(string key)
        {
            return DummyTranslate.TryGetValue(key, out string? value) ? value : key;
        }
    }

    internal sealed class EditOwnerTestApiConn : SimulatedApiConnection
    {
        public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null)
        {
            if (query == OwnerQueries.getNetworkOwnerships)
            {
                return Task.FromResult((QueryResponseType)(object)new List<NwObjectElement>());
            }

            if (query == OwnerQueries.getRuleOwnerships)
            {
                return Task.FromResult(Activator.CreateInstance<QueryResponseType>());
            }

            throw new NotImplementedException();
        }
    }
}
