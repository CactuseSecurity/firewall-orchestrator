using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FWO.Ui.Data
{
    public delegate void NotifyCollapse();
    public class CollapseState
    {
        public event NotifyCollapse? OnCollapseAll;
        public event NotifyCollapse? OnExpandAll;

        public void CollapseAll()
        {
            OnCollapseAll?.Invoke();
        }

        public void ExpandAll()
        {
            OnExpandAll?.Invoke();
        }
    }
}
