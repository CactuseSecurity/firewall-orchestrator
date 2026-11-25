namespace FWO.Ai.Harness.Agent;

/// <summary>
/// A run was refused because the user holds none of the roles the AI settings allow to use the AI function the run
/// belongs to. The AG-UI hosting reports it to the client as run error without details, like any other failed run.
/// </summary>
public sealed class AgentFeatureNotPermittedException : InvalidOperationException
{
    /// <summary>
    /// Creates the exception for the given AI function.
    /// </summary>
    /// <param name="feature">The AI function the user may not use</param>
    public AgentFeatureNotPermittedException(AgentFeature feature)
        : base($"The AI function {feature} is not allowed for the roles of the user.")
    {
        Feature = feature;
    }

    /// <summary>The AI function the user may not use.</summary>
    public AgentFeature Feature { get; }
}
