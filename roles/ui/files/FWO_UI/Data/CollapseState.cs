using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FWO.Ui.Data
{
    public delegate void Notify(bool show);
    public class CollapseState
    {
        public event Notify OnCollapseAll;

        public void CollapseAll()
        {
            OnCollapseAll?.Invoke(false);
        }
    }
}
