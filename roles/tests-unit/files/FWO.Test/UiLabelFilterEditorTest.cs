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
    public class UiLabelFilterEditorTest
    {
        private static readonly List<string> kPolicyCheckLabelNames = new() { "policy_check" };
        private static readonly List<string> kBusinessUnitLabelNames = new() { "business_unit" };

        [Test]
        public async Task LabelFilterEditor_RendersSummaryForEmptyValueAndModeFilters()
        {
            await using BunitContext context = CreateContext();
            LabelFilter filter = new();
            IRenderedComponent<LabelFilterEditor> cut = context.Render<LabelFilterEditor>(parameters => parameters
                .Add(p => p.LabelFilter, filter)
                .Add(p => p.AvailableLabelNames, kPolicyCheckLabelNames)
                .Add(p => p.IdPrefix, "testLabel"));

            Assert.That(cut.Find("#testLabel-summary").GetAttribute("value"), Is.EqualTo("-"));

            cut = context.Render<LabelFilterEditor>(parameters => parameters
                .Add(p => p.LabelFilter, new LabelFilter
                {
                    Name = "policy_check",
                    Mode = LabelFilterMode.value,
                    Value = "passed"
                })
                .Add(p => p.AvailableLabelNames, kPolicyCheckLabelNames)
                .Add(p => p.IdPrefix, "testLabel"));
            Assert.That(cut.Find("#testLabel-summary").GetAttribute("value"), Is.EqualTo("policy_check: passed"));

            cut = context.Render<LabelFilterEditor>(parameters => parameters
                .Add(p => p.LabelFilter, new LabelFilter
                {
                    Name = "policy_check",
                    Mode = LabelFilterMode.display_only
                })
                .Add(p => p.AvailableLabelNames, kPolicyCheckLabelNames)
                .Add(p => p.IdPrefix, "testLabel"));
            Assert.That(cut.Find("#testLabel-summary").GetAttribute("value"), Is.EqualTo("policy_check: Display only").IgnoreCase);
        }

        [Test]
        public async Task LabelFilterEditor_ApplyLabelFilterDialog_NotifiesParent()
        {
            using BunitContext context = CreateContext();
            LabelFilter? changedFilter = null;
            IRenderedComponent<LabelFilterEditor> cut = context.Render<LabelFilterEditor>(parameters => parameters
                .Add(p => p.LabelFilter, new LabelFilter())
                .Add(p => p.LabelFilterChanged, updated => changedFilter = updated)
                .Add(p => p.AvailableLabelNames, kBusinessUnitLabelNames)
                .Add(p => p.IdPrefix, "testLabel"));

            SetPrivateField(cut.Instance, "labelFilterDraft", new LabelFilter
            {
                Name = "business_unit",
                Mode = LabelFilterMode.value,
                Value = "true"
            });

            await InvokePrivateTask(cut, cut.Instance, "ApplyLabelFilterDialog");

            Assert.Multiple(() =>
            {
                Assert.That(cut.Instance.LabelFilter.Name, Is.EqualTo("business_unit"));
                Assert.That(cut.Instance.LabelFilter.Mode, Is.EqualTo(LabelFilterMode.value));
                Assert.That(cut.Instance.LabelFilter.Value, Is.EqualTo("true"));
                Assert.That(changedFilter, Is.Not.Null);
                Assert.That(changedFilter!.Name, Is.EqualTo("business_unit"));
            });
        }

        [Test]
        public async Task LabelFilterEditor_DeleteLabelFilterDialog_NotifiesParent()
        {
            using BunitContext context = CreateContext();
            LabelFilter? changedFilter = null;
            IRenderedComponent<LabelFilterEditor> cut = context.Render<LabelFilterEditor>(parameters => parameters
                .Add(p => p.LabelFilter, new LabelFilter
                {
                    Name = "policy_check",
                    Mode = LabelFilterMode.value,
                    Value = "passed"
                })
                .Add(p => p.LabelFilterChanged, updated => changedFilter = updated)
                .Add(p => p.AvailableLabelNames, kPolicyCheckLabelNames)
                .Add(p => p.IdPrefix, "testLabel"));

            SetPrivateField(cut.Instance, "showLabelFilterDialog", true);
            await InvokePrivateTask(cut, cut.Instance, "DeleteLabelFilterDialog");

            Assert.Multiple(() =>
            {
                Assert.That(cut.Instance.LabelFilter.Name, Is.EqualTo(string.Empty));
                Assert.That(cut.Instance.LabelFilter.Mode, Is.EqualTo(LabelFilterMode.existing));
                Assert.That(cut.Instance.LabelFilter.Value, Is.EqualTo(string.Empty));
                Assert.That(changedFilter, Is.Not.Null);
                Assert.That(changedFilter!.Name, Is.EqualTo(string.Empty));
            });
        }

        [Test]
        public async Task LabelFilterEditor_AddsMissingLabelNameToDropdown()
        {
            await using BunitContext context = CreateContext();
            IRenderedComponent<LabelFilterEditor> cut = context.Render<LabelFilterEditor>(parameters => parameters
                .Add(p => p.LabelFilter, new LabelFilter
                {
                    Name = "custom_label",
                    Mode = LabelFilterMode.existing
                })
                .Add(p => p.AvailableLabelNames, Array.Empty<string>())
                .Add(p => p.IdPrefix, "testLabel"));

            List<string> availableLabelNames = GetPrivateMember<List<string>>(cut.Instance, "availableLabelNames");

            Assert.That(availableLabelNames, Does.Contain("custom_label"));
        }

        [Test]
        public async Task LabelFilterEditor_OpensEmptyFilterWithDisplayOnlyDefaultMode()
        {
            await using BunitContext context = CreateContext();
            IRenderedComponent<LabelFilterEditor> cut = context.Render<LabelFilterEditor>(parameters => parameters
                .Add(p => p.LabelFilter, new LabelFilter())
                .Add(p => p.AvailableLabelNames, kPolicyCheckLabelNames)
                .Add(p => p.IdPrefix, "testLabel"));

            cut.Find("#testLabel-editButton").Click();

            LabelFilter draft = GetPrivateMember<LabelFilter>(cut.Instance, "labelFilterDraft");

            Assert.That(draft.Mode, Is.EqualTo(LabelFilterMode.display_only));
        }

        [Test]
        public async Task LabelFilterEditor_CommitsTypedFreeTextAndKeepsItOnReopen()
        {
            await using BunitContext context = CreateContext();
            LabelFilter filter = new();
            IRenderedComponent<LabelFilterEditor> cut = context.Render<LabelFilterEditor>(parameters => parameters
                .Add(p => p.LabelFilter, filter)
                .Add(p => p.AvailableLabelNames, kPolicyCheckLabelNames)
                .Add(p => p.IdPrefix, "testLabel"));

            cut.Find("#testLabel-editButton").Click();

            IRenderedComponent<Dropdown<string>> dropdown = cut.FindComponent<Dropdown<string>>();
            SetPrivateField(dropdown.Instance, "searchValue", "custom_label");
            await InvokePrivateTask(dropdown, dropdown.Instance, "CommitFreeTextSelection");

            cut.Find("#testLabel-saveButton").Click();

            Assert.That(cut.Instance.LabelFilter.Name, Is.EqualTo("custom_label"));
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
