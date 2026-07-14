namespace FWO.Data.Logging
{
    public sealed record ManagementPromptLogRequest
    {
        public required PromptLogEvent PromptEvent { get; init; }
        public required ChangeLogOperation Operation { get; init; }
        public required string UserId { get; init; }
        public required ChangeLogOrigin Origin { get; init; }
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;
        public int? ManagementId { get; init; }
        public string? ManagementName { get; init; }

        public PromptLogRequest ToPromptLogRequest()
        {
            return new PromptLogRequest
            {
                PromptEvent = PromptEvent,
                Object = ChangeLogObject.Management,
                Operation = Operation,
                UserId = UserId,
                Origin = Origin,
                Timestamp = Timestamp,
                Fields =
                [
                    ("Management ID", ManagementId),
                    ("Management Name", ManagementName)
                ]
            };
        }
    }
}
