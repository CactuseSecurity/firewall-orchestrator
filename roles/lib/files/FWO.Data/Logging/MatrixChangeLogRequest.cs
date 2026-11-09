namespace FWO.Data.Logging
{
    public sealed record MatrixChangeLogRequest
    {
        public required ChangeLogFamily Family { get; init; }
        public required ChangeLogOperation Operation { get; init; }
        public required string UserId { get; init; }
        public required ChangeLogOrigin Origin { get; init; }
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;
        public int? MatrixId { get; init; }
        public string? MatrixName { get; init; }

        public ChangeLogRequest ToChangeLogRequest()
        {
            return new ChangeLogRequest
            {
                Family = Family,
                Object = ChangeLogObject.Matrix,
                Operation = Operation,
                UserId = UserId,
                Origin = Origin,
                Timestamp = Timestamp,
                Fields =
                [
                    ("Matrix ID", MatrixId),
                    ("Matrix Name", MatrixName)
                ]
            };
        }
    }
}
