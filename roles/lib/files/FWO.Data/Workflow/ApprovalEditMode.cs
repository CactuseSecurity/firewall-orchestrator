namespace FWO.Data.Workflow
{
    /// <summary>Defines how approval-phase ticket editing is presented.</summary>
    public enum ApprovalEditMode
    {
        /// <summary>Uses the existing task editor.</summary>
        Classic,
        /// <summary>Uses the consolidated access-task details view.</summary>
        AccessTaskView,
        /// <summary>Uses the consolidated task view with one request task per table row.</summary>
        ListView
    }
}
