using FWO.Data;
using FWO.Data.Workflow;
using FWO.Services.Workflow;
using FWO.Ui.Pages.Request;
using Microsoft.AspNetCore.Components;

namespace FWO.Ui.Services
{
    /// <summary>
    /// Configured actions of the request and implementation task scope, offered as buttons on single tasks
    /// in the access task, list and planning views.
    /// </summary>
    public static class WorkflowTaskActions
    {
        /// <summary>
        /// Returns the actions offered for the state of a task. State changing actions are only returned if the task
        /// may be changed; read-only actions are always returned.
        /// </summary>
        /// <param name="wfHandler">The workflow handler providing the action handler and the phase.</param>
        /// <param name="task">The task whose state defines the actions.</param>
        /// <param name="scope">The scope of the task.</param>
        /// <param name="canChangeTask">Whether the user may change the task in the current phase.</param>
        public static IReadOnlyList<WfStateAction> Visible(WfHandler wfHandler, WfStatefulObject task, WfObjectScopes scope, bool canChangeTask)
        {
            if (wfHandler.ActionHandler == null)
            {
                return [];
            }
            return [.. wfHandler.ActionHandler.GetOfferedActions(task, scope, wfHandler.Phase)
                .Where(action => WfStateAction.IsReadonlyType(action.ActionType)
                    || (canChangeTask && wfHandler.CanPerformActionOnActTicket(action)))];
        }

        /// <summary>
        /// Performs an action on each target and reloads the ticket afterwards, also if the action fails for a target:
        /// the targets before it may already be changed and must not be shown (and changed again) in their old state.
        /// </summary>
        /// <param name="wfHandler">The workflow handler providing the action handler.</param>
        /// <param name="action">The action to perform.</param>
        /// <param name="scope">The scope of the targets.</param>
        /// <param name="targets">The tasks with the owner and ticket id passed to the action.</param>
        public static Task Perform(WfHandler wfHandler, WfStateAction action, WfObjectScopes scope,
            IEnumerable<(WfStatefulObject Task, FwoOwner? Owner, long TicketId)> targets)
        {
            return RunAndReload(wfHandler, async actionHandler =>
            {
                foreach ((WfStatefulObject task, FwoOwner? owner, long ticketId) in targets)
                {
                    await actionHandler.PerformAction(action, task, scope, owner, ticketId);
                }
                return true;
            });
        }

        /// <summary>
        /// Runs actions if an action handler exists and reloads the ticket afterwards, also if an action fails.
        /// </summary>
        /// <param name="wfHandler">The workflow handler providing the action handler.</param>
        /// <param name="performActions">Performs the actions with the action handler; returns false if the ticket is known
        /// to be no longer visible, so a reload would only fetch it to filter it out again.</param>
        private static async Task RunAndReload(WfHandler wfHandler, Func<ActionHandler, Task<bool>> performActions)
        {
            if (wfHandler.ActionHandler == null)
            {
                return;
            }
            bool reload = true;
            try
            {
                reload = await performActions(wfHandler.ActionHandler);
            }
            finally
            {
                if (reload)
                {
                    // reports its own errors, so the error of the action is not hidden
                    await wfHandler.ReloadActTicket();
                }
            }
        }

        /// <summary>Returns whether the user may change all request tasks of a group (a single task or a bundle displayed as one).</summary>
        /// <param name="wfHandler">The workflow handler providing the read-only mode and the phase.</param>
        /// <param name="taskGroup">The request tasks.</param>
        /// <remarks>Not on an outdated ticket, whose states the change would be based on.</remarks>
        public static bool CanChangeRequestTasks(WfHandler wfHandler, IReadOnlyList<WfReqTask> taskGroup)
        {
            return taskGroup.Count > 0 && !wfHandler.ReadOnlyMode && !wfHandler.ActTicketOutdated
                && taskGroup.All(wfHandler.CanActOnReqTaskInCurrentPhase);
        }

        /// <summary>Returns whether the user may change an implementation task and its request task.</summary>
        /// <param name="wfHandler">The workflow handler providing the read-only mode and the phase.</param>
        /// <param name="implementationTask">The implementation task.</param>
        /// <param name="requestTask">The request task the implementation task belongs to.</param>
        /// <remarks>Not on an outdated ticket, whose states the change would be based on.</remarks>
        public static bool CanChangeImplementationTask(WfHandler wfHandler, WfImplTask implementationTask, WfReqTask requestTask)
        {
            return !wfHandler.ReadOnlyMode
                && !wfHandler.ActTicketOutdated
                && wfHandler.CanActOnReqTaskInCurrentPhase(requestTask)
                && wfHandler.CanActOnImplTaskInCurrentPhase(implementationTask);
        }

        /// <summary>Returns the actions offered for a group of request tasks, defined by the state of its first task.</summary>
        /// <param name="wfHandler">The workflow handler providing the action handler and the phase.</param>
        /// <param name="taskGroup">The request tasks: a single task or a bundle displayed as one.</param>
        public static IReadOnlyList<WfStateAction> VisibleForRequestTasks(WfHandler wfHandler, IReadOnlyList<WfReqTask> taskGroup)
        {
            return taskGroup.Count == 0
                ? []
                : Visible(wfHandler, taskGroup[0], WfObjectScopes.RequestTask, CanChangeRequestTasks(wfHandler, taskGroup));
        }

