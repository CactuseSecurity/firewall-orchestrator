namespace FWO.Ai.Harness.Agent;

/// <summary>
/// A run was refused because the AI function it belongs to is switched off in the AI settings. The AG-UI hosting
/// reports it to the client as run error without details, like any other failed run.
/// </summary>
public sealed class AgentFeatureDisabledException : InvalidOperationException
{
    /// <summary>
    /// Creates the exception for the given AI function.
    /// </summary>
    /// <param name="feature">The AI function that is switched off</param>
    public AgentFeatureDisabledException(AgentFeature feature)
        : base($"The AI function {feature} is switched off in the AI settings.")
    {
        Feature = feature;
    }

    /// <summary>The AI function that is switched off.</summary>
    public AgentFeature Feature { get; }
}
