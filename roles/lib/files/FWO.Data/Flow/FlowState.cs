namespace FWO.Data.Flow
{
    public static class FlowState
    {
        public const string Requested = "requested";
        public const string Denied = "denied";
        public const string Implemented = "implemented";
        public const string Removed = "removed";

        public static readonly IReadOnlyList<string> All =
        [
            Requested,
            Denied,
            Implemented,
            Removed
        ];
    }
}
