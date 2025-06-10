namespace FWO.Data
{
    public class ErrorBaseModel()
    {
        /// <summary>
        /// The error message containing infos abour what went wrong
        /// </summary>
        public string? Message { get; set; }

        /// <summary>
        /// Additional info on errors at system level
        /// </summary>
        public Exception? InternalException { get; set; }

        /// <summary>
        /// Identifier for severity typing
        /// </summary>
        public MessageType MessageType { get; set; }
    }
}
