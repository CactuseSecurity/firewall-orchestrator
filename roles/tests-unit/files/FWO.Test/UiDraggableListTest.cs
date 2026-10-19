using Bunit;
using FWO.Ui.Services;
using FWO.Ui.Shared;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Covers the frame and selection styling of the draggable list.
    /// </summary>
    [TestFixture]
    internal class UiDraggableListTest
    {
        private static readonly List<string> kElements = ["first", "second"];
        private static readonly List<string> kSelectedElements = ["second"];

        private static BunitContext CreateContext()
        {
            BunitContext context = new();
            context.Services.AddSingleton(new KeyboardInputService());
            return context;
        }

        [Test]
        public async Task DraggableList_IsBorderedByDefault()
        {
            await using BunitContext context = CreateContext();

            IRenderedComponent<DraggableList<string>> component = context.Render<DraggableList<string>>(parameters => parameters
                .Add(p => p.AllElements, kElements)
                .Add(p => p.Display, element => element));

            Assert.That(component.FindAll(".bg-secondary.border"), Has.Count.EqualTo(1));
        }

        [Test]
        public async Task DraggableList_WithoutBorder_RendersNoBorderClass()
        {
            await using BunitContext context = CreateContext();

            IRenderedComponent<DraggableList<string>> component = context.Render<DraggableList<string>>(parameters => parameters
                .Add(p => p.AllElements, kElements)
                .Add(p => p.Display, element => element)
                .Add(p => p.Bordered, false));

            Assert.Multiple(() =>
            {
                Assert.That(component.FindAll(".bg-secondary"), Has.Count.EqualTo(1));
                Assert.That(component.FindAll(".border"), Is.Empty);
            });
        }

        [Test]
        public async Task DraggableList_MarksSelectedElements()
        {
            await using BunitContext context = CreateContext();

            IRenderedComponent<DraggableList<string>> component = context.Render<DraggableList<string>>(parameters => parameters
                .Add(p => p.AllElements, kElements)
                .Add(p => p.SelectedElements, kSelectedElements)
                .Add(p => p.Display, element => element));

            Assert.Multiple(() =>
            {
                Assert.That(component.FindAll(".draggable-item"), Has.Count.EqualTo(2));
                Assert.That(component.FindAll(".draggable-selected"), Has.Count.EqualTo(1));
                Assert.That(component.Find(".draggable-selected").TextContent, Is.EqualTo(kSelectedElements[0]));
            });
        }
    }
}
