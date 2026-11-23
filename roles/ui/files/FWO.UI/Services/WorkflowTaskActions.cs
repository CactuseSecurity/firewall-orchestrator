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

        /// <summary>Performs an action on each target and reloads the ticket afterwards.</summary>
        /// <param name="wfHandler">The workflow handler providing the action handler.</param>
        /// <param name="action">The action to perform.</param>
        /// <param name="scope">The scope of the targets.</param>
        /// <param name="targets">The tasks with the owner and ticket id passed to the action.</param>
        public static async Task Perform(WfHandler wfHandler, WfStateAction action, WfObjectScopes scope,
            IEnumerable<(WfStatefulObject Task, FwoOwner? Owner, long TicketId)> targets)
        {
            if (wfHandler.ActionHandler == null)
            {
                return;
            }
            foreach ((WfStatefulObject task, FwoOwner? owner, long ticketId) in targets)
            {
                await wfHandler.ActionHandler.PerformAction(action, task, scope, owner, ticketId);
            }
            await wfHandler.ReloadActTicket();
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
