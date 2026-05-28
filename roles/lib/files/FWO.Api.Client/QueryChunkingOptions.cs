using FWO.Basics;

namespace FWO.Api.Client
{
    public class QueryChunkingOptions
    {
        public bool Enabled { get; set; } = false;

         /// <summary>
         /// Name of the variable that contains the list to split into chunks.
         /// </summary>
         public string ChunkVariableName { get; set; } = "";
 
         /// <summary>
         /// Number of elements per chunk. Must be greater than zero.
         /// </summary>
         public int ChunkSize { get; set; } = 500;
 
         /// <summary>
         /// Defines how responses from multiple chunks are merged.
         /// None allows chunking only when the payload fits into a single chunk.
         /// </summary>
         public ChunkMergeMode MergeMode { get; set; } = ChunkMergeMode.None;
    }
}
