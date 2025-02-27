using Microsoft.AspNetCore.Components.Web;
using FWO.Ui.Data;

namespace FWO.Ui.Services
{
    public class KeyboardInputService
    {
        public bool ShiftPressed { get; set; } = false;
        public bool ControlPressed { get; set; } = false;
    }
}
