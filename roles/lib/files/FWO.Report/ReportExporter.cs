using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FWO.Report
{
    public abstract class ReportExporter
    {
        public abstract void ToCsv();

        public abstract string ToHtml();

        public abstract string ToPdf();

        protected string Template = @"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8""/>
      <title> ##Title##</title>   
         <style>
             table {
                font-family: arial, sans-serif;
                font-size: 10px;
                border-collapse: collapse;
                width: 100 %;
              }

              td {
                border: 1px solid #000000;
                text-align: left;
                padding: 3px;
              }

              th {
                border: 1px solid #000000;
                text-align: left;
                padding: 3px;
                background-color: #dddddd;
              }
         </style>
    </head>
    <body>
        ##Body##
    </body>
</html>";
    }
}
