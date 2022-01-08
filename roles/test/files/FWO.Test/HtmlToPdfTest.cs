﻿using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WkHtmlToPdfDotNet;

namespace FWO.Test.HtmlToPdf
{
    [TestFixture]
    internal class HtmlToPdfTest
    {
        // Pdf converter
        protected readonly SynchronizedConverter converter;

        public HtmlToPdfTest()
        {
            converter = new SynchronizedConverter(new PdfTools());
        }

        [Test]
        public void GeneratePdf()
        {
            // HTML
            string html = "<html> <body> <h1>test<h1> test </body> </html>";

            GlobalSettings globalSettings = new GlobalSettings
            {
                ColorMode = ColorMode.Color,
                Orientation = Orientation.Landscape,
                PaperSize = PaperKind.A4
            };

            HtmlToPdfDocument doc = new HtmlToPdfDocument()
            {
                GlobalSettings = globalSettings,
                Objects =
                {
                    new ObjectSettings()
                    {
                        PagesCount = true,
                        HtmlContent = html,
                        WebSettings = { DefaultEncoding = "utf-8" },
                        HeaderSettings = { FontSize = 9, Right = "Page [page] of [toPage]", Line = true, Spacing = 2.812 }
                    }
                }
            };

            byte[] pdf = converter.Convert(doc);
        }
    }
}
