using FWO.Basics;
using FWO.Data;
using FWO.Data.Report;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class ReportFileTest
    {
        private static ReportFile BuildFilledReportFile()
        {
            return new ReportFile
            {
                Id = 7,
                Name = "archived report",
                OwningUserId = 3,
                Type = (int)ReportType.Rules,
                Description = "a description",
                ReportOwningUser = new UiUser { Name = "reporter" },
                Json = "{\"rules\":[]}",
                Pdf = "cGRmIGNvbnRlbnQ=",
                Html = "<html>report</html>",
                Csv = "a;b;c"
            };
        }

        [Test]
        public void ReleaseContentDropsEveryGeneratedPayload()
        {
            ReportFile reportFile = BuildFilledReportFile();

            reportFile.ReleaseContent();

            Assert.Multiple(() =>
            {
                Assert.That(reportFile.Json, Is.Null);
                Assert.That(reportFile.Pdf, Is.Null);
                Assert.That(reportFile.Html, Is.Null);
                Assert.That(reportFile.Csv, Is.Null);
            });
        }

        [Test]
        public void ReleaseContentKeepsTheMetadataThatTheArchiveTableShows()
        {
            ReportFile reportFile = BuildFilledReportFile();

            reportFile.ReleaseContent();

            Assert.Multiple(() =>
            {
                Assert.That(reportFile.Id, Is.EqualTo(7));
                Assert.That(reportFile.Name, Is.EqualTo("archived report"));
                Assert.That(reportFile.OwningUserId, Is.EqualTo(3));
                Assert.That(reportFile.Type, Is.EqualTo((int)ReportType.Rules));
                Assert.That(reportFile.Description, Is.EqualTo("a description"));
                Assert.That(reportFile.ReportOwningUser.Name, Is.EqualTo("reporter"));
            });
        }

        [Test]
        public void ReleaseContentIsIdempotent()
        {
            ReportFile reportFile = BuildFilledReportFile();

            reportFile.ReleaseContent();

            Assert.DoesNotThrow(reportFile.ReleaseContent);
            Assert.That(reportFile.Html, Is.Null);
        }

        [Test]
        public void ReleaseContentOnAnEmptyReportFileChangesNothing()
        {
            ReportFile reportFile = new();

            Assert.DoesNotThrow(reportFile.ReleaseContent);
            Assert.That(reportFile.Name, Is.EqualTo(""));
        }
    }
}
