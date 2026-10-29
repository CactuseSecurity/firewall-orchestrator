using FWO.Data.Labelling;
using FWO.Services.Labelling;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Optional label columns of the app server csv upload.
    /// </summary>
    [TestFixture]
    [Parallelizable]
    internal class CsvLabelColumnsTest
    {
        private const int kFixedColumns = 4;
        private static readonly string[] kHeader = ["App-Server-Name", "External-App-ID", "App-Server-Typ", "App-IP-Address-Range", "label:Stage", "\"LABEL: OS \"", "label:", "Remark"];
        private static readonly string[] kFixedHeader = ["App-Server-Name", "External-App-ID", "App-Server-Typ", "App-IP-Address-Range"];
        private static readonly string[] kLine = ["srv", "APP-1", "Type", "10.0.0.1", " Prod ", ""];
        private static readonly List<string> kUnknownColumns = ["label:", "Remark"];
        private static readonly Dictionary<int, string> kLabelColumns = new() { [4] = "Stage", [5] = "OS" };
        private static readonly List<(string, string)> kStageProd = [("Stage", "Prod")];

        [Test]
        public void Find_ReturnsLabelColumnsAndUnknownOptionalColumns()
        {
            Dictionary<int, string> labelColumns = CsvLabelColumns.Find(kHeader, kFixedColumns, out List<string> unknownColumns);

            Assert.Multiple(() =>
            {
                Assert.That(labelColumns, Is.EqualTo(kLabelColumns));
                Assert.That(unknownColumns, Is.EqualTo(kUnknownColumns));
            });
        }

        [Test]
        public void GetLabels_ReturnsTheValuesOfTheLine()
        {
            Dictionary<int, string> labelColumns = CsvLabelColumns.Find(kHeader, kFixedColumns, out _);

            List<ModellingImportLabel>? labels = CsvLabelColumns.GetLabels(kLine, labelColumns);

            Assert.That(labels?.Select(label => (label.Key, label.Value)), Is.EqualTo(kStageProd));
        }

        [Test]
        public void GetLabels_IsNullWithoutLabelColumns()
        {
            Dictionary<int, string> labelColumns = CsvLabelColumns.Find(kFixedHeader, kFixedColumns, out List<string> unknownColumns);

            Assert.That(CsvLabelColumns.GetLabels(kLine, labelColumns), Is.Null);
            Assert.That(unknownColumns, Is.Empty);
        }
    }
}
