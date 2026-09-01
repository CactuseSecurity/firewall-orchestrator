namespace FWO.Data
{
    /// <summary>
    /// Identifies central change-history subjects which are not modelling objects.
    /// </summary>
    public enum ChangeHistoryObjectType
    {
        Ticket = 100,
        RequestTask = 101,
        ImplementationTask = 102,
        Approval = 103
    }
}
