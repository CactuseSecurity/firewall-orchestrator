namespace FWO.Data.Workflow
{
    /// <summary>Defines how planning-phase ticket editing is presented.</summary>
    public enum PlannerEditMode
    {
        /// <summary>Uses the existing ticket and task editors.</summary>
        Classic,
        /// <summary>Uses the consolidated planning view with implementation tasks.</summary>
        PlanningView
    }
}