        /// <summary>Returns the actions offered for an implementation task.</summary>
        /// <param name="wfHandler">The workflow handler providing the action handler and the phase.</param>
        /// <param name="implementationTask">The implementation task.</param>
        /// <param name="requestTask">The request task the implementation task belongs to.</param>
        public static IReadOnlyList<WfStateAction> VisibleForImplementationTask(WfHandler wfHandler, WfImplTask implementationTask, WfReqTask requestTask)
        {
            return Visible(wfHandler, implementationTask, WfObjectScopes.ImplementationTask,
                CanChangeImplementationTask(wfHandler, implementationTask, requestTask));
        }

        /// <summary>
        /// Performs an action on every request task of a group and reloads the ticket afterwards. Promoting one task of a
        /// bundle moves its lagging bundled tasks along, so a task whose state changed while the action ran on the
        /// previous tasks is skipped instead of being promoted a second time. A state changing action delegated to the
        /// middleware changes only the stored states, so the ticket is reloaded before each further task to see them. If that
        /// reload fails (read error, or the ticket is no longer visible to the user), the remaining tasks are skipped, as
        /// their states are unknown and sending them would be rejected, and the user is told which tasks were skipped. After
        /// a read error the final reload is still tried: after a passing error it brings the ticket up to date; if the error
        /// lasts, it is reported once more. A ticket no longer visible is not fetched again. A ticket left outdated offers
        /// no further state change (see <see cref="WfHandler.ActTicketOutdated"/>).
        /// </summary>
        /// <param name="wfHandler">The workflow handler providing the action handler.</param>
        /// <param name="action">The action to perform.</param>
        /// <param name="taskGroup">The request tasks: a single task or a bundle displayed as one.</param>
        public static Task PerformOnRequestTasks(WfHandler wfHandler, WfStateAction action, IReadOnlyList<WfReqTask> taskGroup)
        {
            List<int> initialStateIds = [.. taskGroup.Select(task => task.StateId)];
            bool reloadBetweenTasks = wfHandler.MiddlewareClient != null && !WfStateAction.IsReadonlyType(action.ActionType);
            return RunAndReload(wfHandler, async actionHandler =>
            {
                for (int index = 0; index < taskGroup.Count; index++)
                {
                    WfReqTask task = taskGroup[index];
                    if (index > 0 && reloadBetweenTasks)
                    {
                        TicketReloadResult reload = await wfHandler.TryReloadActTicket();
                        if (reload != TicketReloadResult.Refreshed)
                        {
                            ReportSkippedTasks(wfHandler, action, taskGroup.Skip(index));
                            return reload != TicketReloadResult.NotVisible;
                        }
                    }
                    if (CurrentStateId(wfHandler, task) == initialStateIds[index])
                    {
                        await actionHandler.PerformAction(action, task, WfObjectScopes.RequestTask, task.Owners.FirstOrDefault()?.Owner, task.TicketId);
                    }
                }
                return true;
            });
        }

        /// <summary>
        /// Tells the user that an action was not sent separately for the remaining tasks of a bundle; they may have moved
        /// with the bundle anyway. A failed reload is only reported (by the db access) if the ticket can not be read, not
        /// if it is filtered out, so this is the only hint that the bundle may have moved only partly.
        /// </summary>
        /// <param name="wfHandler">The workflow handler displaying the message.</param>
        /// <param name="action">The action, whose name is the message title.</param>
        /// <param name="skippedTasks">The tasks the action was not sent for.</param>
        private static void ReportSkippedTasks(WfHandler wfHandler, WfStateAction action, IEnumerable<WfReqTask> skippedTasks)
        {
            string taskIds = string.Join(", ", skippedTasks.Select(task => task.Id));
            wfHandler.DisplayMessage(null, action.Name, string.Format(wfHandler.userConfig.GetText("planning_bundle_action_stopped"), taskIds), true);
        }

        /// <summary>The state of a request task in the current ticket, which may hold a replaced instance of the task.</summary>
        private static int CurrentStateId(WfHandler wfHandler, WfReqTask task)
        {
            return wfHandler.ActTicket.Tasks.FirstOrDefault(candidate => candidate.Id == task.Id)?.StateId ?? task.StateId;
        }

        /// <summary>Performs an action on an implementation task and reloads the ticket afterwards.</summary>
        /// <param name="wfHandler">The workflow handler providing the action handler.</param>
        /// <param name="action">The action to perform.</param>
        /// <param name="implementationTask">The implementation task.</param>
        /// <param name="requestTask">The request task providing owner and ticket.</param>
        public static Task PerformOnImplementationTask(WfHandler wfHandler, WfStateAction action, WfImplTask implementationTask, WfReqTask requestTask)
        {
            return Perform(wfHandler, action, WfObjectScopes.ImplementationTask,
                [(implementationTask, requestTask.Owners.FirstOrDefault()?.Owner, requestTask.TicketId)]);
        }

        /// <summary>Renders the action buttons; renders nothing if no action is offered.</summary>
        /// <param name="actions">The actions to offer.</param>
        /// <param name="perform">Performs a selected action.</param>
        public static RenderFragment Buttons(IReadOnlyList<WfStateAction> actions, Func<WfStateAction, Task> perform) => builder =>
        {
            if (actions.Count == 0)
            {
                return;
            }
            builder.OpenComponent<ConfiguredTaskActions>(0);
            builder.AddComponentParameter(1, nameof(ConfiguredTaskActions.OfferedActions), actions);
            builder.AddComponentParameter(2, nameof(ConfiguredTaskActions.ActionVisibility), (Func<WfStateAction, bool>)(_ => true));
            builder.AddComponentParameter(3, nameof(ConfiguredTaskActions.ActionRequested), perform);
            builder.CloseComponent();
        };
    }
}
