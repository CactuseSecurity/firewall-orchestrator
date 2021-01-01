using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FWO.ApiClient.Queries;
using FWO.ApiClient;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FWO.Ui.Pages
{
    public class DownloadManagerModel : PageModel
    {
        APIConnection apiConnection;

        public DownloadManagerModel(APIConnection apiConnection)
        {
            this.apiConnection = apiConnection;
        }

        public async Task<IActionResult> OnGetAsync(string reportId)
        {
            var queryParameter = new { reportId };
            //await apiConnection.SendQueryAsync<string>(ReportQueries.getSavedReports, queryParameter);
            //return File(download.GetContent(), download.Type, download.Name);
            return null;
        }
    }
}
