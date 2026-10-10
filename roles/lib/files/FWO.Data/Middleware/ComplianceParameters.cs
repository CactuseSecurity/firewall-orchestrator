namespace FWO.Data.Middleware
{
    /// <summary>
    /// Parameters of the compliance report request.
    /// </summary>
    public class ComplianceReportParameters
    {
        /// <summary>
        /// Ids of the managements to report. Each id must exist, be visible to the caller and be relevant for the
        /// compliance check, otherwise the request is rejected. Duplicate ids are ignored.
        /// Defaults to an empty list, which reports all managements visible to the caller and relevant for the compliance check.
        /// </summary>
        public List<int> ManagementIds { get; set; } = [];
    }

    public class ImportMatrixParameters
    {
        public string FileName { get; set; } = "";
        public string Data { get; set; } = "";
        public string UserName { get; set; } = "";
        public string UserDn { get; set; } = "";
    }
}
