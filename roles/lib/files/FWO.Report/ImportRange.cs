namespace FWO.Report
{
    /// <summary>
    /// Range of imports of one management a report was generated for.
    /// </summary>
    /// <param name="Start">Last import before the report period, for a report of one point in time the import valid at this time.</param>
    /// <param name="End">Last import of the report period.</param>
    public readonly record struct ImportRange(long Start, long End);
}
