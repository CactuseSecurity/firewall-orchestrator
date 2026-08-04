using Bunit;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Report;
using FWO.Ui.Pages.Reporting;
using FWO.Ui.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using NUnit.Framework;
using System.Text;

namespace FWO.Test
{
    [TestFixture]
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    internal class UiReportDownloadPopUpTest
    {
        private const string kDownloadFunction = "DownloadFileFromStream";

        [SetUp]
        public void SetUpTranslations()
        {
            SimulatedUserConfig.DummyTranslate.TryAdd("export_report_download", "Download export");
            SimulatedUserConfig.DummyTranslate.TryAdd("download_csv", "Download CSV");
            SimulatedUserConfig.DummyTranslate.TryAdd("download_pdf", "Download PDF");
            SimulatedUserConfig.DummyTranslate.TryAdd("download_html", "Download HTML");
            SimulatedUserConfig.DummyTranslate.TryAdd("download_json", "Download JSON");
            SimulatedUserConfig.DummyTranslate.TryAdd("close", "Close");
            SimulatedUserConfig.DummyTranslate.TryAdd("exporting", "Exporting");
        }

        private static BunitContext CreateContext()
        {
            BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddLocalization();
            context.Services.AddSingleton<UserConfig>(new SimulatedUserConfig());
            context.Services.AddScoped<DomEventService>();
            return context;
        }

        private static ReportFile BuildReportFile()
        {
            return new ReportFile
            {
                Name = "export",
                Csv = "a;b;c",
                Html = "<html>report</html>",
                Json = "{\"rules\":[]}",
                // "pdf content" base64 encoded
                Pdf = Convert.ToBase64String(Encoding.UTF8.GetBytes("pdf content"))
            };
        }

        private static IRenderedComponent<ReportDownloadPopUp> Render(BunitContext context, ReportFile? reportFile,
            bool showJson = true, Action? onClose = null)
        {
            return context.Render<ReportDownloadPopUp>(parameters => parameters
                .Add(p => p.ReportFile, reportFile)
                .Add(p => p.Show, true)
                .Add(p => p.ShowJson, showJson)
                .Add(p => p.OnClose, onClose ?? new Action(() => { })));
        }

        [Test]
        public void ShowsOneDownloadButtonPerAvailableFormat()
        {
            using BunitContext context = CreateContext();

            IRenderedComponent<ReportDownloadPopUp> popUp = Render(context, BuildReportFile());

            string markup = popUp.Markup;
            Assert.Multiple(() =>
            {
                Assert.That(markup, Does.Contain("Download CSV"));
                Assert.That(markup, Does.Contain("Download PDF"));
                Assert.That(markup, Does.Contain("Download HTML"));
                Assert.That(markup, Does.Contain("Download JSON"));
            });
        }

        [Test]
        public void HidesTheJsonButtonWhenJsonIsNotRequested()
        {
            using BunitContext context = CreateContext();

            IRenderedComponent<ReportDownloadPopUp> popUp = Render(context, BuildReportFile(), showJson: false);

            Assert.That(popUp.Markup, Does.Not.Contain("Download JSON"));
            Assert.That(popUp.Markup, Does.Contain("Download CSV"));
        }

        [Test]
        public void OffersOnlyTheFormatsThatWereActuallyGenerated()
        {
            using BunitContext context = CreateContext();
            ReportFile onlyCsv = new() { Name = "export", Csv = "a;b;c" };

            IRenderedComponent<ReportDownloadPopUp> popUp = Render(context, onlyCsv);

            Assert.Multiple(() =>
            {
                Assert.That(popUp.Markup, Does.Contain("Download CSV"));
                Assert.That(popUp.Markup, Does.Not.Contain("Download PDF"));
                Assert.That(popUp.Markup, Does.Not.Contain("Download HTML"));
                Assert.That(popUp.Markup, Does.Not.Contain("Download JSON"));
            });
        }

        [Test]
        public void DownloadingCsvStreamsItToTheBrowserInsteadOfPassingABuffer()
        {
            using BunitContext context = CreateContext();
            var handler = context.JSInterop.SetupVoid(kDownloadFunction, _ => true);
            IRenderedComponent<ReportDownloadPopUp> popUp = Render(context, BuildReportFile());

            popUp.FindAll("btn")[0].Click();
            // completing the invocation lets the component run past the await, so the spinner reset
            // and the disposal of the transfer stream are exercised too
            handler.SetVoidResult();

            JSRuntimeInvocation invocation = context.JSInterop.Invocations[kDownloadFunction].Single();
            Assert.Multiple(() =>
            {
                Assert.That(invocation.Arguments[0], Is.EqualTo("export.csv"));
                Assert.That(invocation.Arguments[1], Is.EqualTo("text/csv"));
                // the payload must travel as a stream reference, never as a byte[] argument
                Assert.That(invocation.Arguments[2], Is.InstanceOf<DotNetStreamReference>());
            });
            // the spinner is cleared on the continuation after the transfer, so wait for that render
            popUp.WaitForAssertion(() => Assert.That(popUp.Markup, Does.Contain("Download CSV")));
        }

        [Test]
        public void DownloadingHtmlUsesTheHtmlNameAndContentType()
        {
            using BunitContext context = CreateContext();
            context.JSInterop.SetupVoid(kDownloadFunction, _ => true);
            ReportFile onlyHtml = new() { Name = "export", Html = "<html>report</html>" };
            IRenderedComponent<ReportDownloadPopUp> popUp = Render(context, onlyHtml);

            popUp.FindAll("btn")[0].Click();

            JSRuntimeInvocation invocation = context.JSInterop.Invocations[kDownloadFunction].Single();
            Assert.Multiple(() =>
            {
                Assert.That(invocation.Arguments[0], Is.EqualTo("export.html"));
                Assert.That(invocation.Arguments[1], Is.EqualTo("text/html"));
            });
        }

        [Test]
        public void DownloadingJsonUsesTheJsonNameAndContentType()
        {
            using BunitContext context = CreateContext();
            context.JSInterop.SetupVoid(kDownloadFunction, _ => true);
            ReportFile onlyJson = new() { Name = "export", Json = "{\"rules\":[]}" };
            IRenderedComponent<ReportDownloadPopUp> popUp = Render(context, onlyJson);

            popUp.FindAll("btn")[0].Click();

            JSRuntimeInvocation invocation = context.JSInterop.Invocations[kDownloadFunction].Single();
            Assert.Multiple(() =>
            {
                Assert.That(invocation.Arguments[0], Is.EqualTo("export.json"));
                Assert.That(invocation.Arguments[1], Is.EqualTo("application/json"));
            });
        }

        [Test]
        public void DownloadingPdfDecodesTheBase64PayloadBeforeStreamingIt()
        {
            using BunitContext context = CreateContext();
            context.JSInterop.SetupVoid(kDownloadFunction, _ => true);
            ReportFile onlyPdf = new()
            {
                Name = "export",
                Pdf = Convert.ToBase64String(Encoding.UTF8.GetBytes("pdf content"))
            };
            IRenderedComponent<ReportDownloadPopUp> popUp = Render(context, onlyPdf);

            popUp.FindAll("btn")[0].Click();

            JSRuntimeInvocation invocation = context.JSInterop.Invocations[kDownloadFunction].Single();
            Assert.Multiple(() =>
            {
                Assert.That(invocation.Arguments[0], Is.EqualTo("export.pdf"));
                Assert.That(invocation.Arguments[1], Is.EqualTo("application/octet-stream"));
                Assert.That(invocation.Arguments[2], Is.InstanceOf<DotNetStreamReference>());
            });
        }

        [Test]
        public void DownloadingTextThatWasReleasedInTheMeantimeDoesNothing()
        {
            using BunitContext context = CreateContext();
            context.JSInterop.SetupVoid(kDownloadFunction, _ => true);
            ReportFile reportFile = new() { Name = "export", Csv = "a;b;c" };
            IRenderedComponent<ReportDownloadPopUp> popUp = Render(context, reportFile);

            // the payload can be released between rendering the button and clicking it
            reportFile.Csv = null;
            popUp.FindAll("btn")[0].Click();

            Assert.That(context.JSInterop.Invocations[kDownloadFunction], Is.Empty);
        }

        [Test]
        public void DownloadingAPdfThatWasReleasedInTheMeantimeDoesNothing()
        {
            using BunitContext context = CreateContext();
            context.JSInterop.SetupVoid(kDownloadFunction, _ => true);
            ReportFile reportFile = new()
            {
                Name = "export",
                Pdf = Convert.ToBase64String(Encoding.UTF8.GetBytes("pdf content"))
            };
            IRenderedComponent<ReportDownloadPopUp> popUp = Render(context, reportFile);

            reportFile.Pdf = null;
            popUp.FindAll("btn")[0].Click();

            Assert.That(context.JSInterop.Invocations[kDownloadFunction], Is.Empty);
        }

        [Test]
        public void AFailedTransferIsSwallowedAndTheSpinnerIsResetAgain()
        {
            using BunitContext context = CreateContext();
            context.JSInterop.SetupVoid(kDownloadFunction, _ => true)
                .SetException(new InvalidOperationException("browser went away"));
            IRenderedComponent<ReportDownloadPopUp> popUp = Render(context, BuildReportFile());

            Assert.DoesNotThrow(() => popUp.FindAll("btn")[0].Click());

            // the dialog has to become usable again rather than being stuck on the spinner
            popUp.WaitForAssertion(() => Assert.That(popUp.Markup, Does.Contain("Download CSV")));
        }

        [Test]
        public void ClosingReleasesEveryGeneratedPayload()
        {
            using BunitContext context = CreateContext();
            ReportFile reportFile = BuildReportFile();
            IRenderedComponent<ReportDownloadPopUp> popUp = Render(context, reportFile);

            popUp.Find("button.btn-danger").Click();

            Assert.Multiple(() =>
            {
                Assert.That(reportFile.Csv, Is.Null);
                Assert.That(reportFile.Pdf, Is.Null);
                Assert.That(reportFile.Html, Is.Null);
                Assert.That(reportFile.Json, Is.Null);
            });
        }

        [Test]
        public void ClosingStillNotifiesTheParent()
        {
            using BunitContext context = CreateContext();
            bool closed = false;
            IRenderedComponent<ReportDownloadPopUp> popUp = Render(context, BuildReportFile(), onClose: () => closed = true);

            popUp.Find("button.btn-danger").Click();

            Assert.That(closed, Is.True);
        }

        [Test]
        public void ClosingWithoutAReportFileDoesNotThrow()
        {
            using BunitContext context = CreateContext();
            bool closed = false;
            IRenderedComponent<ReportDownloadPopUp> popUp = Render(context, null, onClose: () => closed = true);

            Assert.DoesNotThrow(() => popUp.Find("button.btn-danger").Click());
            Assert.That(closed, Is.True);
        }
    }
}
