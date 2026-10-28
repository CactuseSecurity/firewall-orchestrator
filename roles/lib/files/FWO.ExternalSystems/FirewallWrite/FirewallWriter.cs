using FWO.Basics.Exceptions;
using FWO.Data.DeviceSpecificSettings;
using FWO.Data.Workflow;
using FWO.Logging;

namespace FWO.ExternalSystems.FirewallWrite
{
    /// <summary>
    /// Writes implementation tasks to firewalls. The calls are built from the endpoints and body templates of the device
    /// specific settings (or the defaults shipped for the device type), all calls are rendered and checked before the
    /// first one is sent, and they run in one vendor session including publish, optional install, discard on error and logout.
    /// Deletions of objects, services and groups are only sent if the device specific setting <c>fwWriteDeleteObjects</c>
    /// allows them, because these may still be used elsewhere on the firewall; rule deletions are always sent.
    /// </summary>
    public sealed class FirewallWriter
    {
        private const string kLogCategory = "Firewall Write";

        private static readonly HashSet<FirewallWriteOperation> ObjectDeletions =
        [
            FirewallWriteOperation.HostDelete, FirewallWriteOperation.NetworkDelete, FirewallWriteOperation.AddressRangeDelete,
            FirewallWriteOperation.ServiceDelete, FirewallWriteOperation.GroupDelete
        ];

        /// <summary>The calls to send and the object deletions left out because deleting objects is switched off.</summary>
        private sealed record RenderedTask(List<RenderedFirewallRequest> Requests, List<string> SkippedDeletions);

        private readonly IFirewallWriteDataSource dataSource;
        private readonly IFirewallWriteClientFactory clientFactory;
        private readonly IFirewallRulePlacementProvider placementProvider;

        /// <summary>Creates a writer.</summary>
        /// <param name="dataSource">Loads management, gateway and settings of a task.</param>
        /// <param name="clientFactory">Creates the vendor clients; defaults to <see cref="FirewallWriteClientFactory"/>.</param>
        /// <param name="placementProvider">Supplies rule positions; defaults to the end of the rulebase.</param>
        public FirewallWriter(IFirewallWriteDataSource dataSource, IFirewallWriteClientFactory? clientFactory = null,
            IFirewallRulePlacementProvider? placementProvider = null)
        {
            this.dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
            this.clientFactory = clientFactory ?? new FirewallWriteClientFactory();
            this.placementProvider = placementProvider ?? new EndOfRulebasePlacementProvider();
        }

        /// <summary>
        /// Writes one implementation task to its management and gateway. Never throws: configuration, template and data
        /// errors are reported before anything is sent, firewall errors with the results of the calls sent so far.
        /// </summary>
        public async Task<ModifyFirewallResult> ModifyFirewall(WfImplTask implTask)
        {
            ArgumentNullException.ThrowIfNull(implTask);
            RenderedTask rendered;
            FirewallWriteContext context;
            FirewallWriteClient client;
            try
            {
                context = await dataSource.LoadContext(implTask);
                if (context.Vendor == FirewallVendor.Unsupported)
                {
                    return ModifyFirewallResult.Failed($"Device type {context.DeviceTypeId} of management '{context.Management.Name}' is not supported for firewall writes.");
                }
                rendered = RenderTask(implTask, context);
                if (rendered.Requests.Count == 0)
                {
                    return ModifyFirewallResult.Succeeded("Nothing to write." + SkippedNote(rendered.SkippedDeletions), new List<FirewallStepResult>(),
                        rendered.SkippedDeletions);
                }
                client = clientFactory.Create(context);
            }
            catch (Exception exception)
            {
                Log.WriteWarning(kLogCategory, $"Implementation task {implTask.Id} not written: {exception.Message}");
                return ModifyFirewallResult.Failed(exception.Message);
            }
            return await Execute(client, rendered, context);
        }

        /// <summary>
        /// Plans and renders all calls of a task, including publish and install, without the object deletions that the
        /// settings do not allow. Fails if an operation has no endpoint for the device type, a placeholder stays unresolved
        /// or a body is no valid JSON.
        /// </summary>
        public List<RenderedFirewallRequest> RenderAll(WfImplTask implTask, FirewallWriteContext context) => RenderTask(implTask, context).Requests;

