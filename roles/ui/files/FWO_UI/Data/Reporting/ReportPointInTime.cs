using FWO.Ui.Data.API;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FWO.Ui.Data
{
    class ReportPointInTime : Report
    {
        public Management[] Managements { get; set; }

        public override void ToCsv()
        {
            throw new NotImplementedException();
        }

        public override void ToHtml()
        {
            throw new NotImplementedException();
        }

        public override void ToPdf()
        {
            throw new NotImplementedException();
        }
    }
}
