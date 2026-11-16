using FWO.Config.Api;
using FWO.Services.Workflow;

namespace FWO.Ui.Services;

/// <summary>
/// Supplies workflow data and UI refresh behavior to the approval-task editor services.
/// </summary>
internal sealed class ApprovalTaskDetailsEditorContext
{
    internal WfHandler WfHandler { get; private set; } = new();
    internal UserConfig UserConfig { get; private set; } = new();
    internal Func<Task> RefreshUi { get; private set; } = () => Task.CompletedTask;

    internal void Configure(WfHandler wfHandler, UserConfig userConfig, Func<Task> refreshUi)
    {
        WfHandler = wfHandler;
        UserConfig = userConfig;
        RefreshUi = refreshUi;
    }
}
