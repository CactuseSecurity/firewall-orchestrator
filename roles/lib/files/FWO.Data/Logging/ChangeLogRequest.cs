namespace FWO.Data.Logging
{
    public sealed record ChangeLogRequest
    {
        public required ChangeLogFamily Family { get; init; }
        public required ChangeLogObject Object { get; init; }
        public required ChangeLogOperation Operation { get; init; }
        public required string UserId { get; init; }
        public required ChangeLogOrigin Origin { get; init; }
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;
        public (string Key, object? Value)[] Fields { get; init; } = [];
    }
}
