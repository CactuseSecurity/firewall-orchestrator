using Bunit;
using FWO.Data;
using FWO.Data.Report;
using FWO.Ui.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using AngleSharp.Dom;
using FWO.Basics;

namespace FWO.Test
{
    [TestFixture]
    public class TreeTableDebounceTests : Bunit.TestContext
    {
        private const int DebounceDelaySubstractor = 100;

        [SetUp]
        public void Setup()
        {
            // Register a mock or fake UserConfig service
            Services.AddSingleton(new Config.Api.UserConfig());
        }

        [Test]
        public async Task Search_Is_Debounced()
        {
            // Arrange: create minimal test data and render the component
            var nodes = new List<TreeNode<Rule>>();
            var cut = RenderComponent<TreeTable<Rule>>(parameters => parameters
                .Add(p => p.Nodes, nodes)
                .Add(p => p.CellTemplate, rule => builder => { })
            );

            IElement? input = cut.Find("#searchbar");

            // Act: Simulate rapid typing
            input.Input(new ChangeEventArgs { Value = "a" });
            input.Input(new ChangeEventArgs { Value = "ab" });
            input.Input(new ChangeEventArgs { Value = "abc" });

            int debounceDelay = GlobalConst.SearchInputDebounceTime - DebounceDelaySubstractor;

            Assert.That(debounceDelay, Is.GreaterThan(0), 
                $"{nameof(debounceDelay)} resulted in '0' ms after the delay substraction" );

            // Wait for the debounce delay
            await Task.Delay(debounceDelay);

            // Assert: Check that the search text is not immediately applied
            object? value = cut.Instance.GetType().GetProperty(nameof(TreeTable<object>.AppliedSearchText))?.GetValue(cut.Instance);
            Assert.That(value, Is.Not.EqualTo("abc"));

            // Wait a bit longer to ensure the debounce has completed
            await Task.Delay(GlobalConst.SearchInputDebounceTime + DebounceDelaySubstractor);

            // Assert: Check that the search text is applied after the debounce delay
            value = cut.Instance.GetType().GetProperty(nameof(TreeTable<object>.AppliedSearchText))?.GetValue(cut.Instance);
            Assert.That(cut.Instance.GetType().GetProperty("AppliedSearchText")?.GetValue(cut.Instance), Is.EqualTo("abc"));
        }
    }
}
