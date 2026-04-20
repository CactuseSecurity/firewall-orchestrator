namespace FWO.Data.Logging
{
    public sealed record PromptLogRequest
    {
        public required PromptLogEvent PromptEvent { get; init; }
        public required ChangeLogObject Object { get; init; }
        public required ChangeLogOperation Operation { get; init; }
        public required string UserId { get; init; }
        public required ChangeLogOrigin Origin { get; init; }
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;
        public (string Key, object? Value)[] Fields { get; init; } = [];
    }
}
