using Bunit;
using FWO.Config.Api;
using FWO.Data.Report;
using FWO.Ui.Services;
using FWO.Ui.Shared;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Collections.Generic;
using System.Reflection;

namespace FWO.Test
{
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    public class UiAddInfoFilterEditorTest
    {
        private static readonly List<string> kPolicyCheckLabelNames = new() { "policy_check" };
        private static readonly List<string> kBusinessUnitLabelNames = new() { "business_unit" };

        [Test]
        public async Task AddInfoFilterEditor_RendersSummaryForEmptyValueAndModeFilters()
        {
            await using BunitContext context = CreateContext();
            AddInfoFilter filter = new();
            IRenderedComponent<AddInfoFilterEditor> cut = context.Render<AddInfoFilterEditor>(parameters => parameters
                .Add(p => p.AddInfoFilter, filter)
                .Add(p => p.AvailableAddInfoNames, kPolicyCheckLabelNames)
                .Add(p => p.AllowFreeText, true)
                .Add(p => p.DefaultMode, AddInfoFilterMode.display_only)
                .Add(p => p.IdPrefix, "testLabel"));

            Assert.That(cut.Find("#testLabel-summary").GetAttribute("value"), Is.EqualTo("-"));

            cut = context.Render<AddInfoFilterEditor>(parameters => parameters
                .Add(p => p.AddInfoFilter, new AddInfoFilter
                {
                    Name = "policy_check",
                    Mode = AddInfoFilterMode.value,
                    Value = "passed"
                })
                .Add(p => p.AvailableAddInfoNames, kPolicyCheckLabelNames)
                .Add(p => p.IdPrefix, "testLabel"));
            Assert.That(cut.Find("#testLabel-summary").GetAttribute("value"), Is.EqualTo("policy_check: passed"));

            cut = context.Render<AddInfoFilterEditor>(parameters => parameters
                .Add(p => p.AddInfoFilter, new AddInfoFilter
                {
                    Name = "policy_check",
                    Mode = AddInfoFilterMode.display_only
                })
                .Add(p => p.AvailableAddInfoNames, kPolicyCheckLabelNames)
                .Add(p => p.IdPrefix, "testLabel"));
            Assert.That(cut.Find("#testLabel-summary").GetAttribute("value"), Is.EqualTo("policy_check: Display only").IgnoreCase);
        }

        [Test]
        public async Task AddInfoFilterEditor_ApplyAddInfoFilterDialog_NotifiesParent()
        {
            await using BunitContext context = CreateContext();
            AddInfoFilter? changedFilter = null;
            IRenderedComponent<AddInfoFilterEditor> cut = context.Render<AddInfoFilterEditor>(parameters => parameters
                .Add(p => p.AddInfoFilter, new AddInfoFilter())
                .Add(p => p.AddInfoFilterChanged, updated => changedFilter = updated)
                .Add(p => p.AvailableAddInfoNames, kBusinessUnitLabelNames)
                .Add(p => p.IdPrefix, "testLabel"));

            SetPrivateField(cut.Instance, "addInfoFilterDraft", new AddInfoFilter
            {
                Name = "business_unit",
                Mode = AddInfoFilterMode.value,
                Value = "true"
            });

            await InvokePrivateTask(cut, cut.Instance, "ApplyAddInfoFilterDialog");

            Assert.Multiple(() =>
            {
                Assert.That(cut.Instance.AddInfoFilter.Name, Is.EqualTo("business_unit"));
                Assert.That(cut.Instance.AddInfoFilter.Mode, Is.EqualTo(AddInfoFilterMode.value));
                Assert.That(cut.Instance.AddInfoFilter.Value, Is.EqualTo("true"));
                Assert.That(changedFilter, Is.Not.Null);
                Assert.That(changedFilter!.Name, Is.EqualTo("business_unit"));
            });
        }

        [Test]
        public async Task AddInfoFilterEditor_DeleteAddInfoFilterDialog_NotifiesParent()
        {
            await using BunitContext context = CreateContext();
            AddInfoFilter? changedFilter = null;
            IRenderedComponent<AddInfoFilterEditor> cut = context.Render<AddInfoFilterEditor>(parameters => parameters
                .Add(p => p.AddInfoFilter, new AddInfoFilter
                {
                    Name = "policy_check",
                    Mode = AddInfoFilterMode.value,
                    Value = "passed"
                })
                .Add(p => p.AddInfoFilterChanged, updated => changedFilter = updated)
                .Add(p => p.AvailableAddInfoNames, kPolicyCheckLabelNames)
                .Add(p => p.IdPrefix, "testLabel"));

            SetPrivateField(cut.Instance, "showAddInfoFilterDialog", true);
            await InvokePrivateTask(cut, cut.Instance, "DeleteAddInfoFilterDialog");

            Assert.Multiple(() =>
            {
                Assert.That(cut.Instance.AddInfoFilter.Name, Is.EqualTo(string.Empty));
                Assert.That(cut.Instance.AddInfoFilter.Mode, Is.EqualTo(AddInfoFilterMode.existing));
                Assert.That(cut.Instance.AddInfoFilter.Value, Is.EqualTo(string.Empty));
                Assert.That(changedFilter, Is.Not.Null);
                Assert.That(changedFilter!.Name, Is.EqualTo(string.Empty));
            });
        }

        [Test]
        public async Task AddInfoFilterEditor_AddsMissingLabelNameToDropdown()
        {
            await using BunitContext context = CreateContext();
            IRenderedComponent<AddInfoFilterEditor> cut = context.Render<AddInfoFilterEditor>(parameters => parameters
                .Add(p => p.AddInfoFilter, new AddInfoFilter
                {
                    Name = "custom_label",
                    Mode = AddInfoFilterMode.existing
                })
                .Add(p => p.AvailableAddInfoNames, Array.Empty<string>())
                .Add(p => p.IdPrefix, "testLabel"));

            List<string> availableAddInfoNames = GetPrivateMember<List<string>>(cut.Instance, "availableAddInfoNames");

            Assert.That(availableAddInfoNames, Does.Contain("custom_label"));
        }

        [Test]
        public async Task AddInfoFilterEditor_OpensEmptyFilterWithDisplayOnlyDefaultMode()
        {
            await using BunitContext context = CreateContext();
            IRenderedComponent<AddInfoFilterEditor> cut = context.Render<AddInfoFilterEditor>(parameters => parameters
                .Add(p => p.AddInfoFilter, new AddInfoFilter())
                .Add(p => p.AvailableAddInfoNames, kPolicyCheckLabelNames)
                .Add(p => p.DefaultMode, AddInfoFilterMode.display_only)
                .Add(p => p.IdPrefix, "testLabel"));

            cut.Find("#testLabel-editButton").Click();

            AddInfoFilter draft = GetPrivateMember<AddInfoFilter>(cut.Instance, "addInfoFilterDraft");

            Assert.That(draft.Mode, Is.EqualTo(AddInfoFilterMode.display_only));
        }

        [Test]
        public async Task AddInfoFilterEditor_CommitsTypedFreeTextAndKeepsItOnReopen()
        {
            await using BunitContext context = CreateContext();
            AddInfoFilter filter = new();
            IRenderedComponent<AddInfoFilterEditor> cut = context.Render<AddInfoFilterEditor>(parameters => parameters
                .Add(p => p.AddInfoFilter, filter)
                .Add(p => p.AvailableAddInfoNames, kPolicyCheckLabelNames)
                .Add(p => p.AllowFreeText, true)
                .Add(p => p.IdPrefix, "testLabel"));

            cut.Find("#testLabel-editButton").Click();

            IRenderedComponent<Dropdown<string>> dropdown = cut.FindComponent<Dropdown<string>>();
            SetPrivateField(dropdown.Instance, "searchValue", "custom_label");
            await InvokePrivateTask(dropdown, dropdown.Instance, "CommitFreeTextSelection");

            cut.Find("#testLabel-saveButton").Click();

            Assert.That(cut.Instance.AddInfoFilter.Name, Is.EqualTo("custom_label"));
            Assert.That(cut.Find("#testLabel-summary").GetAttribute("value"), Is.EqualTo("custom_label: Display only").IgnoreCase);
        }

        private static BunitContext CreateContext()
        {
            BunitContext context = new();
            context.Services.AddSingleton<UserConfig>(new SimulatedUserConfig());
            context.Services.AddScoped<DomEventService>();
            context.Services.AddLocalization();
            return context;
        }

        private static void SetPrivateField(object instance, string fieldName, object value)
        {
            instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(instance, value);
        }

        private static T GetPrivateMember<T>(object instance, string memberName)
        {
            return (T)(instance.GetType().GetField(memberName, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(instance)
                ?? throw new MissingFieldException(instance.GetType().FullName, memberName));
        }

        private static async Task InvokePrivateTask<TComponent>(IRenderedComponent<TComponent> component, object instance, string methodName)
            where TComponent : Microsoft.AspNetCore.Components.IComponent
        {
            await component.InvokeAsync(async () =>
            {
                Task? task = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(instance, null) as Task;
                if (task != null)
                {
                    await task;
                }
            });
        }
    }
}
