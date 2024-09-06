﻿using NUnit.Framework;
using NUnit.Framework.Legacy;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WkHtmlToPdfDotNet;
using FWO.Logging;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class HtmlToPdfTest
    {
        // Pdf converter
        protected readonly SynchronizedConverter converter;

        public HtmlToPdfTest()
        {
            converter = new SynchronizedConverter(new PdfTools());
        }

        [Test]
        [Parallelizable]
        public void GeneratePdf()
        {
            Log.WriteInfo("Test Log", "starting PDF generation");
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
            string filePath = "test.pdf";
            using (var s = File.OpenWrite(filePath)) {
              var bw = new BinaryWriter(s);
              bw.Write(pdf);
            }
            Assert.That(filePath, Does.Exist);
            ClassicAssert.Greater(new System.IO.FileInfo(filePath).Length, 5000);
        }

        [OneTimeTearDown]
        public void OnFinished()
        {
            File.Delete("test.pdf");
        }
    }
}
