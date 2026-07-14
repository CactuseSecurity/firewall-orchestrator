namespace FWO.Data.Flow
{
    public static class FlowAccessInsertHelper
    {
        public static FlowAccessInsertMembersContainer BuildMembersContainer<T>(IEnumerable<T> items) where T : class
        {
            return new FlowAccessInsertMembersContainer { Data = [.. items.Cast<object>()] };
        }
    }
}
