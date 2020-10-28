using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FWO.Ui.Data
{
    abstract class Report
    {
        public abstract void ToCsv();

        public abstract void ToHtml();

        public abstract void ToPdf();
    }
}
