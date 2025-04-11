using FWO.Basics;
using FWO.Data.Modelling;

namespace FWO.Ui.Data
{
    public class CSVAppServerImportModel
    {
        public string? AppServerName { get; set; }
        public string? AppID { get; set; }
        public string? AppServerTyp { get; set; }
        public string? AppIPRangeStart { get; set; }
        public string? AppIPRangeEnd { get; set; }

        public CSVAppServerImportModel (string ipString)
        {
            (AppIPRangeStart, AppIPRangeEnd) = IpOperations.SplitIpToRange(ipString);
        }

        public CSVAppServerImportModel()
        {
                
        }

        public ModellingAppServer ToModellingAppServer()
        {
            return new ModellingAppServer()
            {
                Name = AppServerName ?? "",
                Ip = AppIPRangeStart ?? "",
                IpEnd = AppIPRangeEnd ?? ""
            };
        }
    }
}
