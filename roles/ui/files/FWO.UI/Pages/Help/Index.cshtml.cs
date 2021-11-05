using Microsoft.AspNetCore.Mvc.RazorPages;
using FWO.Config.Api;

namespace FWO.Ui.Pages.Help
{
    public class MainModel : PageModel
    {
        public UserConfig userConfig { get; set; }

        public MainModel(UserConfig userConfig)
        {
            this.userConfig = userConfig;
        }

        public void OnGet(string lang)
        {
            userConfig.SetLanguage(lang);
        }
    }
}
