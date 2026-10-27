using FWO.Data.DeviceSpecificSettings;

namespace FWO.ExternalSystems.FirewallWrite
{
    /// <summary>Outcome of one firewall write call.</summary>
    public sealed record FirewallStepResult(FirewallWriteOperation Operation, string Target, bool Success, int StatusCode, string Message);

    /// <summary>
    /// Outcome of <see cref="FirewallWriter.ModifyFirewall"/>: overall success, a message for the user and the result of
    /// every call sent. A failure before the first call (configuration, template or data error) has no step results.
    /// </summary>
    public sealed class ModifyFirewallResult
    {
        public bool Success { get; init; }

        public string Message { get; init; } = "";

        public List<FirewallStepResult> Steps { get; init; } = [];

        /// <summary>Creates a successful result.</summary>
        public static ModifyFirewallResult Succeeded(string message, List<FirewallStepResult> steps) =>
            new() { Success = true, Message = message, Steps = steps };

        /// <summary>Creates a failed result.</summary>
        public static ModifyFirewallResult Failed(string message, List<FirewallStepResult>? steps = null) =>
            new() { Success = false, Message = message, Steps = steps ?? [] };
    }
}
