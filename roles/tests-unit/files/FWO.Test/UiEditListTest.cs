using Bunit;
using FWO.Ui.Shared;
using Microsoft.AspNetCore.Components;
using NUnit.Framework;

namespace FWO.Test;

[TestFixture]
internal class UiEditListTest
{
    [Test]
    public async Task EditList_RendersAddedAndExistingElementsWithTheirDisplayContent()
    {
        await using BunitContext context = new();
        string[] elements = ["existing"];
        List<string> elementsToAdd = ["added"];
        RenderFragment<string> display = value => builder => builder.AddContent(0, value);

        IRenderedComponent<EditList<string>> component = context.Render<EditList<string>>(parameters => parameters
            .Add(p => p.Elements, elements)
            .Add(p => p.ElementsToAdd, elementsToAdd)
            .Add(p => p.Display, display));

        Assert.Multiple(() =>
        {
            Assert.That(component.FindAll("li"), Has.Count.EqualTo(2));
            Assert.That(component.Markup, Does.Contain("added"));
            Assert.That(component.Markup, Does.Contain("existing"));
            Assert.That(component.FindAll("li")[0].ClassList, Does.Contain("bg-success-subtle"));
        });
    }

    [Test]
    public async Task EditList_TogglesRemovalAndUndoForExistingElements()
    {
        await using BunitContext context = new();
        string[] elements = ["existing"];
        List<string> elementsToDelete = [];
        RenderFragment<string> display = value => builder => builder.AddContent(0, value);
        IRenderedComponent<EditList<string>> component = context.Render<EditList<string>>(parameters => parameters
            .Add(p => p.Elements, elements)
            .Add(p => p.ElementsToDelete, elementsToDelete)
            .Add(p => p.Display, display)
            .Add(p => p.StdLayout, false));

        component.Find("li span").Click();

        Assert.Multiple(() =>
        {
            Assert.That(elementsToDelete, Is.EquivalentTo(elements));
            Assert.That(component.Find("li div").GetAttribute("style"), Does.Contain("opacity: 0.5"));
            Assert.That(component.Find("li span").ClassList, Does.Contain("bg-success"));
            Assert.That(component.Find("li i").ClassList, Does.Contain("bi-arrow-counterclockwise"));
        });

        component.Find("li span").Click();

        Assert.Multiple(() =>
        {
            Assert.That(elementsToDelete, Is.Empty);
            Assert.That(component.Find("li div").GetAttribute("style"), Does.Contain("opacity: 1"));
            Assert.That(component.Find("li span").ClassList, Does.Contain("bg-danger"));
            Assert.That(component.Find("li i").ClassList, Does.Contain("bi-x-lg"));
        });
    }

    [Test]
    public async Task EditList_RemovesAddedElementsAndInvokesCallbacks()
    {
        await using BunitContext context = new();
        List<string> elementsToAdd = ["added"];
        int afterRemoveCount = 0;
        int elementsRemovedCount = 0;
        RenderFragment<string> display = value => builder => builder.AddContent(0, value);
        IRenderedComponent<EditList<string>> component = context.Render<EditList<string>>(parameters => parameters
            .Add(p => p.ElementsToAdd, elementsToAdd)
            .Add(p => p.Display, display)
            .Add(p => p.AfterRemoveAction, () =>
            {
                afterRemoveCount++;
                return true;
            })
            .Add(p => p.ElementsRemoved, EventCallback.Factory.Create(this, () => elementsRemovedCount++)));

        component.Find("li span").Click();

        Assert.Multiple(() =>
        {
            Assert.That(elementsToAdd, Is.Empty);
            Assert.That(component.FindAll("li"), Is.Empty);
            Assert.That(afterRemoveCount, Is.EqualTo(1));
            Assert.That(elementsRemovedCount, Is.EqualTo(1));
        });
    }
}