        private RenderedTask RenderTask(WfImplTask implTask, FirewallWriteContext context)
        {
            ArgumentNullException.ThrowIfNull(implTask);
            ArgumentNullException.ThrowIfNull(context);
            FirewallRulePlacement? placement = placementProvider.GetPlacement(implTask);
            List<FirewallWriteStep> planned = ImplTaskStepPlanner.Plan(implTask, context, placement);
            List<FirewallWriteStep> skipped = context.Settings.FwWriteDeleteObjects
                ? []
                : [.. planned.Where(step => ObjectDeletions.Contains(step.Operation))];
            List<string> skippedDeletions = [.. skipped.Select(step => $"{step.Operation} '{step.TargetName}'")];
            List<FirewallWriteStep> steps = [.. planned.Except(skipped)];
            if (steps.Count == 0)
            {
                return new RenderedTask(new List<RenderedFirewallRequest>(), skippedDeletions);
            }

            IReadOnlyDictionary<string, FirewallPlaceholderValue> contextValues = ImplTaskStepPlanner.ContextValues(implTask, context, placement);
            AddSessionStep(steps, FirewallWriteOperation.Publish, context, contextValues);
            if (context.Settings.FwWriteInstallPolicy)
            {
                AddSessionStep(steps, FirewallWriteOperation.InstallPolicy, context, contextValues);
            }
            List<RenderedFirewallRequest> requests = [.. steps.Select(step => Render(step, context))];
            return new RenderedTask(requests, skippedDeletions);
        }

        private static string SkippedNote(List<string> skippedDeletions) => skippedDeletions.Count == 0
            ? ""
            : $" Not deleted on the firewall, because deleting objects is switched off: {string.Join(", ", skippedDeletions)}.";

        private static void AddSessionStep(List<FirewallWriteStep> steps, FirewallWriteOperation operation, FirewallWriteContext context,
            IReadOnlyDictionary<string, FirewallPlaceholderValue> contextValues)
        {
            if (!string.IsNullOrWhiteSpace(context.GetEndpoint(operation)))
            {
                steps.Add(new FirewallWriteStep(operation) { TargetName = context.Gateway?.Name ?? context.Management.Name }.AddMissing(contextValues));
            }
        }

        private static RenderedFirewallRequest Render(FirewallWriteStep step, FirewallWriteContext context)
        {
            string endpoint = context.GetEndpoint(step.Operation, step.IsIpv6);
            if (string.IsNullOrWhiteSpace(endpoint))
            {
                throw new ConfigException($"{step.Operation} is not supported for device type {context.DeviceTypeId}: no endpoint configured.");
            }
            return FirewallTemplateRenderer.Render(step, endpoint, context.GetTemplate(step.Operation, step.IsIpv6));
        }

        private static async Task<ModifyFirewallResult> Execute(FirewallWriteClient client, RenderedTask rendered, FirewallWriteContext context)
        {
            List<string> skipped = rendered.SkippedDeletions;
            List<FirewallStepResult> results = [];
            bool completed = false;
            try
            {
                await client.Login();
                foreach (RenderedFirewallRequest request in rendered.Requests)
                {
                    FirewallStepResult result = await client.Execute(request);
                    results.Add(result);
                    if (!result.Success)
                    {
                        return ModifyFirewallResult.Failed($"{result.Operation} on '{result.Target}' failed with status {result.StatusCode}.", results, skipped);
                    }
                }
                completed = true;
                return ModifyFirewallResult.Succeeded($"{results.Count} call(s) written to '{context.Management.Name}'.{SkippedNote(skipped)}", results, skipped);
            }
            catch (Exception exception)
            {
                Log.WriteError(kLogCategory, $"Writing to '{context.Management.Name}' failed.", exception);
                return ModifyFirewallResult.Failed(exception.Message, results, skipped);
            }
            finally
            {
                await CloseSession(client, completed);
            }
        }

        private static async Task CloseSession(FirewallWriteClient client, bool completed)
        {
            try
            {
                if (!completed)
                {
                    await client.Discard();
                }
                await client.Logout();
            }
            catch (Exception exception)
            {
                Log.WriteWarning(kLogCategory, $"Closing the firewall session failed: {exception.Message}");
            }
        }
    }
}
