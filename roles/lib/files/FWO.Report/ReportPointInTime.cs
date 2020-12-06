using FWO.Api.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FWO.Report
{
    public class ReportPointInTime : Report
    {
        public Management[] Managements { get; set; }

        public override string ToCsv()
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

        public override string ToHtml()
        {
            throw new NotImplementedException();
        }

        public override string ToPdf()
        {
            throw new NotImplementedException();
        }
    }
}
