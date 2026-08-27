namespace FWO.Data
{
    /// <summary>
    /// Shared numeric values for <c>stm_link_type</c> / <see cref="RulebaseLink.LinkType"/> as
    /// assigned by the importer. Kept in one place so the rule-tree builder and report-time
    /// filters (e.g. <see cref="RulebaseLink"/> queries) cannot drift apart from the importer's
    /// contract.
    /// </summary>
    public static class RulebaseLinkTypes
    {
        public const int Ordered = 2;
        public const int Inline = 3;
        public const int Concatenated = 4;
        public const int Domain = 5;
        public const int Nat = 6;
        public const int Policy = 7;
    }
}
