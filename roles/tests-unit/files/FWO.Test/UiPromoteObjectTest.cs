using AngleSharp.Dom;
using Bunit;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Flow;
using FWO.Data.Workflow;
using FWO.Services;
using FWO.Services.EventMediator;
using FWO.Ui.Pages.Request;
using FWO.Services.Workflow;
using FWO.Ui.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using static FWO.Test.UiRequestWorkflowTest;

namespace FWO.Test
{
    [TestFixture]
    internal class UiPromoteObjectTest
    {
        [Test]
        public async Task PromoteObject_MissingStateName_FallsBackToStateId()
        {
            await using BunitContext context = new();
            WfStateDict states = new();
            StateMatrix stateMatrix = new()
            {
                Matrix = new()
                {
                    [0] = [5, 6]
                }
            };
            WfStatefulObject statefulObject = new()
            {
                StateId = 0
            };

            IRenderedComponent<PromoteObject> component = RenderPromoteObject(context, states, stateMatrix, statefulObject, Roles.Requester);

            Assert.That(component.Markup, Does.Contain("promote_to"));
            Assert.That(component.Markup, Does.Contain("dropdown-input-"));
        }


    }
}
