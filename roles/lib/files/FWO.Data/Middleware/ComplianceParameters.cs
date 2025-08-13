namespace FWO.Data.Middleware
{
    public class ComplianceReportParameters
    {
        public List<int> ManagementIds { get; set; } = [];
    }

    public class ComplianceImportMatrixParameters
    {
        public string FileName { get; set; } = "";
        public string Data { get; set; } = "";
        public string UserName { get; set; } = "";
        public string UserDn { get; set; } = "";
    }
}
