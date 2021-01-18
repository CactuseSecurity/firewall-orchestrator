using System;
using System.Threading.Tasks;
using FWO.ApiClient.Queries;
using FWO.ApiClient;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using FWO.Api.Data;
using System.Text;

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
            try
            {
                var queryParameter = new { reportId };
                ReportFile file = (await apiConnection.SendQueryAsync<ReportFile[]>(ReportQueries.getGeneratedReports, queryParameter))[0];
                return null;//File(file.Content, file.Type, file.Name);
            }
            catch (Exception exception)
            {
                return File(Encoding.ASCII.GetBytes(exception.Message), "text/html", "error.txt");
            }
        }
    }
}
