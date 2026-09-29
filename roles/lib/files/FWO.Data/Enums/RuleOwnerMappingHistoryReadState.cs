namespace FWO.Data.Enums
{
    /// <summary>
    /// What came back when the stored rule_owner mapping run history was read. A writer only needs to know
    /// that it must not save, and treats every failure alike; a page that reports the failure has to tell
    /// them apart, because they last for different lengths of time and are repaired by opposite means.
    /// </summary>
    public enum RuleOwnerMappingHistoryReadState
    {
        /// <summary>The entry was read. It can still be empty, because nothing has been recorded yet.</summary>
        Read,

        /// <summary>
        /// The entry could not be fetched. The stored value is untouched and the next read may well succeed,
        /// so resetting the entry here would destroy a healthy history over a passing failure.
        /// </summary>
        NotFetched,

        /// <summary>
        /// The entry was fetched but its value could not be decoded. Because no writer saves over an entry it
        /// could not read, it stays that way and nothing is recorded meanwhile - until it is reset.
        /// </summary>
        NotDecoded
    }
}
