namespace FWO.Data
{
    /// <summary>
    /// Identifies central change-history subjects which are not modelling objects.
    /// </summary>
    /// <remarks>
    /// These values share the change_history.object_type column with
    /// <see cref="FWO.Data.Modelling.ModellingTypes.ModObjectType"/>. The two enums are disjoint by
    /// convention: modelling object types stay below 100, workflow object types start at 100.
    /// A reader selects the right enum via the change_history.module column, which is
    /// <see cref="FWO.Basics.GlobalConst.kModuleWorkflow"/> for these values. Do not use
    /// change_source for that: it carries an import source name chosen by the customer.
    /// Keep new members at 100 or above.
    /// </remarks>
    public enum ChangeHistoryObjectType
    {
        Ticket = 100,
        RequestTask = 101,
        ImplementationTask = 102,
        Approval = 103
    }
}
