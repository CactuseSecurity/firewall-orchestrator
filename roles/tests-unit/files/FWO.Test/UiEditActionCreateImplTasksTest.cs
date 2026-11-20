using Bunit;
using FWO.Config.Api;
using FWO.Data.Workflow;
using FWO.Ui.Pages.Settings;
using FWO.Ui.Services;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Reflection;

namespace FWO.Test
{
    /// <summary>
    /// Tests the editor of the action "Prepare implementation": the target state for request tasks of an external
    /// ticket system defaults to "To Implement" and is stored with the action.
    /// </summary>
    [TestFixture]
    internal class UiEditActionCreateImplTasksTest
    {
        private static readonly WfState kPlanned = new() { Id = 149, Name = "Planned" };
        private static readonly WfState kToImplement = new() { Id = PrepareImplementationActionParams.kToImplementStateId, Name = "To Implement" };
        private static readonly List<WfState> kStates = [kPlanned, kToImplement];

        private static BunitContext CreateContext()
        {
            BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddSingleton<UserConfig>(new SimulatedUserConfig());
            context.Services.AddSingleton<DomEventService>();
            return context;
        }

        private static IRenderedComponent<EditActionCreateImplTasks> Render(BunitContext context, WfStateAction action)
        {
            return context.Render<EditActionCreateImplTasks>(parameters => parameters
                .Add(component => component.ActAction, action)
                .Add(component => component.States, kStates));
        }

        private static WfState? SelectedState(EditActionCreateImplTasks component)
        {
            FieldInfo field = typeof(EditActionCreateImplTasks).GetField("selectedTargetState", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingFieldException(nameof(EditActionCreateImplTasks), "selectedTargetState");
            return field.GetValue(component) as WfState;
        }

        [Test]
        public async Task NewAction_StoresTheDefaultToImplement()
        {
            await using BunitContext context = CreateContext();
            WfStateAction action = new() { ActionType = nameof(StateActionTypes.CreateImplTasks) };

            IRenderedComponent<EditActionCreateImplTasks> rendered = Render(context, action);

            Assert.Multiple(() =>
            {
                Assert.That(PrepareImplementationActionParams.FromExternalParams(action.ExternalParams).ExternalTicketSystemTargetStateId,
                    Is.EqualTo(kToImplement.Id));
                Assert.That(SelectedState(rendered.Instance), Is.SameAs(kToImplement));
            });
        }

        [Test]
        public async Task StoredTarget_IsSelected()
        {
            await using BunitContext context = CreateContext();
            WfStateAction action = new()
            {
                ActionType = nameof(StateActionTypes.CreateImplTasks),
                ExternalParams = new PrepareImplementationActionParams { ExternalTicketSystemTargetStateId = kPlanned.Id }.ToExternalParams()
            };

            IRenderedComponent<EditActionCreateImplTasks> rendered = Render(context, action);

            Assert.That(SelectedState(rendered.Instance), Is.SameAs(kPlanned));
        }

        [Test]
        public async Task ChangedTarget_IsStoredWithTheAction()
        {
            await using BunitContext context = CreateContext();
            WfStateAction action = new() { ActionType = nameof(StateActionTypes.CreateImplTasks) };
            IRenderedComponent<EditActionCreateImplTasks> rendered = Render(context, action);
            MethodInfo onChanged = typeof(EditActionCreateImplTasks).GetMethod("OnTargetStateChanged", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMethodException(nameof(EditActionCreateImplTasks), "OnTargetStateChanged");
            object?[] arguments = [kPlanned];

            await rendered.InvokeAsync(() => (Task)onChanged.Invoke(rendered.Instance, arguments)!);

            Assert.That(PrepareImplementationActionParams.FromExternalParams(action.ExternalParams).ExternalTicketSystemTargetStateId,
                Is.EqualTo(kPlanned.Id));
        }
    }
}
