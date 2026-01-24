namespace FWO.Middleware.Server.Services
{
    /// <summary>
    /// Options for configuring Quartz scheduler service metadata.
    /// </summary>
    public sealed record QuartzSchedulerOptions(
        string SchedulerName,
        string JobKeyName,
        string TriggerKeyName,
        string ConfigSubscriptionQuery);
}
