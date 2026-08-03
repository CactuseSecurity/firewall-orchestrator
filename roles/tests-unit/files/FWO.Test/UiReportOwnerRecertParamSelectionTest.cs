using Bunit;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Report;
using FWO.Data.Workflow;
using FWO.Ui.Pages.Reporting;
using FWO.Ui.Shared;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Reflection;

namespace FWO.Test
{
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    public class UiReportOwnerRecertParamSelectionTest
    {
        private static readonly List<FwoOwner> kOwnerLabelOwners = new()
        {
            new FwoOwner
            {
                AdditionalInfo = new Dictionary<string, string>
                {
                    { "business_unit", "Payments" },
                    { "region", "EMEA" }
                }
            },
            new FwoOwner
            {
                AdditionalInfo = new Dictionary<string, string>
                {
                    { "region", "APAC" },
                    { "service_tier", "gold" }
                }
            }
        };

        [Test]
        public async Task ReportOwnerRecertParamSelection_RendersMergeAndLabelFields()
        {
            await using BunitContext context = CreateContext(new ReportOwnerRecertParamSelectionTestApiConnection());
            IRenderedComponent<ReportOwnerRecertParamSelection> cut = context.Render<ReportOwnerRecertParamSelection>(parameters => parameters
                .Add(p => p.ModellingFilter, new ModellingFilter
                {
                    OwnerLabelFilter = new LabelFilter { Name = "ConnId" }
                })
                .Add(p => p.UseLightText, false));

            Assert.That(cut.Find("#mergeOwnerRecertTables"), Is.Not.Null);
            Assert.That(cut.Find("#ownerLabel-summary"), Is.Not.Null);
            Assert.That(cut.Find("#ownerLabel-editButton"), Is.Not.Null);
            Assert.That(cut.Markup, Does.Contain("ConnId"));
        }

        [Test]
        public async Task ReportOwnerRecertParamSelection_UpdatesMergeFlag()
        {
            await using BunitContext context = CreateContext(new ReportOwnerRecertParamSelectionTestApiConnection());
            ModellingFilter filter = new();
            ModellingFilter? changedFilter = null;
            IRenderedComponent<ReportOwnerRecertParamSelection> cut = context.Render<ReportOwnerRecertParamSelection>(parameters => parameters
                .Add(p => p.ModellingFilter, filter)
                .Add(p => p.ModellingFilterChanged, updated => changedFilter = updated));

            cut.Find("#mergeOwnerRecertTables").Change(true);

            Assert.That(filter.MergeOwnerRecertTables, Is.True);
            Assert.That(changedFilter, Is.SameAs(filter));
        }

        [Test]
        public async Task ReportOwnerRecertParamSelection_UpdatesOwnerLabelFilter()
        {
            await using BunitContext context = CreateContext(new ReportOwnerRecertParamSelectionTestApiConnection());
            ModellingFilter filter = new();
            ModellingFilter? changedFilter = null;
            IRenderedComponent<ReportOwnerRecertParamSelection> cut = context.Render<ReportOwnerRecertParamSelection>(parameters => parameters
                .Add(p => p.ModellingFilter, filter)
                .Add(p => p.ModellingFilterChanged, updated => changedFilter = updated));

            IRenderedComponent<LabelFilterEditor> editor = cut.FindComponent<LabelFilterEditor>();
            SetPrivateField(editor.Instance, "labelFilterDraft", new LabelFilter
            {
                Name = "business_unit",
                Mode = LabelFilterMode.value,
                Value = "true"
            });

            await InvokePrivateTask(editor, editor.Instance, "ApplyLabelFilterDialog");

            Assert.That(filter.OwnerLabelFilter.Name, Is.EqualTo("business_unit"));
            Assert.That(filter.OwnerLabelFilter.Mode, Is.EqualTo(LabelFilterMode.value));
            Assert.That(filter.OwnerLabelFilter.Value, Is.EqualTo("true"));
            Assert.That(filter.OwnerAdditionalInfoKey, Is.EqualTo("business_unit"));
            Assert.That(changedFilter, Is.SameAs(filter));
        }

        [Test]
        public async Task ReportOwnerRecertParamSelection_DeletesOwnerLabelFilter()
        {
            await using BunitContext context = CreateContext(new ReportOwnerRecertParamSelectionTestApiConnection());
            ModellingFilter filter = new()
            {
                OwnerLabelFilter = new LabelFilter
                {
                    Name = "custom_owner_label",
                    Mode = LabelFilterMode.value,
                    Value = "foo"
                },
                OwnerAdditionalInfoKey = "custom_owner_label"
            };
            ModellingFilter? changedFilter = null;
            IRenderedComponent<ReportOwnerRecertParamSelection> cut = context.Render<ReportOwnerRecertParamSelection>(parameters => parameters
                .Add(p => p.ModellingFilter, filter)
                .Add(p => p.ModellingFilterChanged, updated => changedFilter = updated));

            IRenderedComponent<LabelFilterEditor> editor = cut.FindComponent<LabelFilterEditor>();
            SetPrivateField(editor.Instance, "showLabelFilterDialog", true);
            await InvokePrivateTask(editor, editor.Instance, "DeleteLabelFilterDialog");

            Assert.That(filter.OwnerLabelFilter.Name, Is.EqualTo(string.Empty));
            Assert.That(filter.OwnerLabelFilter.Mode, Is.EqualTo(LabelFilterMode.existing));
            Assert.That(filter.OwnerLabelFilter.Value, Is.EqualTo(string.Empty));
            Assert.That(filter.OwnerAdditionalInfoKey, Is.EqualTo(string.Empty));
            Assert.That(changedFilter, Is.SameAs(filter));
        }

        [Test]
        public async Task ReportOwnerRecertParamSelection_UpdatesShowAllAndInactiveFlagsInFormLayout()
        {
            await using BunitContext context = CreateContext(new ReportOwnerRecertParamSelectionTestApiConnection());
            ModellingFilter filter = new();
            ModellingFilter? changedFilter = null;
            IRenderedComponent<ReportOwnerRecertParamSelection> cut = context.Render<ReportOwnerRecertParamSelection>(parameters => parameters
                .Add(p => p.ModellingFilter, filter)
                .Add(p => p.ModellingFilterChanged, updated => changedFilter = updated)
                .Add(p => p.UseFormLayout, true)
                .Add(p => p.UseLightText, false));

            Assert.That(cut.Markup, Does.Contain("form-group row mt-2"));

            cut.Find("#allOwners").Change(true);
            Assert.Multiple(() =>
            {
                Assert.That(filter.ShowAllOwners, Is.True);
                Assert.That(changedFilter, Is.SameAs(filter));
            });

            cut.Find("#showInactiveRecertOwners").Change(true);
            Assert.Multiple(() =>
            {
                Assert.That(filter.ShowInactiveRecertOwners, Is.True);
                Assert.That(changedFilter, Is.SameAs(filter));
            });
        }

        [Test]
        public async Task ReportOwnerRecertParamSelection_LoadsAvailableLabelNamesFromOwnersAndAppendsCurrentSelection()
        {
            await using BunitContext context = CreateContext(new ReportOwnerRecertParamSelectionTestApiConnection());
            IRenderedComponent<ReportOwnerRecertParamSelection> cut = context.Render<ReportOwnerRecertParamSelection>(parameters => parameters
                .Add(p => p.ModellingFilter, new ModellingFilter
                {
                    OwnerLabelFilter = new LabelFilter
                    {
                        Name = "custom_owner_label"
                    }
                }));

            IRenderedComponent<LabelFilterEditor> editor = cut.FindComponent<LabelFilterEditor>();
            List<string> availableLabelNames = GetPrivateMember<List<string>>(editor.Instance, "availableLabelNames");

            Assert.Multiple(() =>
            {
                Assert.That(availableLabelNames, Does.Contain("business_unit"));
                Assert.That(availableLabelNames, Does.Contain("region"));
                Assert.That(availableLabelNames, Does.Contain("service_tier"));
                Assert.That(availableLabelNames, Does.Contain("custom_owner_label"));
            });
        }

        [Test]
        public async Task ReportOwnerRecertParamSelection_UsesProvidedAvailableLabelNamesWithoutQueryingOwners()
        {
            await using BunitContext context = CreateContext(new ThrowingApiConnection());
            IRenderedComponent<ReportOwnerRecertParamSelection> cut = context.Render<ReportOwnerRecertParamSelection>(parameters => parameters
                .Add(p => p.ModellingFilter, new ModellingFilter
                {
                    OwnerLabelFilter = new LabelFilter
                    {
                        Name = "custom_owner_label"
                    }
                })
                .Add(p => p.AvailableLabelNames, new List<string> { "team_label" }));

            IRenderedComponent<LabelFilterEditor> editor = cut.FindComponent<LabelFilterEditor>();
            List<string> availableLabelNames = GetPrivateMember<List<string>>(editor.Instance, "availableLabelNames");

            Assert.Multiple(() =>
            {
                Assert.That(availableLabelNames, Does.Contain("team_label"));
                Assert.That(availableLabelNames, Does.Contain("custom_owner_label"));
            });
        }

        private static BunitContext CreateContext(ApiConnection apiConnection)
        {
            BunitContext context = new();
            context.Services.AddSingleton<UserConfig>(new SimulatedUserConfig());
            context.Services.AddSingleton<ApiConnection>(apiConnection);
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

        private sealed class ReportOwnerRecertParamSelectionTestApiConnection : SimulatedApiConnection
        {
            public override Task<T> SendQueryAsync<T>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                if (query == OwnerQueries.getOwners)
                {
                    return Task.FromResult((T)(object)kOwnerLabelOwners);
                }

                throw new InvalidOperationException($"Unexpected query: {query}");
            }
        }

        private sealed class ThrowingApiConnection : SimulatedApiConnection
        {
            public override Task<T> SendQueryAsync<T>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                throw new InvalidOperationException($"Unexpected query: {query}");
            }
        }
    }
}
