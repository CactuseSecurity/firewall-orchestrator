using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FWO.Ui.Data
{
    public delegate void NotifyCollapse(string? location); // optional location in rsb {tab}-{mgm/rule id}-{objtype} e.g. "report-m2-nwobj"
    public class CollapseState
    {
        public event NotifyCollapse? OnCollapse;
        public event NotifyCollapse? OnExpand;

        public void Collapse(string? location = null)
        {
            OnCollapse?.Invoke(location);
        }

        public void Expand(string? location = null)
        {
            OnExpand?.Invoke(location);
        }
    }
}
