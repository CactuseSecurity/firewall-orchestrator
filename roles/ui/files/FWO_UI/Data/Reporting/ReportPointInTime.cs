using FWO.Ui.Data.API;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FWO.Ui.Data
{
    class ReportPointInTime : Report
    {
        public Management[] Managements { get; set; }

        public override (string name, string csv)[] ToCsv()
        {
            StringBuilder csvBuilder = new StringBuilder();

            foreach (Management management in Managements)
            {
                //foreach (var item in collection)
                //{

                //}
            }

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
