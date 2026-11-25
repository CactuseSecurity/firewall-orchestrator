using FWO.Data;
using FWO.Data.Workflow;
using FWO.Services.Workflow;

namespace FWO.Ui.Services
{
    /// <summary>Callbacks of the host of a <see cref="PlanningImplTaskEditor"/>.</summary>
    public sealed class PlanningImplTaskEditorCallbacks
    {
        /// <summary>Returns the current workflow handler of the host.</summary>
        public Func<WfHandler> WfHandler { get; init; } = () => new();
        /// <summary>Returns the display logic of the implementation tasks, built from the current data of the host.</summary>
        public Func<PlanningImplTaskDisplayLogic> DisplayLogic { get; init; } = () => throw new InvalidOperationException("No display logic configured.");
        /// <summary>Determines whether the user may change an implementation task of a request task.</summary>
        public Func<WfImplTask, WfReqTask, bool> CanEdit { get; init; } = (_, _) => false;
        /// <summary>Saves the changed selected implementation task (<see cref="WfHandler.ActImplTask"/>).</summary>
        public Func<Task> Save { get; init; } = () => Task.CompletedTask;
        /// <summary>Renders the host again, so an opened editor is shown.</summary>
        public Func<Task> Refresh { get; init; } = () => Task.CompletedTask;
    }

    /// <summary>
    /// Editor state of the management, device, free text and name of implementation tasks in the planning views: opens
    /// the editor for a task, preselects its current value and applies and saves the edited value.
    /// </summary>
    public sealed class PlanningImplTaskEditor
    {
        private readonly PlanningImplTaskEditorCallbacks callbacks;
        private bool taskSelected;

        /// <summary>Initializes the editor.</summary>
        /// <param name="callbacks">The callbacks of the host.</param>
        public PlanningImplTaskEditor(PlanningImplTaskEditorCallbacks callbacks)
        {
            this.callbacks = callbacks;
        }

        /// <summary>The request task the edited implementation task belongs to.</summary>
        public WfReqTask RequestTask { get; private set; } = new();
        /// <summary>The management preselected in the management editor.</summary>
        public Management? SelectedManagement { get; private set; }
        /// <summary>The device preselected in the device editor.</summary>
        public Device? SelectedDevice { get; private set; }
        /// <summary>The free text shown in the free-text editor.</summary>
        public string FreeText { get; private set; } = "";
        /// <summary>The name shown in the name editor.</summary>
        public string Name { get; private set; } = "";
        /// <summary>Shows the management editor.</summary>
        public bool DisplayManagementEditor { get; set; }
        /// <summary>Shows the device editor.</summary>
        public bool DisplayDeviceEditor { get; set; }
        /// <summary>Shows the free-text editor.</summary>
        public bool DisplayFreeTextEditor { get; set; }
        /// <summary>Shows the name editor.</summary>
        public bool DisplayNameEditor { get; set; }

        /// <summary>The selectable managements.</summary>
        public List<Management> Managements => callbacks.WfHandler().Managements;

        /// <summary>The devices selectable for the edited implementation task: those of its effective management, or all devices.</summary>
        public List<Device> AvailableDevices() => callbacks.DisplayLogic().AvailableDevices(callbacks.WfHandler().ActImplTask, RequestTask);

        /// <summary>Opens the management editor, preselecting the effective management of the task.</summary>
        /// <param name="implementationTask">The implementation task.</param>
        /// <param name="requestTask">The request task the implementation task belongs to.</param>
        public Task OpenManagement(WfImplTask implementationTask, WfReqTask requestTask)
        {
            if (!Select(implementationTask, requestTask))
            {
                return Task.CompletedTask;
            }
            int? managementId = callbacks.DisplayLogic().EffectiveManagementId(implementationTask, requestTask);
            SelectedManagement = Managements.FirstOrDefault(management => management.Id == managementId);
            DisplayManagementEditor = true;
            return callbacks.Refresh();
        }

