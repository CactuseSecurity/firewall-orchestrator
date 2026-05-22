namespace FWO.Api.Client
{
    public class QueryChunkingOptions
    {
        public bool Enabled { get; set; } = false;
        public string ChunkVariableName { get; set; } = "";
        public int ChunkSize { get; set; } = 500;
    }
}
