namespace FWO.Basics.Exceptions
{
    /// <summary>
    /// Reports a zone matrix that a path analysis cannot work on, because no matrix with that id holds any zone.
    /// This covers a matrix that does not exist, an id of a compliance criterion that is not a matrix, and a
    /// matrix without zones; the three cases are deliberately not told apart.
    /// </summary>
    /// <remarks>
    /// Distinct from the general exceptions so that a caller can react to this case alone: the REST endpoint
    /// answers with a client error when the caller supplied the matrix id, and treats it as a server failure
    /// when the id came from the configured designated zone matrix. Every other failure is still logged as such.
    /// <para>
    /// The message is meant for logs only. Callers build their own caller-facing text from
    /// <see cref="MatrixId"/>, so that the exception carries neither a language nor a transport detail.
    /// </para>
    /// </remarks>
    public class ZoneMatrixUnavailableException : Exception
    {
        /// <summary>
        /// Id of the zone matrix the path analysis could not work on.
        /// </summary>
        public int MatrixId { get; }

        /// <summary>
        /// Creates the exception for the zone matrix the path analysis could not work on.
        /// </summary>
        /// <param name="matrixId">Id of the zone matrix, either supplied by the caller or taken from the configuration.</param>
        public ZoneMatrixUnavailableException(int matrixId)
            : base($"Zone matrix {matrixId} does not exist or holds no zones.")
        {
            MatrixId = matrixId;
        }
    }
}
