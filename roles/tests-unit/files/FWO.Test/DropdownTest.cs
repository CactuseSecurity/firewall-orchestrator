using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Bunit;
using FWO.Ui.Services;
using FWO.Ui.Shared;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    public class DropdownTest : BunitContext
    {
        private static MethodInfo GetInstanceMethod(string methodName, params Type[] parameterTypes)
        {
            MethodInfo? method = typeof(Dropdown<string>).GetMethod(
                methodName,
                BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                parameterTypes,
                null);

            Assert.That(method, Is.Not.Null);
            return method!;
        }

        private static string GetSearchValue(Dropdown<string> dropdown)
        {
            FieldInfo? searchValueField = typeof(Dropdown<string>).GetField("searchValue", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(searchValueField, Is.Not.Null);
            return (string?)searchValueField!.GetValue(dropdown) ?? "";
        }

        private static void SetComponentParameter<TValue>(Dropdown<string> dropdown, string parameterName, TValue value)
        {
            PropertyInfo? parameter = typeof(Dropdown<string>).GetProperty(parameterName, BindingFlags.Public | BindingFlags.Instance);
            Assert.That(parameter, Is.Not.Null);
            parameter!.SetValue(dropdown, value);
        }

        /// <summary>
        /// Ensures that a global focus callback tolerates a missing JS runtime after disposal.
        /// </summary>
        [Test]
        public void OnFocusChanged_DoesNotThrow_WhenJsRuntimeIsNull()
        {
            Dropdown<string> dropdown = new();
            MethodInfo? method = typeof(Dropdown<string>).GetMethod("OnFocusChanged", BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.That(method, Is.Not.Null);
            Assert.DoesNotThrow(() => method!.Invoke(dropdown, ["test-element"]));
        }

        /// <summary>
        /// Verifies that filtering matches values without regard to input casing.
        /// </summary>
        [Test]
        public void Filter_IsCaseInsensitive()
        {
            Dropdown<string> dropdown = new();
            SetComponentParameter(dropdown, nameof(Dropdown<string>.Elements), new[] { "Alpha", "beta", "Gamma" });
            MethodInfo filterMethod = GetInstanceMethod("Filter", typeof(string));

            filterMethod.Invoke(dropdown, ["AL"]);

            Assert.That(dropdown.FilteredElements, Is.EqualTo(["Alpha"]));
        }

        /// <summary>
        /// Verifies that the none-selected label is shown when there is no selection.
        /// </summary>
        [Test]
        public void DisplaySelection_UsesNoneSelectedText_WhenNoSelection()
        {
            Dropdown<string> dropdown = new();
            SetComponentParameter(dropdown, nameof(Dropdown<string>.NoneSelectedText), "none");
            MethodInfo displaySelectionMethod = GetInstanceMethod("DisplaySelection", typeof(IEnumerable<string>));

            displaySelectionMethod.Invoke(dropdown, [Enumerable.Empty<string>()]);

            Assert.That(GetSearchValue(dropdown), Is.EqualTo("none"));
        }

        /// <summary>
        /// Verifies that multiple selected values are rendered as first item plus count summary.
        /// </summary>
        [Test]
        public void DisplaySelection_FormatsSummary_WhenMultipleElementsSelected()
        {
            Dropdown<string> dropdown = new();
            MethodInfo displaySelectionMethod = GetInstanceMethod("DisplaySelection", typeof(IEnumerable<string>));

            displaySelectionMethod.Invoke(dropdown, [new[] { "first", "second", "third" }]);

            Assert.That(GetSearchValue(dropdown), Is.EqualTo("first, ... (+ 2)"));
        }

        /// <summary>
        /// Verifies that selecting the same element twice in multiselect mode keeps a single entry.
        /// </summary>
        [Test]
        public async Task SelectElement_MultiSelect_AddsElementOnlyOnce()
        {
            Services.AddScoped<DomEventService>();
            IRenderedComponent<Dropdown<string>> renderedDropdown = Render<Dropdown<string>>(parameters => parameters
                .Add(p => p.Multiselect, true)
                .Add(p => p.SelectedElements, []));
            Dropdown<string> dropdown = renderedDropdown.Instance;
            MethodInfo selectMethod = GetInstanceMethod("SelectElement", typeof(string));

            Task firstSelection = (Task)selectMethod.Invoke(dropdown, ["one"])!;
            await firstSelection;
            Task secondSelection = (Task)selectMethod.Invoke(dropdown, ["one"])!;
            await secondSelection;

            Assert.That(dropdown.SelectedElements, Is.EqualTo(["one"]));
            Assert.That(dropdown.Toggled, Is.False);
        }

        /// <summary>
        /// Verifies that unselecting one item in multiselect mode removes only that item.
        /// </summary>
        [Test]
        public async Task UnselectElement_MultiSelect_RemovesElementFromSelection()
        {
            Services.AddScoped<DomEventService>();
            IRenderedComponent<Dropdown<string>> renderedDropdown = Render<Dropdown<string>>(parameters => parameters
                .Add(p => p.Multiselect, true)
                .Add(p => p.SelectedElements, ["one", "two"]));
            Dropdown<string> dropdown = renderedDropdown.Instance;
            MethodInfo unselectMethod = GetInstanceMethod("UnselectElement", typeof(string));

            Task unselection = (Task)unselectMethod.Invoke(dropdown, ["one"])!;
            await unselection;

            Assert.That(dropdown.SelectedElements, Is.EqualTo(["two"]));
            Assert.That(dropdown.Toggled, Is.False);
        }
    }
}
