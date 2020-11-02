using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FWO.Ui.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FWO.Ui.Pages
{
    public class DownloadManagerModel : PageModel
    {
        private DownloadManagerService downloadManager;

        public DownloadManagerModel(DownloadManagerService downloadManager)
        {
            this.downloadManager = downloadManager;
        }

        public IActionResult OnGet(string name)
        {
            Download download = downloadManager.Downloads.Find(download => download.Name == name);
            FileStream stream = System.IO.File.OpenRead("");
            return File(download.GetContent(), download.Type, download.Name);
        }
    }
}
