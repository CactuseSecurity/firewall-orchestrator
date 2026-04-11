namespace FWO.Data.Logging
{
    public sealed record GatewayChangeLogRequest
    {
        public required ChangeLogFamily Family { get; init; }
        public required ChangeLogOperation Operation { get; init; }
        public required string UserId { get; init; }
        public required ChangeLogOrigin Origin { get; init; }
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;
        public int? DeviceId { get; init; }
        public string? DeviceName { get; init; }
        public int? ManagementId { get; init; }

        public ChangeLogRequest ToChangeLogRequest()
        {
            return new ChangeLogRequest
            {
                Family = Family,
                Object = ChangeLogObject.Gateway,
                Operation = Operation,
                UserId = UserId,
                Origin = Origin,
                Timestamp = Timestamp,
                Fields =
                [
                    ("Device ID", DeviceId),
                    ("Device Name", DeviceName),
                    ("Management ID", ManagementId)
                ]
            };
        }
    }
}