        /// <summary>Opens the device editor, preselecting the device of the task.</summary>
        /// <param name="implementationTask">The implementation task.</param>
        /// <param name="requestTask">The request task the implementation task belongs to.</param>
        public Task OpenDevice(WfImplTask implementationTask, WfReqTask requestTask)
        {
            if (!Select(implementationTask, requestTask))
            {
                return Task.CompletedTask;
            }
            SelectedDevice = callbacks.DisplayLogic().FindDevice(implementationTask.DeviceId);
            DisplayDeviceEditor = true;
            return callbacks.Refresh();
        }

        /// <summary>Opens the free-text editor with the free text of the task.</summary>
        /// <param name="implementationTask">The implementation task.</param>
        /// <param name="requestTask">The request task the implementation task belongs to.</param>
        public Task OpenFreeText(WfImplTask implementationTask, WfReqTask requestTask)
        {
            if (!Select(implementationTask, requestTask))
            {
                return Task.CompletedTask;
            }
            FreeText = implementationTask.FreeText ?? "";
            DisplayFreeTextEditor = true;
            return callbacks.Refresh();
        }

        /// <summary>Opens the name editor with the name of the group, object or time object of the task.</summary>
        /// <param name="implementationTask">The implementation task.</param>
        /// <param name="requestTask">The request task the implementation task belongs to.</param>
        public Task OpenName(WfImplTask implementationTask, WfReqTask requestTask)
        {
            if (!Select(implementationTask, requestTask))
            {
                return Task.CompletedTask;
            }
            Name = PlanningImplTaskDisplayLogic.EditableName(implementationTask, requestTask);
            DisplayNameEditor = true;
            return callbacks.Refresh();
        }

        /// <summary>Sets the management, removing a device of another management, and saves the task.</summary>
        /// <param name="management">The selected management.</param>
        public async Task SaveManagement(Management? management)
        {
            callbacks.DisplayLogic().ApplyManagement(callbacks.WfHandler().ActImplTask, RequestTask, management);
            await Save();
        }

        /// <summary>Sets the device and saves the task; a device of another management is rejected.</summary>
        /// <param name="device">The selected device.</param>
        public async Task SaveDevice(Device? device)
        {
            if (PlanningImplTaskDisplayLogic.ApplyDevice(callbacks.WfHandler().ActImplTask, RequestTask, device))
            {
                await Save();
            }
        }

        /// <summary>Sets the free text and saves the task.</summary>
        /// <param name="freeText">The edited free text.</param>
        public async Task SaveFreeText(string freeText)
        {
            callbacks.WfHandler().ActImplTask.FreeText = freeText;
            await Save();
        }

        /// <summary>Sets the name of the group, object or time object and saves the task.</summary>
        /// <param name="name">The edited name.</param>
        public async Task SaveName(string name)
        {
            PlanningImplTaskDisplayLogic.ApplyName(callbacks.WfHandler().ActImplTask, name);
            await Save();
        }

        /// <summary>Selects the task for editing if the user may change it.</summary>
        private bool Select(WfImplTask implementationTask, WfReqTask requestTask)
        {
            if (!callbacks.CanEdit(implementationTask, requestTask))
            {
                return false;
            }
            RequestTask = requestTask;
            callbacks.WfHandler().SelectImplTask(implementationTask, ObjAction.edit);
            taskSelected = true;
            return true;
        }

        /// <summary>
        /// Saves the task selected by opening an editor, if it may still be changed and its device belongs to its management.
        /// </summary>
        private async Task Save()
        {
            if (!taskSelected)
            {
                return;
            }
            WfImplTask implementationTask = callbacks.WfHandler().ActImplTask;
            Device? device = callbacks.DisplayLogic().FindDevice(implementationTask.DeviceId);
            if (!callbacks.CanEdit(implementationTask, RequestTask)
                || !PlanningImplTaskDisplayLogic.IsDeviceCompatible(implementationTask, RequestTask, device))
            {
                return;
            }
            await callbacks.Save();
        }
    }
}
