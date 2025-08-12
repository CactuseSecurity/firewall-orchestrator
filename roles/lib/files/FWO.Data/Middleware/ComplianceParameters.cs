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
    }
}
