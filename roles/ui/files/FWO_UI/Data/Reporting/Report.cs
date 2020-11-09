using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace FWO.Ui.Data
{
    abstract class Report
    {
        public abstract (string name, string csv)[] ToCsv();

        public abstract void ToHtml();

        public abstract void ToPdf();
    }
}
