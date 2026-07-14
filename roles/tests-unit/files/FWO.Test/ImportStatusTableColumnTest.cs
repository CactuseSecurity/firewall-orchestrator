using System.Linq.Expressions;
using BlazorTable;
using Bunit;
using FWO.Data;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Guards against the BlazorTable crash class fixed in PR #4733: a &lt;Column Field="..."&gt;
    /// whose lambda is not a direct member access (e.g. "x => !x.ImportDisabled") makes
    /// BlazorTable.Table.AddColumn resolve a null MemberInfo and throw a NullReferenceException
    /// in BlazorTable.Utilities.GetMemberUnderlyingType during render.
    ///
    /// These tests render the real BlazorTable Table/Column components, so they exercise the
    /// exact code path that fails at runtime rather than reimplementing BlazorTable internals.
    /// </summary>
    [TestFixture]
    internal class ImportStatusTableColumnTest
    {
        private static readonly ImportStatus[] SampleRows =
        [
            new() { MgmId = 1, MgmName = "fw-a", ImportDisabled = false },
            new() { MgmId = 2, MgmName = "fw-b", ImportDisabled = true }
        ];

        /// <summary>
        /// Renders a single-column BlazorTable bound to <paramref name="field"/>. Throws the same
        /// exception BlazorTable would throw in the live UI if the field expression is unusable.
        /// </summary>
        private static void RenderTableWithColumn(Expression<Func<ImportStatus, object>> field)
        {
            using BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddBlazorTable();

            context.Render<Table<ImportStatus>>(parameters => parameters
                .Add(p => p.Items, SampleRows)
                .AddChildContent<Column<ImportStatus>>(column => column
                    .Add(p => p.Title, "col")
                    .Add(p => p.Field, field)
                    .Add(p => p.Sortable, true)
                    .Add(p => p.Filterable, true)));
        }

        // --- Regression guards for the columns actually used by MonitorImportStatus.razor ---

        [Test]
        public void ImportEnabledColumn_RendersWithoutThrowing()
        {
            // This is the fixed binding: a real (computed) member, not "x => !x.ImportDisabled".
            Assert.DoesNotThrow(() => RenderTableWithColumn(x => x.ImportEnabled));
        }

        [Test]
        public void PlainMemberColumns_RenderWithoutThrowing()
        {
            Assert.Multiple(() =>
            {
                Assert.DoesNotThrow(() => RenderTableWithColumn(x => x.MgmId));
                Assert.DoesNotThrow(() => RenderTableWithColumn(x => x.MgmName));
                Assert.DoesNotThrow(() => RenderTableWithColumn(x => x.ImportDisabled));
            });
        }

        // --- Documents the failure mode so the guard's purpose stays explicit ---

        [Test]
        public void NegatedFieldExpression_ReproducesTheCrash()
        {
            // "x => !x.ImportDisabled" is not a member access -> NullReferenceException at render.
            // If a future BlazorTable upgrade ever makes this safe, this test will flag that the
            // workaround (the computed ImportEnabled property) is no longer strictly required.
            Assert.Throws<NullReferenceException>(() => RenderTableWithColumn(x => !x.ImportDisabled));
        }
    }
}
