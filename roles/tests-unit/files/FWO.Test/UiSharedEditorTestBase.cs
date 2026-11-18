using Bunit;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Flow;
using FWO.Data.Workflow;
using FWO.Ui.Pages.Request;
using FWO.Ui.Pages.Settings;
using FWO.Ui.Services;
using FWO.Ui.Shared;
using FWO.Services.Workflow;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Reflection;

namespace FWO.Test;

internal abstract class UiSharedEditorTestBase
{
    protected static BunitContext CreateContext()
    {
        BunitContext context = new();
        context.Services.AddSingleton<UserConfig>(new SimulatedUserConfig());
        context.Services.AddSingleton<DomEventService>();
        context.Services.AddSingleton<ApiConnection>(new UiRequestWorkflowTest.RequestWorkflowApiConn());
        return context;
    }
}
