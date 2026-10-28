using FWO.Data.DeviceSpecificSettings;

namespace FWO.ExternalSystems.FirewallWrite
{
    /// <summary>Outcome of one firewall write call: the firewall's answer and, if the call failed, its classified error.</summary>
    public sealed record FirewallStepResult(FirewallWriteOperation Operation, string Target, bool Success, int StatusCode, string Message,
        FirewallWriteError? Error = null);

    /// <summary>
    /// Outcome of <see cref="FirewallWriter.ModifyFirewall"/>: overall success, a message for the user and the result of
    /// every call sent. A failure before the first call (configuration, template or data error) has no step results. A
    /// failure carries a vendor-independent <see cref="Error"/> with the firewall's native message, from which the caller
    /// decides whether to try again later (<see cref="IsRetryable"/>) or to fail the task.
    /// </summary>
    public sealed class ModifyFirewallResult
    {
        public bool Success { get; init; }

        public string Message { get; init; } = "";

        public List<FirewallStepResult> Steps { get; init; } = [];

        /// <summary>Deletions of objects, services and groups that were not sent, because deleting objects is switched off.</summary>
        public List<string> SkippedDeletions { get; init; } = [];

        /// <summary>The classified error of a failed write; null on success.</summary>
        public FirewallWriteError? Error { get; init; }

        /// <summary>Whether the failed write may succeed if the task is tried again later; false on success.</summary>
        public bool IsRetryable => Error?.IsRetryable ?? false;

        /// <summary>Creates a successful result.</summary>
        public static ModifyFirewallResult Succeeded(string message, List<FirewallStepResult> steps, List<string>? skippedDeletions = null) =>
            new() { Success = true, Message = message, Steps = steps, SkippedDeletions = skippedDeletions ?? [] };

        /// <summary>Creates a failed result with its classified error.</summary>
        public static ModifyFirewallResult Failed(string message, FirewallWriteError error, List<FirewallStepResult>? steps = null,
            List<string>? skippedDeletions = null) =>
            new() { Success = false, Message = message, Error = error, Steps = steps ?? [], SkippedDeletions = skippedDeletions ?? [] };
    }
}
